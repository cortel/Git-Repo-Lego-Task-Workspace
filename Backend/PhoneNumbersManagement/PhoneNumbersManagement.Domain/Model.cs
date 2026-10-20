using System.Globalization;
using System.Text.RegularExpressions;

namespace PhoneNumbersManagement.Domain;

public sealed class RuleViolation(string message) : Exception(message);
public sealed class Conflict(string message) : Exception(message);
public sealed class EntityNotFound(string entity) : Exception($"{entity} was not found.");
public enum RangeType { DirectRouting, OperatorConnect, FreePhone }
public enum PhoneStatus { Available, Reserved, InUse, Error }
public enum ReservationSystem { MicrosoftTeams, ContactCenter, Hotline, Other }
public enum ReservationKind { Standard, Offboarding }

public static partial class E164
{
    [GeneratedRegex(@"^\+[1-9][0-9]{1,14}$")]
    private static partial Regex Pattern();
    public static string Validate(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !Pattern().IsMatch(value))
            throw new RuleViolation("Phone numbers must use E.164 format: + followed by 2 to 15 digits.");
        return value;
    }
    public static long Numeric(string value) => long.Parse(Validate(value).AsSpan(1), CultureInfo.InvariantCulture);
    public static string Format(long value) => "+" + value.ToString(CultureInfo.InvariantCulture);
}

public sealed class NumberRange
{
    public const int MaximumSize = 10000;
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; } = "";
    public string? Description { get; private set; }
    public string RangeStart { get; private set; } = "";
    public string RangeEnd { get; private set; } = "";
    public string? MainLine { get; private set; }
    public bool Active { get; private set; }
    public string Provider { get; private set; } = "";
    public RangeType Type { get; private set; }
    public string? VoiceRoute { get; private set; }
    public string? NetboxSiteId { get; private set; }
    public Guid Version { get; private set; } = Guid.NewGuid();
    private NumberRange() { }
    public NumberRange(string name, string? description, string start, string end, string? mainLine,
        bool active, string provider, RangeType type, string? voiceRoute, string? siteId)
    {
        RangeStart = E164.Validate(start); RangeEnd = E164.Validate(end);
        if (start.Length != end.Length || E164.Numeric(end) < E164.Numeric(start) ||
            E164.Numeric(end) - E164.Numeric(start) >= MaximumSize)
            throw new RuleViolation($"Range endpoints must have equal lengths, be ordered, and contain at most {MaximumSize} numbers.");
        Update(name, description, mainLine, active, provider, type, voiceRoute, siteId);
    }
    public void Update(string name, string? description, string? mainLine, bool active,
        string provider, RangeType type, string? voiceRoute, string? siteId)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200 || string.IsNullOrWhiteSpace(provider) || provider.Length > 200)
            throw new RuleViolation("Name and provider are required and must not exceed 200 characters.");
        if (!Enum.IsDefined(type)) throw new RuleViolation("Invalid range type.");
        if (description?.Length > 2000 || voiceRoute?.Length > 500 || siteId?.Length > 200)
            throw new RuleViolation("A range field exceeds its maximum length.");
        if (mainLine is not null) E164.Validate(mainLine);
        Name = name; Description = description; MainLine = mainLine; Active = active;
        Provider = provider; Type = type; VoiceRoute = voiceRoute; NetboxSiteId = siteId;
        Version = Guid.NewGuid();
    }
    public IEnumerable<PhoneNumber> CreateNumbers()
    {
        for (var n = E164.Numeric(RangeStart); n <= E164.Numeric(RangeEnd); n++)
            yield return new PhoneNumber(Id, E164.Format(n));
    }
}

