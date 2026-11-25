using dotnet.challenge.api.Models;
using dotnet.challenge.api.Services;
using Microsoft.AspNetCore.Mvc;

namespace dotnet.challenge.api.Controllers;

[ApiController]
[Route("api/reservations")]
[Produces("application/json")]
public class ReservationsController : ControllerBase
{
    private readonly IInventoryService _inventory;

    public ReservationsController(IInventoryService inventory)
    {
        _inventory = inventory;
    }

    [HttpPost]
    public async Task<IActionResult> CreateReservation(
        [FromBody] CreateReservationRequest request,
        [FromHeader(Name = "X-Verbose")] string? verbose = null)
    {
        var (result, reservation) = await _inventory.TryReserveAsync(
            request.ProductId, request.Quantity, request.TtlSeconds);



        switch (result)
        {
            case ReserveResult.Success:
                return CreatedAtAction(nameof(GetReservation), new { id = reservation!.Id }, reservation);

            case ReserveResult.ProductNotFound:
                return NotFound(new { error = "Product not found." });

            case ReserveResult.InsufficientStock:
                if (bool.TryParse(verbose, out var includeDetails) && includeDetails)
                {
                    var details = FormattableString.Invariant(
                        $"result={result}; requestedQuantity={request.Quantity}; stockQuantity={reservation?.Quantity}");

                    Response.Headers["X-Reservation-Details"] = details;
                }
                return Conflict(new { error = "Insufficient stock." });

            default:
                return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetReservation(Guid id)
    {
        var reservation = await _inventory.GetReservationAsync(id);
        if (reservation is null)
        {
            return NotFound();
        }
        return Ok(reservation);
    }

    [HttpPost("{id:guid}/confirm")]
    public async Task<IActionResult> Confirm(Guid id)
    {
        var result = await _inventory.ConfirmAsync(id);
        return result switch
        {
            ConfirmResult.Success => Ok(),
            ConfirmResult.AlreadyConfirmed => Ok(),
            ConfirmResult.NotFound => NotFound(),
            ConfirmResult.IllegalState => Conflict(new { error = "Reservation is not in a confirmable state." }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Release(Guid id)
    {
        var result = await _inventory.ReleaseAsync(id);
        return result switch
        {
            ReleaseResult.Success => NoContent(),
            ReleaseResult.AlreadyReleased => NoContent(),
            ReleaseResult.NotFound => NotFound(),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }
}
