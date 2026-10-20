using System.Linq.Expressions;
using PhoneNumbersManagement.Domain;

namespace PhoneNumbersManagement.Application;

public sealed record RangeResponse
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required string? Description { get; init; }

    public required string RangeStart { get; init; }

    public required string RangeEnd { get; init; }

    public required string? MainLine { get; init; }

    public required bool Active { get; init; }

    public required string Provider { get; init; }

    public required RangeType Type { get; init; }

    public required string? VoiceRoute { get; init; }

    public required string? NetboxSiteId { get; init; }

    public required Guid Version { get; init; }

    public static Expression<Func<NumberRange, RangeResponse>> Projection { get; } = entity => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        Description = entity.Description,
        RangeStart = entity.RangeStart,
        RangeEnd = entity.RangeEnd,
        MainLine = entity.MainLine,
        Active = entity.Active,
        Provider = entity.Provider,
        Type = entity.Type,
        VoiceRoute = entity.VoiceRoute,
        NetboxSiteId = entity.NetboxSiteId,
        Version = entity.Version
    };

    private static readonly Func<NumberRange, RangeResponse> Map = Projection.Compile();

    public static RangeResponse FromEntity(NumberRange entity) => Map(entity);
}