public sealed class PhoneNumber
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string LineUri { get; private set; } = "";
    public long NumericValue { get; private set; }
    public Guid RangeId { get; private set; }
    public Guid? AssignedUser { get; private set; }
    public DateTimeOffset? ScheduledUnassignment { get; private set; }
    public PhoneStatus Status { get; private set; }
    public Guid Version { get; private set; } = Guid.NewGuid();
    private PhoneNumber() { }
    public PhoneNumber(Guid rangeId, string number)
    { RangeId = rangeId; LineUri = E164.Validate(number); NumericValue = E164.Numeric(number); }
    public void Reserve()
    {
        if (Status != PhoneStatus.Available || AssignedUser is not null) throw new Conflict("Phone number is not available.");
        Status = PhoneStatus.Reserved; Touch();
    }
    public void Assign(Guid user)
    {
        if (user == Guid.Empty) throw new RuleViolation("Assigned user must be an Entra object ID.");
        if (AssignedUser == user && ScheduledUnassignment is null && Status == PhoneStatus.InUse) return;
        AssignedUser = user; ScheduledUnassignment = null; Status = PhoneStatus.InUse; Touch();
    }
    public void StartOffboarding(DateTimeOffset until)
    {
        if (AssignedUser is null) throw new Conflict("Only an assigned number can enter offboarding.");
        ScheduledUnassignment ??= until; Status = PhoneStatus.Reserved; Touch();
    }
    public void Release(bool hasReservation)
    {
        if (hasReservation) throw new Conflict("An active reservation still exists.");
        AssignedUser = null; ScheduledUnassignment = null; Status = PhoneStatus.Available; Touch();
    }
    public void ReservationEnded()
    {
        Status = AssignedUser is null ? PhoneStatus.Available : PhoneStatus.InUse;
        ScheduledUnassignment = null; Touch();
    }
    public void Update(Guid? user, PhoneStatus status, DateTimeOffset? scheduled, bool hasReservation)
    {
        if (!Enum.IsDefined(status) || user == Guid.Empty) throw new RuleViolation("Invalid phone status or assigned user.");
        if (scheduled != ScheduledUnassignment) throw new Conflict("Scheduled unassignment is managed by offboarding jobs.");
        if (hasReservation) throw new Conflict("Manage or remove the reservation before updating this phone number.");
        if (status == PhoneStatus.Reserved || (status == PhoneStatus.InUse && user is null) ||
            (user is not null && status != PhoneStatus.InUse))
            throw new RuleViolation("Reserved status requires a reservation; InUse requires an assigned user.");
        AssignedUser = user; Status = status; Touch();
    }
    private void Touch() => Version = Guid.NewGuid();
}

public sealed class Reservation
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string ReservedFor { get; private set; } = "";
    public string? Reason { get; private set; }
    public Guid PhoneNumberId { get; private set; }
    public ReservationSystem System { get; private set; }
    public DateTimeOffset? Expires { get; private set; }
    public string? TicketNumber { get; private set; }
    public ReservationKind Kind { get; private set; }
    public Guid Version { get; private set; } = Guid.NewGuid();
    private Reservation() { }
    public Reservation(Guid phoneNumberId, string reservedFor, string? reason, ReservationSystem system,
        DateTimeOffset? expires, string? ticket, DateTimeOffset now, ReservationKind kind = ReservationKind.Standard)
    {
        PhoneNumberId = phoneNumberId; Kind = kind;
        Update(reservedFor, reason, system, expires, ticket, now);
    }
    public void Update(string reservedFor, string? reason, ReservationSystem system, DateTimeOffset? expires,
        string? ticket, DateTimeOffset now)
    {
        if (reservedFor is not ("user" or "system" or "external")) throw new RuleViolation("ReservedFor must be user, system, or external; never a name or email.");
        if (!Enum.IsDefined(system) || reason?.Length > 2000 || ticket?.Length > 200)
            throw new RuleViolation("Invalid reservation system or field length.");
        if (expires is not null && (expires <= now || expires.Value.Offset != TimeSpan.Zero))
            throw new RuleViolation("Expiry must be a future UTC timestamp, or null for no expiry.");
        ReservedFor = reservedFor; Reason = reason; System = system; Expires = expires; TicketNumber = ticket;
        Version = Guid.NewGuid();
    }
}