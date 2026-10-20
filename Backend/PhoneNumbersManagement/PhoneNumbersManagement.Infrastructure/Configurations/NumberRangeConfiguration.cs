using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhoneNumbersManagement.Domain;

namespace PhoneNumbersManagement.Infrastructure.Configurations;

public sealed class NumberRangeConfiguration : IEntityTypeConfiguration<NumberRange>
{
    public void Configure(EntityTypeBuilder<NumberRange> builder)
    {
        builder.ToTable("Ranges"); builder.HasKey(x => x.Id);
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.RangeStart).HasMaxLength(16).IsRequired();
        builder.Property(x => x.RangeEnd).HasMaxLength(16).IsRequired();
        builder.Property(x => x.MainLine).HasMaxLength(16);
        builder.Property(x => x.Provider).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.VoiceRoute).HasMaxLength(500);
        builder.Property(x => x.NetboxSiteId).HasMaxLength(200);
        builder.HasIndex(x => new { x.Active, x.NetboxSiteId });
    }
}
