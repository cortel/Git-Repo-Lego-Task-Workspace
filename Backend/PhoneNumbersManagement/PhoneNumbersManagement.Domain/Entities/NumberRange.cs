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

    public NumberRange(string name, string? description, string start, string end, string? mainLine,
        bool active, string provider, RangeType type, string? voiceRoute, string? siteId)
    {
        RangeStart = E164.Validate(start);
        RangeEnd = E164.Validate(end);
        if (start.Length != end.Length || E164.Numeric(end) < E164.Numeric(start) ||
            E164.Numeric(end) - E164.Numeric(start) >= MaximumSize)
        {
            throw new RuleViolation($"Range endpoints must have equal lengths, be ordered, and contain at most {MaximumSize} numbers.");
        }

        Update(name, description, mainLine, active, provider, type, voiceRoute, siteId);
    }

    public void Update(string name, string? description, string? mainLine, bool active,
        string provider, RangeType type, string? voiceRoute, string? siteId)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200 || string.IsNullOrWhiteSpace(provider) || provider.Length > 200)
        {
            throw new RuleViolation("Name and provider are required and must not exceed 200 characters.");
        }

        if (!Enum.IsDefined(type))
        {
            throw new RuleViolation("Invalid range type.");
        }

        if (description?.Length > 2000 || voiceRoute?.Length > 500 || siteId?.Length > 200)
        {
            throw new RuleViolation("A range field exceeds its maximum length.");
        }

        if (mainLine is not null)
        {
            E164.Validate(mainLine);
        }

        Name = name;
        Description = description;
        MainLine = mainLine;
        Active = active;
        Provider = provider;
        Type = type;
        VoiceRoute = voiceRoute;
        NetboxSiteId = siteId;
        Version = Guid.NewGuid();
    }

    public IEnumerable<PhoneNumber> CreateNumbers()
    {
        for (var n = E164.Numeric(RangeStart); n <= E164.Numeric(RangeEnd); n++)
        {
            yield return new PhoneNumber(Id, E164.Format(n));
        }
    }
}
