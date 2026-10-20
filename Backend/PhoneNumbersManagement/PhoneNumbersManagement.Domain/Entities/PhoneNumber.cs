namespace PhoneNumbersManagement.Domain;

public sealed class PhoneNumber
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string LineUri { get; private set; } = "";
    public long NumericValue { get; private set; }
    public Guid RangeId { get; private set; }
    public Guid? AssignedUser { get; private set; }
    public DateTimeOffset? ScheduledUnassignment { get; private set; }
    public PhoneStatus Status { get; private set; }
    public Guid Version { get; private set; } = Guid.NewGuid();
    private PhoneNumber() { }
    public PhoneNumber(Guid rangeId, string number)
    { RangeId = rangeId; LineUri = E164.Validate(number); NumericValue = E164.Numeric(number); }
    public void Reserve()
    {
        if (Status != PhoneStatus.Available || AssignedUser is not null) throw new Conflict("Phone number is not available.");
        Status = PhoneStatus.Reserved; Touch();
    }
    public void Assign(Guid user)
    {
        if (user == Guid.Empty) throw new RuleViolation("Assigned user must be an Entra object ID.");
        if (AssignedUser == user && ScheduledUnassignment is null && Status == PhoneStatus.InUse) return;
        AssignedUser = user; ScheduledUnassignment = null; Status = PhoneStatus.InUse; Touch();
    }
    public void StartOffboarding(DateTimeOffset until)
    {
        if (AssignedUser is null) throw new Conflict("Only an assigned number can enter offboarding.");
        ScheduledUnassignment ??= until; Status = PhoneStatus.Reserved; Touch();
    }
    public void Release(bool hasReservation)
    {
        if (hasReservation) throw new Conflict("An active reservation still exists.");
        AssignedUser = null; ScheduledUnassignment = null; Status = PhoneStatus.Available; Touch();
    }
    public void ReservationEnded()
    {
        Status = AssignedUser is null ? PhoneStatus.Available : PhoneStatus.InUse;
        ScheduledUnassignment = null; Touch();
    }
    public void Update(Guid? user, PhoneStatus status, DateTimeOffset? scheduled, bool hasReservation)
    {
        if (!Enum.IsDefined(status) || user == Guid.Empty) throw new RuleViolation("Invalid phone status or assigned user.");
        if (scheduled != ScheduledUnassignment) throw new Conflict("Scheduled unassignment is managed by offboarding jobs.");
        if (hasReservation) throw new Conflict("Manage or remove the reservation before updating this phone number.");
        if (status == PhoneStatus.Reserved || (status == PhoneStatus.InUse && user is null) ||
            (user is not null && status != PhoneStatus.InUse))
            throw new RuleViolation("Reserved status requires a reservation; InUse requires an assigned user.");
        AssignedUser = user; Status = status; Touch();
    }
    private void Touch() => Version = Guid.NewGuid();
}
