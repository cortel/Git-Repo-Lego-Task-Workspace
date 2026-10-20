using System.Globalization;
using System.Text.RegularExpressions;

namespace PhoneNumbersManagement.Domain;

public static partial class E164
{
    [GeneratedRegex(@"^\+[1-9][0-9]{1,14}$")]
    private static partial Regex Pattern();
    public static string Validate(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !Pattern().IsMatch(value))
            throw new RuleViolation("Phone numbers must use E.164 format: + followed by 2 to 15 digits.");
        return value;
    }
    public static long Numeric(string value) => long.Parse(Validate(value).AsSpan(1), CultureInfo.InvariantCulture);
    public static string Format(long value) => "+" + value.ToString(CultureInfo.InvariantCulture);
}
