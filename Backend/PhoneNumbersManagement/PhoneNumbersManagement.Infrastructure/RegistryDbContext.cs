using Microsoft.EntityFrameworkCore;
using PhoneNumbersManagement.Domain;

namespace PhoneNumbersManagement.Infrastructure;

public sealed class RegistryDbContext(DbContextOptions<RegistryDbContext> options) : DbContext(options)
{
    public DbSet<NumberRange> Ranges => Set<NumberRange>();
    public DbSet<PhoneNumber> PhoneNumbers => Set<PhoneNumber>();
    public DbSet<Reservation> Reservations => Set<Reservation>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        base.OnModelCreating(model);
        model.ApplyConfigurationsFromAssembly(typeof(RegistryDbContext).Assembly);
    }
}
