using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhoneNumbersManagement.Application;
using PhoneNumbersManagement.Domain;

namespace PhoneNumbersManagement.Controllers;

[ApiController, Route("v1/ranges")]
public sealed class RangesController(RegistryService service) : ControllerBase
{
    [HttpGet, Authorize(Policy = "read")]
    public Task<Page<RangeResponse>> GetAll([FromQuery] RangeQuery query, CancellationToken ct) => service.RangesAsync(query, ct);

    [HttpGet("{id:guid}"), Authorize(Policy = "read")]
    public Task<RangeResponse> GetById(Guid id, CancellationToken ct) => service.RangeAsync(id, ct);

    [HttpPost, Authorize(Policy = "admin")]
    public async Task<ActionResult<RangeResponse>> Create(RangeInput input, CancellationToken ct)
    {
        var range = await service.CreateRangeAsync(input, ct);
        return CreatedAtAction(nameof(GetById), new
        {
            id = range.Id
        }, range);
    }

    [HttpPut("{id:guid}"), Authorize(Policy = "admin")]
    public Task<RangeResponse> Update(Guid id, RangeInput input, CancellationToken ct) => service.UpdateRangeAsync(id, input, ct);

    [HttpPatch("{id:guid}"), Authorize(Policy = "admin")]
    public async Task<RangeResponse> Patch(Guid id, JsonElement patch, CancellationToken ct)
    {
        var range = await service.RangeAsync(id, ct);
        var input = range.ToInput();
        return await service.UpdateRangeAsync(id, MergePatch.Apply(input, patch), ct, range.Version);
    }

    [HttpDelete("{id:guid}"), Authorize(Policy = "admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteRangeAsync(id, ct);
        return NoContent();
    }
}
