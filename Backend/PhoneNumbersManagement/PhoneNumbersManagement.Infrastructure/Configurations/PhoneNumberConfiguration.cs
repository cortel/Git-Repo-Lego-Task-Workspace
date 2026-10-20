using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhoneNumbersManagement.Domain;

namespace PhoneNumbersManagement.Infrastructure.Configurations;

public sealed class PhoneNumberConfiguration : IEntityTypeConfiguration<PhoneNumber>
{
    public void Configure(EntityTypeBuilder<PhoneNumber> builder)
    {
        builder.ToTable("PhoneNumbers");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.Property(x => x.LineUri).HasMaxLength(16).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(x => x.LineUri).IsUnique();
        builder.HasIndex(x => new { x.RangeId, x.Status, x.NumericValue });
        builder.HasIndex(x => x.AssignedUser);
        builder.HasOne<NumberRange>().WithMany().HasForeignKey(x => x.RangeId).OnDelete(DeleteBehavior.Cascade);
    }
}
