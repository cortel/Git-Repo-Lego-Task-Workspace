using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PhoneNumbersManagement.Domain;
namespace PhoneNumbersManagement.Infrastructure;

public sealed class RegistryDbContext(DbContextOptions<RegistryDbContext> options) : DbContext(options)
{
    public DbSet<NumberRange> Ranges => Set<NumberRange>();
    public DbSet<PhoneNumber> PhoneNumbers => Set<PhoneNumber>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        var range = model.Entity<NumberRange>();
        range.ToTable("Ranges"); range.HasKey(x => x.Id);
        range.Property(x => x.Version).IsConcurrencyToken();
        range.Property(x => x.Name).HasMaxLength(200).IsRequired();
        range.Property(x => x.Description).HasMaxLength(2000);
        range.Property(x => x.RangeStart).HasMaxLength(16).IsRequired();
        range.Property(x => x.RangeEnd).HasMaxLength(16).IsRequired();
        range.Property(x => x.MainLine).HasMaxLength(16);
        range.Property(x => x.Provider).HasMaxLength(200).IsRequired();
        range.Property(x => x.Type).HasConversion<string>().HasMaxLength(30);
        range.Property(x => x.VoiceRoute).HasMaxLength(500);
        range.Property(x => x.NetboxSiteId).HasMaxLength(200);
        range.HasIndex(x => new { x.Active, x.NetboxSiteId });
        var phone = model.Entity<PhoneNumber>();
        phone.ToTable("PhoneNumbers"); phone.HasKey(x => x.Id);
        phone.Property(x => x.Version).IsConcurrencyToken();
        phone.Property(x => x.LineUri).HasMaxLength(16).IsRequired();
        phone.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        phone.HasIndex(x => x.LineUri).IsUnique();
        phone.HasIndex(x => new { x.RangeId, x.Status, x.NumericValue });
        phone.HasIndex(x => x.AssignedUser);
        phone.HasOne<NumberRange>().WithMany().HasForeignKey(x => x.RangeId).OnDelete(DeleteBehavior.Cascade);
        var reservation = model.Entity<Reservation>();
        reservation.ToTable("Reservations"); reservation.HasKey(x => x.Id);
        reservation.Property(x => x.Version).IsConcurrencyToken();
        reservation.Property(x => x.ReservedFor).HasMaxLength(20).IsRequired();
        reservation.Property(x => x.Reason).HasMaxLength(2000);
        reservation.Property(x => x.TicketNumber).HasMaxLength(200);
        reservation.Property(x => x.System).HasConversion<string>().HasMaxLength(30);
        reservation.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
        reservation.HasIndex(x => x.PhoneNumberId).IsUnique();
        reservation.HasIndex(x => x.Expires);
        reservation.HasOne<PhoneNumber>().WithMany().HasForeignKey(x => x.PhoneNumberId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class RegistryDbContextFactory : IDesignTimeDbContextFactory<RegistryDbContext>
{
    public RegistryDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Registry")
            ?? "Host=localhost;Database=phone_registry;Username=postgres";
        return new(new DbContextOptionsBuilder<RegistryDbContext>().UseNpgsql(connection).Options);
    }
}