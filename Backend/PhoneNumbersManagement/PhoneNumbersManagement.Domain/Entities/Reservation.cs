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

    private Reservation()
    {
    }

    public Reservation(Guid phoneNumberId, ReservationDetails details, DateTimeOffset now,
        ReservationKind kind = ReservationKind.Standard)
    {
        PhoneNumberId = phoneNumberId;
        Kind = kind;
        Update(details, now);
    }

    public void Update(ReservationDetails details, DateTimeOffset now)
    {
        if (details.ReservedFor is not ("user" or "system" or "external"))
        {
            throw new RuleViolation("ReservedFor must be user, system, or external; never a name or email.");
        }

        if (!Enum.IsDefined(details.System) || details.Reason?.Length > 2000 || details.TicketNumber?.Length > 200)
        {
            throw new RuleViolation("Invalid reservation system or field length.");
        }

        if (details.Expires is { } expires && (expires <= now || expires.Offset != TimeSpan.Zero))
        {
            throw new RuleViolation("Expiry must be a future UTC timestamp, or null for no expiry.");
        }

        ReservedFor = details.ReservedFor;
        Reason = details.Reason;
        System = details.System;
        Expires = details.Expires;
        TicketNumber = details.TicketNumber;
        Version = Guid.NewGuid();
    }
}
