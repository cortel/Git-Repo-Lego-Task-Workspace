using System.Linq.Expressions;
using PhoneNumbersManagement.Domain;

namespace PhoneNumbersManagement.Application;

public sealed record ReservationResponse
{
    public required Guid Id { get; init; }

    public required string ReservedFor { get; init; }

    public required string? Reason { get; init; }

    public required Guid PhoneNumberId { get; init; }

    public required ReservationSystem System { get; init; }

    public required DateTimeOffset? Expires { get; init; }

    public required string? TicketNumber { get; init; }

    public required ReservationKind Kind { get; init; }

    public required Guid Version { get; init; }

    public static Expression<Func<Reservation, ReservationResponse>> Projection { get; } = entity => new()
    {
        Id = entity.Id,
        ReservedFor = entity.ReservedFor,
        Reason = entity.Reason,
        PhoneNumberId = entity.PhoneNumberId,
        System = entity.System,
        Expires = entity.Expires,
        TicketNumber = entity.TicketNumber,
        Kind = entity.Kind,
        Version = entity.Version
    };

    private static readonly Func<Reservation, ReservationResponse> Map = Projection.Compile();

    public static ReservationResponse FromEntity(Reservation entity) => Map(entity);
}
