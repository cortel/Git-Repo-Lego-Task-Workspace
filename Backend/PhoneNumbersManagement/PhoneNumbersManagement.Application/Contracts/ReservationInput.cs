using PhoneNumbersManagement.Domain;

namespace PhoneNumbersManagement.Application;

public sealed record ReservationInput(string ReservedFor, string? Reason, Guid PhoneNumberId,
    ReservationSystem System, DateTimeOffset? Expires, string? TicketNumber);
