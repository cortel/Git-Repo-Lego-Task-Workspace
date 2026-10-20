using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PhoneNumbersManagement.Infrastructure;

public sealed class RegistryDbContextFactory : IDesignTimeDbContextFactory<RegistryDbContext>
{
    public RegistryDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Registry")
            ?? "Host=localhost;Database=phone_registry;Username=postgres";
        return new(new DbContextOptionsBuilder<RegistryDbContext>().UseNpgsql(connection).Options);
    }
}
