using PhoneNumbersManagement.Domain;

namespace PhoneNumbersManagement.Application;

public sealed record ReservationInput
{
    public string ReservedFor { get; init; } = "";

    public string? Reason { get; init; }

    public Guid PhoneNumberId { get; init; }

    public ReservationSystem System { get; init; }

    public DateTimeOffset? Expires { get; init; }

    public string? TicketNumber { get; init; }
}
