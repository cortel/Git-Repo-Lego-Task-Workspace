using PhoneNumbersManagement.Domain;

namespace PhoneNumbersManagement.Application;

public sealed record RangeInput
{
    public string Name { get; init; } = "";

    public string? Description { get; init; }

    public string RangeStart { get; init; } = "";

    public string RangeEnd { get; init; } = "";

    public string? MainLine { get; init; }

    public bool Active { get; init; }

    public string Provider { get; init; } = "";

    public RangeType Type { get; init; }

    public string? VoiceRoute { get; init; }

    public string? NetboxSiteId { get; init; }
}
