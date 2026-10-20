using PhoneNumbersManagement.Domain;

namespace PhoneNumbersManagement.Application;

public static class RegistryMappings
{
    public static NumberRangeDefinition ToDefinition(this RangeInput input) => new()
    {
        Name = input.Name,
        Description = input.Description,
        RangeStart = input.RangeStart,
        RangeEnd = input.RangeEnd,
        MainLine = input.MainLine,
        Active = input.Active,
        Provider = input.Provider,
        Type = input.Type,
        VoiceRoute = input.VoiceRoute,
        NetboxSiteId = input.NetboxSiteId
    };

    public static ReservationDetails ToDetails(this ReservationInput input) => new()
    {
        ReservedFor = input.ReservedFor,
        Reason = input.Reason,
        System = input.System,
        Expires = input.Expires,
        TicketNumber = input.TicketNumber
    };

    public static RangeInput ToInput(this RangeResponse range) => new()
    {
        Name = range.Name,
        Description = range.Description,
        RangeStart = range.RangeStart,
        RangeEnd = range.RangeEnd,
        MainLine = range.MainLine,
        Active = range.Active,
        Provider = range.Provider,
        Type = range.Type,
        VoiceRoute = range.VoiceRoute,
        NetboxSiteId = range.NetboxSiteId
    };

    public static PhoneInput ToInput(this PhoneResponse phone) => new()
    {
        LineUri = phone.LineUri,
        RangeId = phone.RangeId,
        AssignedUser = phone.AssignedUser,
        ScheduledUnassignment = phone.ScheduledUnassignment,
        Status = phone.Status
    };

    public static ReservationInput ToInput(this ReservationResponse reservation) => new()
    {
        ReservedFor = reservation.ReservedFor,
        Reason = reservation.Reason,
        PhoneNumberId = reservation.PhoneNumberId,
        System = reservation.System,
        Expires = reservation.Expires,
        TicketNumber = reservation.TicketNumber
    };
}
