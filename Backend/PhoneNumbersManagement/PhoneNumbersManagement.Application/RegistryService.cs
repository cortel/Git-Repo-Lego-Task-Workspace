using PhoneNumbersManagement.Domain;
namespace PhoneNumbersManagement.Application;

public sealed class RegistryService(IRegistryStore store, TimeProvider clock)
{
    public Task<Page<NumberRange>> RangesAsync(RangeQuery q, CancellationToken ct) { ValidatePage(q.Offset, q.Limit); return store.RangesAsync(q, ct); }
    public Task<Page<PhoneNumber>> PhonesAsync(PhoneQuery q, CancellationToken ct) { ValidatePage(q.Offset, q.Limit); return store.PhonesAsync(q, ct); }
    public Task<Page<Reservation>> ReservationsAsync(ReservationQuery q, CancellationToken ct) { ValidatePage(q.Offset, q.Limit); return store.ReservationsAsync(q, ct); }
    public async Task<NumberRange> RangeAsync(Guid id, CancellationToken ct) => await store.FindRangeAsync(id, ct) ?? throw new EntityNotFound("Range");
    public async Task<PhoneNumber> PhoneAsync(Guid id, CancellationToken ct) => await store.FindPhoneAsync(id, ct) ?? throw new EntityNotFound("Phone number");
    public async Task<Reservation> ReservationAsync(Guid id, CancellationToken ct) => await store.FindReservationAsync(id, ct) ?? throw new EntityNotFound("Reservation");
    public Task<NumberRange> CreateRangeAsync(RangeInput input, CancellationToken ct) => store.WriteAsync(async token =>
    {
        var range = new NumberRange(input.Name, input.Description, input.RangeStart, input.RangeEnd,
            input.MainLine, input.Active, input.Provider, input.Type, input.VoiceRoute, input.NetboxSiteId);
        if (await store.OverlapsAsync(range.RangeStart, range.RangeEnd, token)) throw new Conflict("The range overlaps an existing range.");
        store.Add(range, range.CreateNumbers()); return range;
    }, ct);
    public Task<NumberRange> UpdateRangeAsync(Guid id, RangeInput input, CancellationToken ct, Guid? expectedVersion = null) => store.WriteAsync(async token =>
    {
        var range = await RangeAsync(id, token);
        if (expectedVersion is not null && range.Version != expectedVersion) throw new Conflict("Record changed during PATCH. Reload and retry.");
        if (range.RangeStart != input.RangeStart || range.RangeEnd != input.RangeEnd)
            throw new Conflict("Range boundaries are immutable. Create a replacement range through an agreed migration.");
        range.Update(input.Name, input.Description, input.MainLine, input.Active, input.Provider, input.Type, input.VoiceRoute, input.NetboxSiteId);
        return range;
    }, ct);
    public Task<bool> DeleteRangeAsync(Guid id, CancellationToken ct) => store.WriteAsync(async token =>
    { store.Remove(await RangeAsync(id, token)); return true; }, ct);
    public async Task<IReadOnlyList<PhoneNumber>> NextAvailableAsync(Guid rangeId, int amount, CancellationToken ct)
    {
        if (amount is < 1 or > 1000) throw new RuleViolation("Amount must be between 1 and 1000.");
        if (!(await RangeAsync(rangeId, ct)).Active) throw new Conflict("Range is inactive.");
        return await store.AvailableAsync(rangeId, amount, ct);
    }
    public Task<PhoneNumber> UpdatePhoneAsync(Guid id, PhoneInput input, CancellationToken ct, Guid? expectedVersion = null) => store.WriteAsync(async token =>
    {
        var phone = await PhoneAsync(id, token);
        if (expectedVersion is not null && phone.Version != expectedVersion) throw new Conflict("Record changed during PATCH. Reload and retry.");
        if (phone.LineUri != input.LineUri || phone.RangeId != input.RangeId) throw new Conflict("Number and range membership are immutable.");
        phone.Update(input.AssignedUser, input.Status, input.ScheduledUnassignment, await store.ReservationForPhoneAsync(id, token) is not null);
        return phone;
    }, ct);
    public Task<Reservation> CreateReservationAsync(ReservationInput input, CancellationToken ct) => store.WriteAsync(async token =>
    {
        var phone = await PhoneAsync(input.PhoneNumberId, token);
        if (!(await RangeAsync(phone.RangeId, token)).Active) throw new Conflict("Range is inactive.");
        if (await store.ReservationForPhoneAsync(phone.Id, token) is not null) throw new Conflict("Phone number is already reserved.");
        var reservation = new Reservation(phone.Id, input.ReservedFor, input.Reason, input.System, input.Expires, input.TicketNumber, clock.GetUtcNow());
        phone.Reserve(); store.Add(reservation); return reservation;
    }, ct);
    public Task<Reservation> UpdateReservationAsync(Guid id, ReservationInput input, CancellationToken ct, Guid? expectedVersion = null) => store.WriteAsync(async token =>
    {
        var reservation = await ReservationAsync(id, token);
        if (expectedVersion is not null && reservation.Version != expectedVersion) throw new Conflict("Record changed during PATCH. Reload and retry.");
        if (reservation.Kind == ReservationKind.Offboarding) throw new Conflict("Offboarding holds are managed by the synchronization job.");
        if (reservation.PhoneNumberId != input.PhoneNumberId) throw new Conflict("Reservation cannot be moved to another phone number.");
        reservation.Update(input.ReservedFor, input.Reason, input.System, input.Expires, input.TicketNumber, clock.GetUtcNow()); return reservation;
    }, ct);
    public Task<bool> DeleteReservationAsync(Guid id, CancellationToken ct) => store.WriteAsync(async token =>
    {
        var reservation = await ReservationAsync(id, token);
        if (reservation.Kind == ReservationKind.Offboarding) throw new Conflict("Offboarding holds are managed by the synchronization job.");
        (await PhoneAsync(reservation.PhoneNumberId, token)).ReservationEnded(); store.Remove(reservation); return true;
    }, ct);
    private static void ValidatePage(int offset, int limit)
    { if (offset < 0 || limit is < 1 or > 1000) throw new RuleViolation("Offset must be non-negative and limit between 1 and 1000."); }
}