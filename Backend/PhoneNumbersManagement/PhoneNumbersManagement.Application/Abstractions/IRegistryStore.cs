using PhoneNumbersManagement.Domain;

namespace PhoneNumbersManagement.Application;

public interface IRegistryStore
{
    Task<T> WriteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct);

    Task<NumberRange?> FindRangeAsync(Guid id, CancellationToken ct);

    Task<PhoneNumber?> FindPhoneAsync(Guid id, CancellationToken ct);

    Task<Reservation?> FindReservationAsync(Guid id, CancellationToken ct);

    Task<Reservation?> ReservationForPhoneAsync(Guid id, CancellationToken ct);

    Task<Page<RangeResponse>> RangesAsync(RangeQuery query, CancellationToken ct);

    Task<Page<PhoneResponse>> PhonesAsync(PhoneQuery query, CancellationToken ct);

    Task<Page<ReservationResponse>> ReservationsAsync(ReservationQuery query, CancellationToken ct);

    Task<IReadOnlyList<PhoneResponse>> AvailableAsync(Guid rangeId, int amount, CancellationToken ct);

    Task<bool> OverlapsAsync(string start, string end, CancellationToken ct);

    Task<IReadOnlyList<Reservation>> ExpiredAsync(DateTimeOffset now, int limit, CancellationToken ct);

    Task<PhoneNumber?> FindPhoneByNumberAsync(string number, CancellationToken ct);

    Task<RangeResponse?> ReadRangeAsync(Guid id, CancellationToken ct);

    Task<PhoneResponse?> ReadPhoneAsync(Guid id, CancellationToken ct);

    Task<ReservationResponse?> ReadReservationAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyDictionary<string, PhoneSyncState>> ManagedPhonesAsync(string[] numbers, CancellationToken ct);

    Task<IReadOnlyList<AssignedPhone>> AssignedPhonesAfterAsync(long afterNumber, int limit, CancellationToken ct);

    void Add(NumberRange range, IEnumerable<PhoneNumber> numbers);

    void Add(Reservation reservation);

    void Remove(NumberRange range);

    void Remove(Reservation reservation);
}
