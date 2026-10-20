namespace PhoneNumbersManagement.Domain;

public sealed class EntityNotFound(string entity) : Exception($"{entity} was not found.");
