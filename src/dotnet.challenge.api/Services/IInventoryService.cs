using dotnet.challenge.data.Entities;

namespace dotnet.challenge.api.Services;

public enum ReserveResult
{
    Success,
    ProductNotFound,
    InsufficientStock
}

public enum ConfirmResult
{
    Success,
    NotFound,
    AlreadyConfirmed,
    IllegalState
}

public enum ReleaseResult
{
    Success,
    NotFound,
    AlreadyReleased
}

public interface IInventoryService
{
    Task<Product> CreateProductAsync(string name, int stock);

    Task<Product?> GetProductAsync(Guid id);

    Task<IEnumerable<Product>> GetProductsAsync();

    Task<(ReserveResult Result, Reservation? Reservation)> TryReserveAsync(Guid productId, int quantity, int ttlSeconds);

    Task<Reservation?> GetReservationAsync(Guid id);

    Task<ConfirmResult> ConfirmAsync(Guid reservationId);

    Task<ReleaseResult> ReleaseAsync(Guid reservationId);

    Task SweepExpiredAsync();
}
