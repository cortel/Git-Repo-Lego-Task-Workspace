using PhoneNumbersManagement.Domain;
namespace PhoneNumbersManagement.Application;

public sealed record RangeInput(string Name, string? Description, string RangeStart, string RangeEnd,
    string? MainLine, bool Active, string Provider, RangeType Type, string? VoiceRoute, string? NetboxSiteId);
public sealed record PhoneInput(string LineUri, Guid RangeId, Guid? AssignedUser, DateTimeOffset? ScheduledUnassignment, PhoneStatus Status);
public sealed record ReservationInput(string ReservedFor, string? Reason, Guid PhoneNumberId,
    ReservationSystem System, DateTimeOffset? Expires, string? TicketNumber);
public sealed record Page<T>(IReadOnlyList<T> Items, int Offset, int Limit);
public sealed record RangeQuery(RangeType? Type = null, string? NetboxSiteId = null, string? Name = null, bool? Active = null, int Offset = 0, int Limit = 100);
public sealed record PhoneQuery(Guid? RangeId = null, Guid? AssignedUser = null, PhoneStatus? Status = null, int Offset = 0, int Limit = 100);
public sealed record ReservationQuery(string? ReservedFor = null, ReservationSystem? System = null, string? TicketNumber = null, int Offset = 0, int Limit = 100);
public sealed record TeamsAssignment(string Number, Guid UserId);
public enum DirectoryUserState { Exists, Missing }
public interface ITeamsDirectory
{
    Task<IReadOnlyList<TeamsAssignment>> GetAssignmentsAsync(CancellationToken ct);
    // Missing must mean an authoritative 404, never a timeout, permission failure, or incomplete page.
    Task<DirectoryUserState> GetUserStateAsync(Guid userId, CancellationToken ct);
}
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