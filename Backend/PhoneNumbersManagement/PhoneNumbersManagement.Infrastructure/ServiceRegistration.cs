using Azure.Core;
using Azure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PhoneNumbersManagement.Application;
namespace PhoneNumbersManagement.Infrastructure;

public static class ServiceRegistration
{
    public static IServiceCollection AddRegistry(this IServiceCollection services, IConfiguration configuration)
    {
        var connection = configuration.GetConnectionString("Registry");
        if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("ConnectionStrings:Registry must be configured.");
        services.AddDbContext<RegistryDbContext>(options => options.UseNpgsql(connection));
        services.AddScoped<IRegistryStore, EfRegistryStore>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<RegistryService>();
        return services;
    }
    public static IServiceCollection AddTeamsSynchronization(this IServiceCollection services)
    {
        // Managed identity in Azure; AZURE_TENANT_ID / CLIENT_ID / CLIENT_SECRET or certificate locally.
        services.AddSingleton<TokenCredential>(_ => new DefaultAzureCredential());
        services.AddHttpClient<ITeamsDirectory, GraphTeamsDirectory>(client => client.Timeout = TimeSpan.FromSeconds(60));
        services.AddScoped<ReconciliationService>();
        return services;
    }
}