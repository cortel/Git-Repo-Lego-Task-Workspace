namespace PhoneNumbersManagement.Tests;

internal sealed class AdvancingTimeProvider : TimeProvider
{
    private DateTimeOffset now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow()
    {
        var result = now;
        now = now.AddSeconds(31);
        return result;
    }
}
