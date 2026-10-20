using System.Linq.Expressions;
using PhoneNumbersManagement.Domain;

namespace PhoneNumbersManagement.Application;

public sealed record PhoneResponse
{
    public required Guid Id { get; init; }

    public required string LineUri { get; init; }

    public required long NumericValue { get; init; }

    public required Guid RangeId { get; init; }

    public required Guid? AssignedUser { get; init; }

    public required DateTimeOffset? ScheduledUnassignment { get; init; }

    public required PhoneStatus Status { get; init; }

    public required Guid Version { get; init; }

    public static Expression<Func<PhoneNumber, PhoneResponse>> Projection { get; } = entity => new()
    {
        Id = entity.Id,
        LineUri = entity.LineUri,
        NumericValue = entity.NumericValue,
        RangeId = entity.RangeId,
        AssignedUser = entity.AssignedUser,
        ScheduledUnassignment = entity.ScheduledUnassignment,
        Status = entity.Status,
        Version = entity.Version
    };

    private static readonly Func<PhoneNumber, PhoneResponse> Map = Projection.Compile();

    public static PhoneResponse FromEntity(PhoneNumber entity) => Map(entity);
}
