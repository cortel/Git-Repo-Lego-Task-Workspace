using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhoneNumbersManagement.Application;
using PhoneNumbersManagement.Domain;

namespace PhoneNumbersManagement.Controllers;

[ApiController, Route("v1/phonenumbers")]
public sealed class PhoneNumbersController(RegistryService service) : ControllerBase
{
    [HttpGet, Authorize(Policy = "read")]
    public Task<Page<PhoneResponse>> GetAll([FromQuery] PhoneQuery query, CancellationToken ct) => service.PhonesAsync(query, ct);

    [HttpGet("{id:guid}"), Authorize(Policy = "read")]
    public Task<PhoneResponse> GetById(Guid id, CancellationToken ct) => service.PhoneAsync(id, ct);

    [HttpGet("getnextavailable"), Authorize(Policy = "read")]
    public Task<IReadOnlyList<PhoneResponse>> NextAvailable([FromQuery] Guid rangeId, [FromQuery] int amount, CancellationToken ct) => service.NextAvailableAsync(rangeId, amount, ct);

    [HttpPut("{id:guid}"), Authorize(Policy = "write")]
    public Task<PhoneResponse> Update(Guid id, PhoneInput input, CancellationToken ct) => service.UpdatePhoneAsync(id, input, ct);

    [HttpPatch("{id:guid}"), Authorize(Policy = "write")]
    public async Task<PhoneResponse> Patch(Guid id, JsonElement patch, CancellationToken ct)
    {
        var phone = await service.PhoneAsync(id, ct);
        return await service.UpdatePhoneAsync(id, MergePatch.Apply(phone.ToInput(), patch), ct, phone.Version);
    }
}
