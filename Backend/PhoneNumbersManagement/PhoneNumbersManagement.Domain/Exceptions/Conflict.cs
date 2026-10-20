namespace PhoneNumbersManagement.Domain;

public sealed class Conflict(string message) : Exception(message);
