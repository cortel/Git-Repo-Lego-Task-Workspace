namespace PhoneNumbersManagement.Application;

public sealed record AssignedPhone(Guid Id, Guid UserId, long NumericValue);
