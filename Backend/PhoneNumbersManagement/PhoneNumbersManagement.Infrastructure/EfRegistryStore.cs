using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PhoneNumbersManagement.Application;
using PhoneNumbersManagement.Domain;
namespace PhoneNumbersManagement.Infrastructure;

public sealed class EfRegistryStore(RegistryDbContext db) : IRegistryStore
{
    public async Task<T> WriteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        // A job reuses its scope. Never let an earlier tracked snapshot hide a concurrent update.
        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var result = await action(ct);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return result;
        }
        catch (DbUpdateConcurrencyException) { throw new Conflict("Record changed concurrently. Reload and retry."); }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: "23505" or "23503" or "40001" or "40P01" })
        { throw new Conflict("A conflicting number, reservation, or range change occurred. Reload and retry."); }
        catch (PostgresException e) when (e.SqlState is "40001" or "40P01")
        { throw new Conflict("Concurrent transaction conflict. Reload and retry."); }
        finally { db.ChangeTracker.Clear(); }
    }
    public Task<NumberRange?> FindRangeAsync(Guid id, CancellationToken ct) => db.Ranges.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<PhoneNumber?> FindPhoneAsync(Guid id, CancellationToken ct) => db.PhoneNumbers.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<PhoneNumber?> FindPhoneByNumberAsync(string number, CancellationToken ct) => db.PhoneNumbers.SingleOrDefaultAsync(x => x.LineUri == number, ct);
    public Task<Reservation?> FindReservationAsync(Guid id, CancellationToken ct) => db.Reservations.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<Reservation?> ReservationForPhoneAsync(Guid id, CancellationToken ct) => db.Reservations.SingleOrDefaultAsync(x => x.PhoneNumberId == id, ct);
    public async Task<Page<NumberRange>> RangesAsync(RangeQuery q, CancellationToken ct)
    {
        var query = db.Ranges.AsNoTracking();
        if (q.Type is not null) query = query.Where(x => x.Type == q.Type);
        if (q.NetboxSiteId is not null) query = query.Where(x => x.NetboxSiteId == q.NetboxSiteId);
        if (q.Name is not null) query = query.Where(x => x.Name.Contains(q.Name));
        if (q.Active is not null) query = query.Where(x => x.Active == q.Active);
        return new(await query.OrderBy(x => x.Id).Skip(q.Offset).Take(q.Limit).ToListAsync(ct), q.Offset, q.Limit);
    }
    public async Task<Page<PhoneNumber>> PhonesAsync(PhoneQuery q, CancellationToken ct)
    {
        var query = db.PhoneNumbers.AsNoTracking();
        if (q.RangeId is not null) query = query.Where(x => x.RangeId == q.RangeId);
        if (q.AssignedUser is not null) query = query.Where(x => x.AssignedUser == q.AssignedUser);
        if (q.Status is not null) query = query.Where(x => x.Status == q.Status);
        return new(await query.OrderBy(x => x.NumericValue).ThenBy(x => x.Id).Skip(q.Offset).Take(q.Limit).ToListAsync(ct), q.Offset, q.Limit);
    }
    public async Task<Page<Reservation>> ReservationsAsync(ReservationQuery q, CancellationToken ct)
    {
        var query = db.Reservations.AsNoTracking();
        if (q.ReservedFor is not null) query = query.Where(x => x.ReservedFor == q.ReservedFor);
        if (q.System is not null) query = query.Where(x => x.System == q.System);
        if (q.TicketNumber is not null) query = query.Where(x => x.TicketNumber == q.TicketNumber);
        return new(await query.OrderBy(x => x.Id).Skip(q.Offset).Take(q.Limit).ToListAsync(ct), q.Offset, q.Limit);
    }
    public async Task<IReadOnlyList<PhoneNumber>> AvailableAsync(Guid rangeId, int amount, CancellationToken ct) =>
        await db.PhoneNumbers.AsNoTracking().Where(x => x.RangeId == rangeId && x.Status == PhoneStatus.Available && x.AssignedUser == null &&
            !db.Reservations.Any(r => r.PhoneNumberId == x.Id)).OrderBy(x => x.NumericValue).Take(amount).ToListAsync(ct);
    public async Task<bool> OverlapsAsync(string start, string end, CancellationToken ct)
    {
        // Bounded ranges are materialized. Unique phone numbers are the final overlapping-range constraint.
        var first = E164.Numeric(start); var last = E164.Numeric(end);
        return await db.PhoneNumbers.AnyAsync(x => x.NumericValue >= first && x.NumericValue <= last, ct);
    }
    public async Task<IReadOnlyList<Reservation>> ExpiredAsync(DateTimeOffset now, int limit, CancellationToken ct) =>
        await db.Reservations.AsNoTracking().Where(x => x.Expires != null && x.Expires <= now).OrderBy(x => x.Expires).Take(limit).ToListAsync(ct);
    public void Add(NumberRange range, IEnumerable<PhoneNumber> numbers) { db.Ranges.Add(range); db.PhoneNumbers.AddRange(numbers); }
    public void Add(Reservation reservation) => db.Reservations.Add(reservation);
    public void Remove(NumberRange range) => db.Ranges.Remove(range);
    public void Remove(Reservation reservation) => db.Reservations.Remove(reservation);
}