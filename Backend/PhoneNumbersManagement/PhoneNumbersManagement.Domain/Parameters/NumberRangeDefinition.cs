namespace PhoneNumbersManagement.Domain;

public sealed record NumberRangeDefinition
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
