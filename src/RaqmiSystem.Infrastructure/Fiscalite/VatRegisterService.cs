using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Fiscalite;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Fiscalite;
using RaqmiSystem.Domain.Purchasing;
using RaqmiSystem.Infrastructure.Persistence;
using System.Text.Json;

namespace RaqmiSystem.Infrastructure.Fiscalite;

/// <summary>
/// VAT sales/purchases registers. <see cref="RegisterSaleAsync"/>/<see cref="RegisterPurchaseAsync"/>
/// stage the new row and write its own audit entry - the audit writer's SaveChangesAsync call is
/// what actually persists the row, so no redundant extra save is issued here. When the caller
/// (PurchasingService.ReceiveOrderAsync) runs inside its own explicit transaction, that save joins
/// the ambient transaction like any other EF Core write and commits or rolls back with it.
/// </summary>
public sealed class VatRegisterService(RaqmiDbContext dbContext, IAuditLogWriter auditLogWriter) : IVatRegisterService
{
    private const string SalesRegisterEntity = "fiscalite.vat_sales_register";
    private const string PurchasesRegisterEntity = "fiscalite.vat_purchase_register";

    public async Task<IReadOnlyCollection<VatSalesRegisterEntryResponse>> GetSalesRegisterAsync(
        int year, int month, CancellationToken cancellationToken)
    {
        var (start, end) = PeriodBounds(year, month);

        var entries = await dbContext.Set<VatSalesRegisterEntry>()
            .AsNoTracking()
            .Where(entry => entry.PieceDate >= start && entry.PieceDate <= end)
            .OrderBy(entry => entry.PieceDate)
            .ToArrayAsync(cancellationToken);

        return entries.Select(Map).ToArray();
    }

    public async Task<IReadOnlyCollection<VatPurchaseRegisterEntryResponse>> GetPurchasesRegisterAsync(
        int year, int month, CancellationToken cancellationToken)
    {
        var (start, end) = PeriodBounds(year, month);

        var entries = await dbContext.Set<VatPurchaseRegisterEntry>()
            .AsNoTracking()
            .Where(entry => entry.PieceDate >= start && entry.PieceDate <= end)
            .OrderBy(entry => entry.PieceDate)
            .ToArrayAsync(cancellationToken);

        return entries.Select(Map).ToArray();
    }

    public async Task RegisterSaleAsync(RegisterVatSaleRequest request, OperationContext context, CancellationToken cancellationToken)
    {
        var entry = new VatSalesRegisterEntry(
            request.InvoiceId, request.PieceNumber, request.PieceDate, request.Type,
            request.CustomerName, request.CustomerNif, request.BaseHt, request.VatAmount);

        entry.MarkCreated(context.UserName, DateTimeOffset.UtcNow);
        dbContext.Set<VatSalesRegisterEntry>().Add(entry);

        await WriteAuditAsync(
            "fiscalite.vat_sales_register.created", SalesRegisterEntity, entry.Id, context,
            new { entry.PieceNumber, entry.BaseHt, entry.VatAmount }, cancellationToken, save: false);
    }

    public async Task RegisterPurchaseAsync(RegisterVatPurchaseRequest request, OperationContext context, CancellationToken cancellationToken)
    {
        var entry = new VatPurchaseRegisterEntry(
            request.PurchaseOrderId, request.PieceNumber, request.PieceDate,
            request.SupplierName, request.SupplierNif, request.BaseHt, request.VatAmount, VatRegisterSource.Achats);

        entry.MarkCreated(context.UserName, DateTimeOffset.UtcNow);
        dbContext.Set<VatPurchaseRegisterEntry>().Add(entry);

        await WriteAuditAsync(
            "fiscalite.vat_purchase_register.created", PurchasesRegisterEntity, entry.Id, context,
            new { entry.PieceNumber, entry.BaseHt, entry.VatAmount, Source = "Achats" }, cancellationToken, save: false);
    }

    public async Task<ApplicationResult<VatPurchaseRegisterEntryResponse>> CreateManualPurchaseEntryAsync(
        CreateVatPurchaseEntryRequest request, OperationContext context, CancellationToken cancellationToken)
    {
        VatPurchaseRegisterEntry entry;

        try
        {
            entry = new VatPurchaseRegisterEntry(
                null, request.PieceNumber, request.PieceDate, request.SupplierName, request.SupplierNif,
                request.BaseHt, request.VatAmount, VatRegisterSource.Manuel);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<VatPurchaseRegisterEntryResponse>.Validation(ex.Message);
        }

        entry.MarkCreated(context.UserName, DateTimeOffset.UtcNow);
        dbContext.Set<VatPurchaseRegisterEntry>().Add(entry);

        await WriteAuditAsync(
            "fiscalite.vat_purchase_register.created", PurchasesRegisterEntity, entry.Id, context,
            new { entry.PieceNumber, entry.BaseHt, entry.VatAmount, Source = "Manuel" }, cancellationToken, save: true);

        return ApplicationResult<VatPurchaseRegisterEntryResponse>.Success(Map(entry));
    }

