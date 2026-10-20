using PhoneNumbersManagement.Domain;

namespace PhoneNumbersManagement.Application;

public sealed class RegistryService(IRegistryStore store, TimeProvider clock)
{
    public Task<Page<RangeResponse>> RangesAsync(RangeQuery q, CancellationToken ct)
    {
        ValidatePage(q.Offset, q.Limit);
        return store.RangesAsync(q, ct);
    }

    public Task<Page<PhoneResponse>> PhonesAsync(PhoneQuery q, CancellationToken ct)
    {
        ValidatePage(q.Offset, q.Limit);
        return store.PhonesAsync(q, ct);
    }

    public Task<Page<ReservationResponse>> ReservationsAsync(ReservationQuery q, CancellationToken ct)
    {
        ValidatePage(q.Offset, q.Limit);
        return store.ReservationsAsync(q, ct);
    }

    public async Task<RangeResponse> RangeAsync(Guid id, CancellationToken ct) => await store.ReadRangeAsync(id, ct) ?? throw new EntityNotFound("Range");

    public async Task<PhoneResponse> PhoneAsync(Guid id, CancellationToken ct) => await store.ReadPhoneAsync(id, ct) ?? throw new EntityNotFound("Phone number");

    public async Task<ReservationResponse> ReservationAsync(Guid id, CancellationToken ct) => await store.ReadReservationAsync(id, ct) ?? throw new EntityNotFound("Reservation");

    public Task<RangeResponse> CreateRangeAsync(RangeInput input, CancellationToken ct) => store.WriteAsync(async token =>
    {
        var range = new NumberRange(input.ToDefinition());
        if (await store.OverlapsAsync(range.RangeStart, range.RangeEnd, token))
        {
            throw new Conflict("The range overlaps an existing range.");
        }

        store.Add(range, range.CreateNumbers());
        return RangeResponse.FromEntity(range);
    }, ct);

    public Task<RangeResponse> UpdateRangeAsync(Guid id, RangeInput input, CancellationToken ct, Guid? expectedVersion = null) => store.WriteAsync(async token =>
    {
        var range = await store.FindRangeAsync(id, token) ?? throw new EntityNotFound("Range");
        if (expectedVersion is not null && range.Version != expectedVersion)
        {
            throw new Conflict("Record changed during PATCH. Reload and retry.");
        }

        range.Update(input.ToDefinition());
        return RangeResponse.FromEntity(range);
    }, ct);

    public Task<bool> DeleteRangeAsync(Guid id, CancellationToken ct) => store.WriteAsync(async token =>
    {
        store.Remove(await store.FindRangeAsync(id, token) ?? throw new EntityNotFound("Range"));
        return true;
    }, ct);

    public async Task<IReadOnlyList<PhoneResponse>> NextAvailableAsync(Guid rangeId, int amount, CancellationToken ct)
    {
        if (amount is < 1 or > 1000)
        {
            throw new RuleViolation("Amount must be between 1 and 1000.");
        }

        if (!(await RangeAsync(rangeId, ct)).Active)
        {
            throw new Conflict("Range is inactive.");
        }

        return await store.AvailableAsync(rangeId, amount, ct);
    }

    public Task<PhoneResponse> UpdatePhoneAsync(Guid id, PhoneInput input, CancellationToken ct, Guid? expectedVersion = null) => store.WriteAsync(async token =>
    {
        var phone = await store.FindPhoneAsync(id, token) ?? throw new EntityNotFound("Phone number");
        if (expectedVersion is not null && phone.Version != expectedVersion)
        {
            throw new Conflict("Record changed during PATCH. Reload and retry.");
        }

        if (phone.LineUri != input.LineUri || phone.RangeId != input.RangeId)
        {
            throw new Conflict("Number and range membership are immutable.");
        }

        phone.Update(input.AssignedUser, input.Status, input.ScheduledUnassignment, await store.ReservationForPhoneAsync(id, token) is not null);
        return PhoneResponse.FromEntity(phone);
    }, ct);

    public Task<ReservationResponse> CreateReservationAsync(ReservationInput input, CancellationToken ct) => store.WriteAsync(async token =>
    {
        var phone = await store.FindPhoneAsync(input.PhoneNumberId, token) ?? throw new EntityNotFound("Phone number");
        if (!(await RangeAsync(phone.RangeId, token)).Active)
        {
            throw new Conflict("Range is inactive.");
        }

        if (await store.ReservationForPhoneAsync(phone.Id, token) is not null)
        {
            throw new Conflict("Phone number is already reserved.");
        }

        var reservation = new Reservation(phone.Id, input.ToDetails(), clock.GetUtcNow());
        phone.Reserve();
        store.Add(reservation);
        return ReservationResponse.FromEntity(reservation);
    }, ct);

    public Task<ReservationResponse> UpdateReservationAsync(Guid id, ReservationInput input, CancellationToken ct, Guid? expectedVersion = null) => store.WriteAsync(async token =>
    {
        var reservation = await store.FindReservationAsync(id, token) ?? throw new EntityNotFound("Reservation");
        if (expectedVersion is not null && reservation.Version != expectedVersion)
        {
            throw new Conflict("Record changed during PATCH. Reload and retry.");
        }

        if (reservation.Kind == ReservationKind.Offboarding)
        {
            throw new Conflict("Offboarding holds are managed by the synchronization job.");
        }

        if (reservation.PhoneNumberId != input.PhoneNumberId)
        {
            throw new Conflict("Reservation cannot be moved to another phone number.");
        }

        reservation.Update(input.ToDetails(), clock.GetUtcNow());
        return ReservationResponse.FromEntity(reservation);
    }, ct);

    public Task<bool> DeleteReservationAsync(Guid id, CancellationToken ct) => store.WriteAsync(async token =>
    {
        var reservation = await store.FindReservationAsync(id, token) ?? throw new EntityNotFound("Reservation");
        if (reservation.Kind == ReservationKind.Offboarding)
        {
            throw new Conflict("Offboarding holds are managed by the synchronization job.");
        }

        (await store.FindPhoneAsync(reservation.PhoneNumberId, token) ?? throw new EntityNotFound("Phone number")).ReservationEnded();
        store.Remove(reservation);
        return true;
    }, ct);

    private static void ValidatePage(int offset, int limit)
    {
        if (offset < 0 || limit is < 1 or > 1000)
        {
            throw new RuleViolation("Offset must be non-negative and limit between 1 and 1000.");
        }
    }
}
