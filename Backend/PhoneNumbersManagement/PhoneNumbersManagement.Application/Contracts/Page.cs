using PhoneNumbersManagement.Domain;

namespace PhoneNumbersManagement.Application;

public sealed record Page<T>(IReadOnlyList<T> Items, int Offset, int Limit);
