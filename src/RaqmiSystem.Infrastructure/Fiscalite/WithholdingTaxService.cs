using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Fiscalite;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Fiscalite;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Infrastructure.Fiscalite;

public sealed class WithholdingTaxService(RaqmiDbContext dbContext, IAuditLogWriter auditLogWriter) : IWithholdingTaxService
{
    private const string WithholdingTaxEntity = "fiscalite.withholding_tax";

    public async Task<IReadOnlyCollection<WithholdingTaxEntryResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var entries = await dbContext.Set<WithholdingTaxEntry>()
            .AsNoTracking()
            .OrderByDescending(entry => entry.Date)
            .ToArrayAsync(cancellationToken);

        return entries.Select(Map).ToArray();
    }

    public async Task<ApplicationResult<WithholdingTaxEntryResponse>> RegisterAsync(
        CreateWithholdingTaxEntryRequest request, OperationContext context, CancellationToken cancellationToken)
    {
        WithholdingTaxEntry entry;

        try
        {
            entry = new WithholdingTaxEntry(request.SupplierName, request.BaseHt, request.Rate, request.Date);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<WithholdingTaxEntryResponse>.Validation(ex.Message);
        }

        entry.MarkCreated(context.UserName, DateTimeOffset.UtcNow);
        dbContext.Set<WithholdingTaxEntry>().Add(entry);

        await auditLogWriter.WriteAsync(
            new AuditLogEntry(
                context.UserId, context.UserName, "fiscalite.withholding_tax.created", WithholdingTaxEntity,
                entry.Id.ToString(), context.IpAddress,
                JsonSerializer.Serialize(new { entry.SupplierName, entry.BaseHt, entry.Rate, entry.MontantRetenu })),
            cancellationToken);

        return ApplicationResult<WithholdingTaxEntryResponse>.Success(Map(entry));
    }

    private static WithholdingTaxEntryResponse Map(WithholdingTaxEntry entry) => new(
        entry.Id, entry.SupplierName, entry.BaseHt, entry.Rate, entry.MontantRetenu, entry.Date);
}
