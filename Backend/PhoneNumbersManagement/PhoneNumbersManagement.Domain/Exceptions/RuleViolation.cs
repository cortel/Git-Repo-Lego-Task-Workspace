namespace PhoneNumbersManagement.Domain;

public sealed class RuleViolation(string message) : Exception(message);
