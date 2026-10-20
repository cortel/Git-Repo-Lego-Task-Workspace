using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhoneNumbersManagement.Application;
using PhoneNumbersManagement.Domain;

namespace PhoneNumbersManagement.Controllers;

[ApiController, Route("v1/reservations")]
public sealed class ReservationsController(RegistryService service) : ControllerBase
{
    [HttpGet, Authorize(Policy = "read")]
    public Task<Page<ReservationResponse>> GetAll([FromQuery] ReservationQuery query, CancellationToken ct) => service.ReservationsAsync(query, ct);

    [HttpGet("{id:guid}"), Authorize(Policy = "read")]
    public Task<ReservationResponse> GetById(Guid id, CancellationToken ct) => service.ReservationAsync(id, ct);

    [HttpPost, Authorize(Policy = "write")]
    public async Task<ActionResult<ReservationResponse>> Create(ReservationInput input, CancellationToken ct)
    {
        var reservation = await service.CreateReservationAsync(input, ct);
        return CreatedAtAction(nameof(GetById), new
        {
            id = reservation.Id
        }, reservation);
    }

    [HttpPut("{id:guid}"), Authorize(Policy = "write")]
    public Task<ReservationResponse> Update(Guid id, ReservationInput input, CancellationToken ct) => service.UpdateReservationAsync(id, input, ct);

    [HttpPatch("{id:guid}"), Authorize(Policy = "write")]
    public async Task<ReservationResponse> Patch(Guid id, JsonElement patch, CancellationToken ct)
    {
        var r = await service.ReservationAsync(id, ct);
        return await service.UpdateReservationAsync(id, MergePatch.Apply(r.ToInput(), patch), ct, r.Version);
    }

    [HttpDelete("{id:guid}"), Authorize(Policy = "admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteReservationAsync(id, ct);
        return NoContent();
    }
}
