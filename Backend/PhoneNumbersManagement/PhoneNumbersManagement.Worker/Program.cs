using Hangfire;
using Hangfire.PostgreSql;
using PhoneNumbersManagement.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddRegistry(builder.Configuration).AddTeamsSynchronization();
var configuredHangfire = builder.Configuration.GetConnectionString("Hangfire");
var connection = string.IsNullOrWhiteSpace(configuredHangfire)
    ? builder.Configuration.GetConnectionString("Registry")!
    : configuredHangfire;
builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer().UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(connection)));
builder.Services.AddHangfireServer();
using var host = builder.Build();
var jobs = host.Services.GetRequiredService<IRecurringJobManager>();
var options = new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc };
jobs.AddOrUpdate<MicrosoftTeamsIntegrationJob>("teams-synchronization",
    job => job.SynchronizeUsedPhoneNumbers(CancellationToken.None),
    builder.Configuration["Jobs:TeamsCron"] ?? "*/15 * * * *", options);
jobs.AddOrUpdate<CleanupReservationsJob>("reservation-cleanup",
    job => job.CleanupExpiredReservations(CancellationToken.None),
    builder.Configuration["Jobs:CleanupCron"] ?? "*/5 * * * *", options);
await host.RunAsync();
