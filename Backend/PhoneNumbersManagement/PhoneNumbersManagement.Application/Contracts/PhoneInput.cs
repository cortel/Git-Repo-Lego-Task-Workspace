using PhoneNumbersManagement.Domain;

namespace PhoneNumbersManagement.Application;

public sealed record PhoneInput(string LineUri, Guid RangeId, Guid? AssignedUser, DateTimeOffset? ScheduledUnassignment, PhoneStatus Status);