    /// <summary>
    /// Imports every received (fully or partially) order of the period not already represented in
    /// the register - matched by PurchaseOrderId, so a re-run never duplicates a line, mirroring the
    /// legacy "Importer bons validés" behaviour.
    /// </summary>
    public async Task<ApplicationResult<int>> ImportApprovedPurchaseOrdersAsync(
        int year, int month, OperationContext context, CancellationToken cancellationToken)
    {
        var (start, end) = PeriodBounds(year, month);

        var alreadyImported = await dbContext.Set<VatPurchaseRegisterEntry>()
            .Where(entry => entry.PurchaseOrderId != null)
            .Select(entry => entry.PurchaseOrderId!.Value)
            .ToArrayAsync(cancellationToken);
        var alreadyImportedSet = alreadyImported.ToHashSet();

        var candidateOrders = await dbContext.Set<PurchaseOrder>()
            .Include(order => order.Lines)
            .Where(order =>
                order.OrderDate >= start && order.OrderDate <= end &&
                (order.Status == PurchaseOrderStatus.Received || order.Status == PurchaseOrderStatus.PartiallyReceived) &&
                !alreadyImportedSet.Contains(order.Id))
            .ToArrayAsync(cancellationToken);

        var supplierCodes = candidateOrders.Select(order => order.SupplierCode).Distinct().ToArray();
        var suppliers = await dbContext.Set<Supplier>()
            .AsNoTracking()
            .Where(supplier => supplierCodes.Contains(supplier.Code))
            .ToDictionaryAsync(supplier => supplier.Code, cancellationToken);

        var imported = 0;
        var now = DateTimeOffset.UtcNow;

        foreach (var order in candidateOrders)
        {
            suppliers.TryGetValue(order.SupplierCode, out var supplier);

            var entry = new VatPurchaseRegisterEntry(
                order.Id,
                order.Number ?? order.Id.ToString(),
                order.OrderDate,
                supplier?.Name ?? order.SupplierCode,
                supplier?.Nif,
                order.Lines.Sum(line => line.LineTotalExclVat),
                order.Lines.Sum(line => line.VatAmount),
                VatRegisterSource.Manuel);

            entry.MarkCreated(context.UserName, now);
            dbContext.Set<VatPurchaseRegisterEntry>().Add(entry);
            imported++;
        }

        if (imported > 0)
        {
            await WriteAuditAsync(
                "fiscalite.vat_purchase_register.imported", PurchasesRegisterEntity, Guid.Empty, context,
                new { Year = year, Month = month, Count = imported }, cancellationToken, save: true);
        }

        return ApplicationResult<int>.Success(imported);
    }

    private static (DateOnly Start, DateOnly End) PeriodBounds(int year, int month)
    {
        var start = new DateOnly(year, month, 1);
        return (start, start.AddMonths(1).AddDays(-1));
    }

    private static VatSalesRegisterEntryResponse Map(VatSalesRegisterEntry entry) => new(
        entry.Id, entry.InvoiceId, entry.PieceNumber, entry.PieceDate, entry.Type,
        entry.CustomerName, entry.CustomerNif, entry.BaseHt, entry.VatAmount, entry.Ttc);

    private static VatPurchaseRegisterEntryResponse Map(VatPurchaseRegisterEntry entry) => new(
        entry.Id, entry.PurchaseOrderId, entry.PieceNumber, entry.PieceDate,
        entry.SupplierName, entry.SupplierNif, entry.BaseHt, entry.VatAmount, entry.Ttc, entry.Source);

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task WriteAuditAsync(
        string action, string entityName, Guid entityId, OperationContext context, object details,
        CancellationToken cancellationToken, bool save)
    {
        await auditLogWriter.WriteAsync(
            new AuditLogEntry(context.UserId, context.UserName, action, entityName, entityId.ToString(), context.IpAddress, JsonSerializer.Serialize(details)),
            cancellationToken);

        if (save)
        {
            await SaveAsync(cancellationToken);
        }
    }
}
