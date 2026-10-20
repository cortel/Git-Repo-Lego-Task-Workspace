namespace PhoneNumbersManagement.Domain;

public sealed class NumberRange
{
    public const int MaximumSize = 10000;

    public Guid Id { get; private set; } = Guid.NewGuid();

    public string Name { get; private set; } = "";

    public string? Description { get; private set; }

    public string RangeStart { get; private set; } = "";

    public string RangeEnd { get; private set; } = "";

    public string? MainLine { get; private set; }

    public bool Active { get; private set; }

    public string Provider { get; private set; } = "";

    public RangeType Type { get; private set; }

    public string? VoiceRoute { get; private set; }

    public string? NetboxSiteId { get; private set; }

    public Guid Version { get; private set; } = Guid.NewGuid();

    private NumberRange()
    {
    }

    public NumberRange(NumberRangeDefinition definition)
    {
        RangeStart = E164.Validate(definition.RangeStart);
        RangeEnd = E164.Validate(definition.RangeEnd);
        var start = E164.Numeric(RangeStart);
        var end = E164.Numeric(RangeEnd);
        if (RangeStart.Length != RangeEnd.Length || end < start || end - start >= MaximumSize)
        {
            throw new RuleViolation($"Range endpoints must have equal lengths, be ordered, and contain at most {MaximumSize} numbers.");
        }

        Update(definition);
    }

    public void Update(NumberRangeDefinition definition)
    {
        if (definition.RangeStart != RangeStart || definition.RangeEnd != RangeEnd)
        {
            throw new Conflict("Range boundaries are immutable. Create a replacement range through an agreed migration.");
        }

        if (string.IsNullOrWhiteSpace(definition.Name) || definition.Name.Length > 200 ||
            string.IsNullOrWhiteSpace(definition.Provider) || definition.Provider.Length > 200)
        {
            throw new RuleViolation("Name and provider are required and must not exceed 200 characters.");
        }

        if (!Enum.IsDefined(definition.Type))
        {
            throw new RuleViolation("Invalid range type.");
        }

        if (definition.Description?.Length > 2000 || definition.VoiceRoute?.Length > 500 || definition.NetboxSiteId?.Length > 200)
        {
            throw new RuleViolation("A range field exceeds its maximum length.");
        }

        if (definition.MainLine is not null)
        {
            E164.Validate(definition.MainLine);
        }

        Name = definition.Name;
        Description = definition.Description;
        MainLine = definition.MainLine;
        Active = definition.Active;
        Provider = definition.Provider;
        Type = definition.Type;
        VoiceRoute = definition.VoiceRoute;
        NetboxSiteId = definition.NetboxSiteId;
        Version = Guid.NewGuid();
    }

    public IEnumerable<PhoneNumber> CreateNumbers()
    {
        var end = E164.Numeric(RangeEnd);
        for (var n = E164.Numeric(RangeStart); n <= end; n++)
        {
            yield return new PhoneNumber(Id, E164.Format(n));
        }
    }
}
