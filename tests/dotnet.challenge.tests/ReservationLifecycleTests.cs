using dotnet.challenge.api.Services;
using dotnet.challenge.data.Cache;
using dotnet.challenge.data.Entities;

namespace dotnet.challenge.tests;

public class ReservationLifecycleTests
{
    [Fact]
    public async Task ConfirmAndRelease_CanBeRepeatedWithoutChangingStockTwice()
    {
        var service = new InventoryService(
            new SimpleObjectCache<Guid, Product>(), new SimpleObjectCache<Guid, Reservation>());
        var product = await service.CreateProductAsync("Widget", 5);
        var (_, reservation) = await service.TryReserveAsync(product.Id, 2, 60);
        var id = reservation!.Id;

        (await service.ConfirmAsync(id)).Should().Be(ConfirmResult.Success);
        (await service.ConfirmAsync(id)).Should().Be(ConfirmResult.AlreadyConfirmed);
        (await service.GetProductAsync(product.Id))!.Stock.Should().Be(3);

        (await service.ReleaseAsync(id)).Should().Be(ReleaseResult.Success);
        (await service.ReleaseAsync(id)).Should().Be(ReleaseResult.AlreadyReleased);
        (await service.GetProductAsync(product.Id))!.Stock.Should().Be(5);
        (await service.ConfirmAsync(id)).Should().Be(ConfirmResult.IllegalState);
    }

    [Fact]
    public async Task ProductRead_ExpiresOverdueReservationAndRestoresStockOnce()
    {
        var reservations = new SimpleObjectCache<Guid, Reservation>();
        var service = new InventoryService(new SimpleObjectCache<Guid, Product>(), reservations);
        var product = await service.CreateProductAsync("Widget", 3);
        var overdue = new Reservation
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            Quantity = 2,
            CreatedAt = DateTime.UtcNow.AddMinutes(-2),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1),
            Status = ReservationStatus.Active
        };
        await reservations.AddAsync(overdue.Id, overdue);

        (await service.GetProductAsync(product.Id))!.Stock.Should().Be(5);
        (await reservations.GetAsync(overdue.Id))!.Status.Should().Be(ReservationStatus.Expired);
        (await service.ConfirmAsync(overdue.Id)).Should().Be(ConfirmResult.IllegalState);
        (await service.ReleaseAsync(overdue.Id)).Should().Be(ReleaseResult.AlreadyReleased);
        await service.SweepExpiredAsync();
        (await service.GetProductAsync(product.Id))!.Stock.Should().Be(5);
    }
}
