namespace PhoneNumbersManagement.Domain;

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
