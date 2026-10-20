using PhoneNumbersManagement.Domain;

namespace PhoneNumbersManagement.Application;

public sealed record ReservationQuery(string? ReservedFor = null, ReservationSystem? System = null, string? TicketNumber = null, int Offset = 0, int Limit = 100);
