namespace PhoneNumbersManagement.Domain;

public sealed record ReservationDetails
{
    public string ReservedFor { get; init; } = "";

    public string? Reason { get; init; }

    public ReservationSystem System { get; init; }

    public DateTimeOffset? Expires { get; init; }

    public string? TicketNumber { get; init; }
}
