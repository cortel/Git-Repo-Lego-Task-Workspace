using PhoneNumbersManagement.Domain;

namespace PhoneNumbersManagement.Application;

public sealed record RangeQuery(RangeType? Type = null, string? NetboxSiteId = null, string? Name = null, bool? Active = null, int Offset = 0, int Limit = 100);
