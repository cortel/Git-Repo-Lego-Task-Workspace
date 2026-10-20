using PhoneNumbersManagement.Domain;

namespace PhoneNumbersManagement.Application;

public interface IRegistryStore
{
    Task<T> WriteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct);
    Task<NumberRange?> FindRangeAsync(Guid id, CancellationToken ct);
    Task<PhoneNumber?> FindPhoneAsync(Guid id, CancellationToken ct);
    Task<Reservation?> FindReservationAsync(Guid id, CancellationToken ct);
    Task<Reservation?> ReservationForPhoneAsync(Guid id, CancellationToken ct);
    Task<Page<NumberRange>> RangesAsync(RangeQuery query, CancellationToken ct);
    Task<Page<PhoneNumber>> PhonesAsync(PhoneQuery query, CancellationToken ct);
    Task<Page<Reservation>> ReservationsAsync(ReservationQuery query, CancellationToken ct);
    Task<IReadOnlyList<PhoneNumber>> AvailableAsync(Guid rangeId, int amount, CancellationToken ct);
    Task<bool> OverlapsAsync(string start, string end, CancellationToken ct);
    Task<IReadOnlyList<Reservation>> ExpiredAsync(DateTimeOffset now, int limit, CancellationToken ct);
    Task<PhoneNumber?> FindPhoneByNumberAsync(string number, CancellationToken ct);
    void Add(NumberRange range, IEnumerable<PhoneNumber> numbers);
    void Add(Reservation reservation);
    void Remove(NumberRange range);
    void Remove(Reservation reservation);
}
