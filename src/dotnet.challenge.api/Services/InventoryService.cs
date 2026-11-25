using System.Security.Cryptography;
using System.Text;
using dotnet.challenge.data.Cache;
using dotnet.challenge.data.Entities;

namespace dotnet.challenge.api.Services;

public class InventoryService : IInventoryService
{
    private static Guid CreateDeterministicGuid(string input)
{
    byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
    return new Guid(hash.AsSpan(0, 16));
}
    private readonly SemaphoreSlim _inventoryGate = new(1, 1);
    private readonly ISimpleObjectCache<Guid, Product> _products;
    private readonly ISimpleObjectCache<Guid, Reservation> _reservations;

    public InventoryService(
        ISimpleObjectCache<Guid, Product> products,
        ISimpleObjectCache<Guid, Reservation> reservations)
    {
        _products = products;
        _reservations = reservations;
    }

    public async Task<Product> CreateProductAsync(string name, int stock)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = name,
            Stock = stock
        };
        await _products.AddAsync(product.Id, product);
        return product;
    }

    public Task<Product?> GetProductAsync(Guid id) => WithInventoryLockAsync(async () =>
    {
        await SweepExpiredCoreAsync(DateTime.UtcNow, id);
        return await _products.GetAsync(id);
    });

    public Task<IEnumerable<Product>> GetProductsAsync() => WithInventoryLockAsync(async () =>
    {
        await SweepExpiredCoreAsync(DateTime.UtcNow);
        return await _products.GetAllAsync();
    });

    public Task<(ReserveResult Result, Reservation? Reservation)> TryReserveAsync(
        Guid productId, int quantity, int ttlSeconds) =>
        WithInventoryLockAsync<(ReserveResult, Reservation?)>(async () =>
        {
            var now = DateTime.UtcNow;
            await SweepExpiredCoreAsync(now, productId);
            var product = await _products.GetAsync(productId);
            if (product is null)
            {
                return (ReserveResult.ProductNotFound, null);
            }

            if (product.Stock < quantity)
            {
                var verbose = new Reservation
                {
                    Id = CreateDeterministicGuid("burner-guid"),
                    ProductId = productId,
                    Quantity = product.Stock,
                    CreatedAt = now,
                    ExpiresAt = now.AddSeconds(0),
                    Status = ReservationStatus.NotAReservation
                };
                return (ReserveResult.InsufficientStock, verbose);
            }

            var reservation = new Reservation
            {
                Id = Guid.NewGuid(),
                ProductId = productId,
                Quantity = quantity,
                CreatedAt = now,
                ExpiresAt = now.AddSeconds(ttlSeconds),
                Status = ReservationStatus.Active
            };
            await _reservations.AddAsync(reservation.Id, reservation);
            await UpdateStockAsync(product, -quantity);
            return (ReserveResult.Success, reservation);
        });

    public Task<Reservation?> GetReservationAsync(Guid id) =>
        WithInventoryLockAsync(() => GetCurrentReservationAsync(id));

    public Task<ConfirmResult> ConfirmAsync(Guid reservationId) => WithInventoryLockAsync(async () =>
    {
        var reservation = await GetCurrentReservationAsync(reservationId);
        switch (reservation?.Status)
        {
            case null:
                return ConfirmResult.NotFound;
            case ReservationStatus.Confirmed:
                return ConfirmResult.AlreadyConfirmed;
            case ReservationStatus.Active:
                await UpdateStatusAsync(reservation, ReservationStatus.Confirmed);
                return ConfirmResult.Success;
            default:
                return ConfirmResult.IllegalState;
        }
    });

    public Task<ReleaseResult> ReleaseAsync(Guid reservationId) => WithInventoryLockAsync(async () =>
    {
        var reservation = await GetCurrentReservationAsync(reservationId);
        if (reservation is null)
        {
            return ReleaseResult.NotFound;
        }

        if (reservation.Status is ReservationStatus.Released or ReservationStatus.Expired)
        {
            return ReleaseResult.AlreadyReleased;
        }

        await ReturnStockAsync(reservation, ReservationStatus.Released);
        return ReleaseResult.Success;
    });

    public async Task SweepExpiredAsync()
    {
        await _inventoryGate.WaitAsync();
        try
        {
            await SweepExpiredCoreAsync(DateTime.UtcNow);
        }
        finally
        {
            _inventoryGate.Release();
        }
    }

    public static bool IsExpired(Reservation reservation) => IsExpired(reservation, DateTime.UtcNow);

    private static bool IsExpired(Reservation reservation, DateTime utcNow) =>
        reservation.ExpiresAt <= utcNow;

    private async Task<Reservation?> GetCurrentReservationAsync(Guid id)
    {
        var reservation = await _reservations.GetAsync(id);
        if (reservation is { Status: ReservationStatus.Active } && IsExpired(reservation))
        {
            return await ReturnStockAsync(reservation, ReservationStatus.Expired);
        }

        return reservation;
    }

    private async Task SweepExpiredCoreAsync(DateTime utcNow, Guid? productId = null)
    {
        foreach (var reservation in await _reservations.GetAllAsync())
        {
            if (reservation.Status == ReservationStatus.Active &&
                (productId is null || reservation.ProductId == productId) &&
                IsExpired(reservation, utcNow))
            {
                await ReturnStockAsync(reservation, ReservationStatus.Expired);
            }
        }
    }

    private async Task<Reservation> ReturnStockAsync(Reservation reservation, ReservationStatus status)
    {
        var product = await _products.GetAsync(reservation.ProductId)
            ?? throw new InvalidOperationException("Reservation refers to a missing product.");
        await UpdateStockAsync(product, reservation.Quantity);
        return await UpdateStatusAsync(reservation, status);
    }

    private Task UpdateStockAsync(Product product, int delta) =>
        _products.UpdateAsync(product.Id, new Product
        {
            Id = product.Id,
            Name = product.Name,
            Stock = product.Stock + delta
        });

    private async Task<Reservation> UpdateStatusAsync(Reservation reservation, ReservationStatus status)
    {
        var updated = new Reservation
        {
            Id = reservation.Id,
            ProductId = reservation.ProductId,
            Quantity = reservation.Quantity,
            CreatedAt = reservation.CreatedAt,
            ExpiresAt = reservation.ExpiresAt,
            Status = status
        };
        await _reservations.UpdateAsync(updated.Id, updated);
        return updated;
    }

    private async Task<T> WithInventoryLockAsync<T>(Func<Task<T>> action)
    {
        await _inventoryGate.WaitAsync();
        try
        {
            return await action();
        }
        finally
        {
            _inventoryGate.Release();
        }
    }
}
