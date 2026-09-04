using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Organization;
using RaqmiSystem.Domain.Pos;

namespace RaqmiSystem.Infrastructure.Pos;

internal static class PosConfig
{
    public static void Audit<T>(EntityTypeBuilder<T> b) where T : Domain.Common.AuditableEntity
    { b.HasKey(x => x.Id); b.Property(x => x.Id).HasColumnName("id"); b.Property(x => x.CreatedAt).HasColumnName("created_at"); b.Property(x => x.CreatedBy).HasColumnName("created_by").HasMaxLength(160); b.Property(x => x.UpdatedAt).HasColumnName("updated_at"); b.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160); }
}
public sealed class PosOutletConfiguration : IEntityTypeConfiguration<PosOutlet>
{
    public void Configure(EntityTypeBuilder<PosOutlet> b) { b.ToTable("outlets", "pos"); PosConfig.Audit(b); b.Property(x=>x.Code).HasColumnName("code").HasMaxLength(40).IsRequired(); b.Property(x=>x.Name).HasColumnName("name").HasMaxLength(160).IsRequired(); b.Property(x=>x.HotelUnitCode).HasColumnName("hotel_unit_code").HasMaxLength(40).IsRequired(); b.Property(x=>x.Kind).HasColumnName("kind").HasMaxLength(60).IsRequired(); b.Property(x=>x.IsActive).HasColumnName("is_active"); b.HasIndex(x=>new{x.HotelUnitCode,x.Code}).IsUnique(); b.HasOne<HotelUnit>().WithMany().HasPrincipalKey(x=>x.Code).HasForeignKey(x=>x.HotelUnitCode).OnDelete(DeleteBehavior.Restrict); }
}
public sealed class PosTableConfiguration : IEntityTypeConfiguration<PosTable>
{
    public void Configure(EntityTypeBuilder<PosTable> b) { b.ToTable("tables", "pos"); PosConfig.Audit(b); b.Property(x=>x.OutletId).HasColumnName("outlet_id"); b.Property(x=>x.Zone).HasColumnName("zone").HasMaxLength(80).IsRequired(); b.Property(x=>x.Number).HasColumnName("number").HasMaxLength(30).IsRequired(); b.Property(x=>x.Seats).HasColumnName("seats"); b.Property(x=>x.IsActive).HasColumnName("is_active"); b.HasIndex(x=>new{x.OutletId,x.Number}).IsUnique(); b.HasOne<PosOutlet>().WithMany().HasForeignKey(x=>x.OutletId).OnDelete(DeleteBehavior.Cascade); }
}
public sealed class PosProductConfiguration : IEntityTypeConfiguration<PosProduct>
{
    public void Configure(EntityTypeBuilder<PosProduct> b) { b.ToTable("products", "pos"); PosConfig.Audit(b); b.Property(x=>x.OutletId).HasColumnName("outlet_id"); b.Property(x=>x.Code).HasColumnName("code").HasMaxLength(40).IsRequired(); b.Property(x=>x.Name).HasColumnName("name").HasMaxLength(160).IsRequired(); b.Property(x=>x.Category).HasColumnName("category").HasMaxLength(80).IsRequired(); b.Property(x=>x.Price).HasColumnName("price").HasPrecision(18,2); b.Property(x=>x.IsActive).HasColumnName("is_active"); b.HasIndex(x=>new{x.OutletId,x.Code}).IsUnique(); b.HasOne<PosOutlet>().WithMany().HasForeignKey(x=>x.OutletId).OnDelete(DeleteBehavior.Cascade); }
}
public sealed class PosTicketConfiguration : IEntityTypeConfiguration<PosTicket>
{
    public void Configure(EntityTypeBuilder<PosTicket> b) { b.ToTable("tickets", "pos"); PosConfig.Audit(b); b.Property(x=>x.OutletId).HasColumnName("outlet_id"); b.Property(x=>x.Number).HasColumnName("number").HasMaxLength(40).IsRequired(); b.Property(x=>x.OrderType).HasColumnName("order_type").HasConversion<string>().HasMaxLength(30); b.Property(x=>x.TableId).HasColumnName("table_id"); b.Property(x=>x.RoomNumber).HasColumnName("room_number").HasMaxLength(30); b.Property(x=>x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20); b.Property(x=>x.OpenedAt).HasColumnName("opened_at"); b.Property(x=>x.ClosedAt).HasColumnName("closed_at"); b.Property(x=>x.PaymentMethod).HasColumnName("payment_method").HasConversion<string>().HasMaxLength(30); b.Property(x=>x.CancellationReason).HasColumnName("cancellation_reason").HasMaxLength(240); b.HasIndex(x=>x.Number).IsUnique(); b.HasOne<PosOutlet>().WithMany().HasForeignKey(x=>x.OutletId).OnDelete(DeleteBehavior.Restrict); b.HasOne<PosTable>().WithMany().HasForeignKey(x=>x.TableId).OnDelete(DeleteBehavior.Restrict); b.HasMany(x=>x.Lines).WithOne().HasForeignKey(x=>x.TicketId).OnDelete(DeleteBehavior.Cascade); }
}
public sealed class PosTicketLineConfiguration : IEntityTypeConfiguration<PosTicketLine>
{
    public void Configure(EntityTypeBuilder<PosTicketLine> b) { b.ToTable("ticket_lines", "pos"); PosConfig.Audit(b); b.Property(x=>x.TicketId).HasColumnName("ticket_id"); b.Property(x=>x.ProductId).HasColumnName("product_id"); b.Property(x=>x.ProductName).HasColumnName("product_name").HasMaxLength(160); b.Property(x=>x.Quantity).HasColumnName("quantity"); b.Property(x=>x.UnitPrice).HasColumnName("unit_price").HasPrecision(18,2); b.Ignore(x=>x.Total); b.HasOne<PosProduct>().WithMany().HasForeignKey(x=>x.ProductId).OnDelete(DeleteBehavior.Restrict); }
}
