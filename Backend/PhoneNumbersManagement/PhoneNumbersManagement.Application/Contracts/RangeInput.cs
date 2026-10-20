using PhoneNumbersManagement.Domain;

namespace PhoneNumbersManagement.Application;

public sealed record RangeInput(string Name, string? Description, string RangeStart, string RangeEnd,
    string? MainLine, bool Active, string Provider, RangeType Type, string? VoiceRoute, string? NetboxSiteId);
