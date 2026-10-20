using PhoneNumbersManagement.Domain;

namespace PhoneNumbersManagement.Application;

public sealed record PhoneInput
{
    public string LineUri { get; init; } = "";

    public Guid RangeId { get; init; }

    public Guid? AssignedUser { get; init; }

    public DateTimeOffset? ScheduledUnassignment { get; init; }

    public PhoneStatus Status { get; init; }
}
