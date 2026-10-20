using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhoneNumbersManagement.Domain;

namespace PhoneNumbersManagement.Infrastructure.Configurations;

public sealed class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    public void Configure(EntityTypeBuilder<Reservation> builder)
    {
        builder.ToTable("Reservations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.Property(x => x.ReservedFor).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(2000);
        builder.Property(x => x.TicketNumber).HasMaxLength(200);
        builder.Property(x => x.System).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(x => x.PhoneNumberId).IsUnique();
        builder.HasIndex(x => x.Expires);
        builder.HasOne<PhoneNumber>().WithMany().HasForeignKey(x => x.PhoneNumberId).OnDelete(DeleteBehavior.Cascade);
    }
}
