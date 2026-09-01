using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Lodging;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Infrastructure.Lodging;

public sealed class PackageConfiguration : IEntityTypeConfiguration<Package>
{
    public void Configure(EntityTypeBuilder<Package> builder)
    {
        builder.ToTable("packages", "lodging", table =>
        {
            table.HasCheckConstraint("ck_packages_total_price", "CAST(total_price AS numeric) >= 0");
        });

        builder.HasKey(package => package.Id);

        builder.Property(package => package.Id).HasColumnName("id");
        builder.Property(package => package.CreatedAt).HasColumnName("created_at");
        builder.Property(package => package.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(package => package.UpdatedAt).HasColumnName("updated_at");
        builder.Property(package => package.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(package => package.HotelUnitCode)
            .HasColumnName("hotel_unit_code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(package => package.Code)
            .HasColumnName("code")
            .HasMaxLength(Package.CodeMaxLength)
            .IsRequired();

        builder.Property(package => package.Label)
            .HasColumnName("label")
            .HasMaxLength(Package.LabelMaxLength)
            .IsRequired();

        builder.Property(package => package.Description)
            .HasColumnName("description")
            .HasMaxLength(Package.DescriptionMaxLength);

        builder.Property(package => package.TotalPrice).HasColumnName("total_price").HasPrecision(18, 2);

        builder.Property(package => package.RatePlanCode)
            .HasColumnName("rate_plan_code")
            .HasMaxLength(40);

        builder.Property(package => package.RoomTypeCode)
            .HasColumnName("room_type_code")
            .HasMaxLength(40);

        builder.Property(package => package.ValidFrom).HasColumnName("valid_from");
        builder.Property(package => package.ValidTo).HasColumnName("valid_to");
        builder.Property(package => package.Nights).HasColumnName("nights");
        builder.Property(package => package.IsActive).HasColumnName("is_active");

        builder.Ignore(package => package.ComponentsTotal);
        builder.Ignore(package => package.IsBalanced);

        builder.HasIndex(package => new { package.HotelUnitCode, package.Code })
            .IsUnique()
            .HasDatabaseName("ux_packages_hotel_unit_code_code");

        builder.HasMany(package => package.Components)
            .WithOne()
            .HasForeignKey(component => component.PackageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(package => package.Components).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasOne<HotelUnit>()
            .WithMany()
            .HasPrincipalKey(unit => unit.Code)
            .HasForeignKey(package => package.HotelUnitCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
