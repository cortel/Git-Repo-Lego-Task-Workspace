using PhoneNumbersManagement.Domain;

namespace PhoneNumbersManagement.Application;

public sealed record PhoneSyncState
{
    public required string LineUri { get; init; }

    public Guid? AssignedUser { get; init; }

    public PhoneStatus Status { get; init; }

    public DateTimeOffset? ScheduledUnassignment { get; init; }

    public bool HasReservation { get; init; }

    public bool MatchesAssignment(Guid userId) => AssignedUser == userId && Status == PhoneStatus.InUse &&
        ScheduledUnassignment is null && !HasReservation;
}
