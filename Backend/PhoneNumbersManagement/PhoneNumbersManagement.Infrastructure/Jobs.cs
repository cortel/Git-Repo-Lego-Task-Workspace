using Hangfire;
using PhoneNumbersManagement.Application;
namespace PhoneNumbersManagement.Infrastructure;

public sealed class MicrosoftTeamsIntegrationJob(ReconciliationService service)
{
    [DisableConcurrentExecution(600)]
    [AutomaticRetry(Attempts = 3)]
    public Task SynchronizeUsedPhoneNumbers(CancellationToken ct) => service.SynchronizeAsync(ct);
}
public sealed class CleanupReservationsJob(ReconciliationService service)
{
    [DisableConcurrentExecution(600)]
    [AutomaticRetry(Attempts = 3)]
    public Task CleanupExpiredReservations(CancellationToken ct) => service.CleanupAsync(ct);
}