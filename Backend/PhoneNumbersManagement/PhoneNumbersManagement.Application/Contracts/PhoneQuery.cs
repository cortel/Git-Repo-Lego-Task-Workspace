using PhoneNumbersManagement.Domain;

namespace PhoneNumbersManagement.Application;

public sealed record PhoneQuery(Guid? RangeId = null, Guid? AssignedUser = null, PhoneStatus? Status = null, int Offset = 0, int Limit = 100);
