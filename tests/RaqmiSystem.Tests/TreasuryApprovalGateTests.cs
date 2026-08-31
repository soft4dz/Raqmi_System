using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Application.Treasury;
using RaqmiSystem.Domain.Approvals;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Domain.Treasury;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Tests;

/// <summary>
/// The REAL business wiring of the approvals module: TreasuryService.ApprovePaymentOrderAsync
/// asks <see cref="RaqmiSystem.Application.Approvals.IApprovalGate"/> before approving.
///
/// Both halves of the contract are proved here, and the first one matters most: it protects the
/// EXISTING behaviour. An installation that never configured an approval circuit must approve
/// payment orders exactly as it did before this module existed - wiring a gate into a working
/// finance path is only acceptable if it is provably inert until someone opts in.
///
/// Its own factory instance keeps the "no active circuit anywhere" baseline honest: that premise
/// is a property of the whole database, so no other test class may have activated a circuit here.
/// </summary>
public sealed class TreasuryApprovalGateTests : IClassFixture<RaqmiApiFactory>
{
    private readonly RaqmiApiFactory _factory;

    public TreasuryApprovalGateTests(RaqmiApiFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// The whole progression runs as ONE sequential test, for the same reason as
    /// ApprovalsGateTests: the "no active circuit" baseline is a property of the WHOLE database,
    /// so it cannot survive a sibling test that activates one - xunit gives no ordering between
    /// the methods of a class sharing a fixture.
    /// </summary>
    [Fact]
    public async Task The_gate_is_inert_until_a_circuit_is_activated_then_governs_each_order_separately()
    {
        // 1. BACKWARD COMPATIBILITY - the half that protects the existing behaviour. No circuit
        // has ever been activated here, so approving a payment order works exactly as it did
        // before the approvals module existed.
        var untouchedOrderId = await CreateDraftPaymentOrderAsync();

        var before = await ApproveAsync(untouchedOrderId);

        Assert.True(before.Succeeded, before.Error);
        Assert.NotNull(before.Value);
        Assert.Equal(PaymentOrderStatus.Approved, before.Value!.Status);
        Assert.Equal(PaymentOrderStatus.Approved, await ReadStatusAsync(untouchedOrderId));

        // 2. A circuit is activated for payment orders: an order that has not cleared it is now
        // refused, and the refusal says what to do about it.
        var governedOrderId = await CreateDraftPaymentOrderAsync();
        var otherOrderId = await CreateDraftPaymentOrderAsync();

        var circuit = await ActivateCircuitAsync("CIRC-OP-VISA");

        var refused = await ApproveAsync(governedOrderId);

        Assert.False(refused.Succeeded);
        Assert.Equal(ApplicationErrorType.Validation, refused.ErrorType);

        // The message has to say what to DO, not merely that something was refused: the operator
        // reading it must learn the order goes through a validation circuit first.
        Assert.Contains("validation circuit", refused.Error, StringComparison.OrdinalIgnoreCase);

        // Refused means unchanged: still a draft, approvable later once the circuit is cleared.
        Assert.Equal(PaymentOrderStatus.Draft, await ReadStatusAsync(governedOrderId));

        // 3. An APPROVED instance for this very order opens the gate - for this order only.
        await ApproveInstanceAsync(circuit, governedOrderId);

        var cleared = await ApproveAsync(governedOrderId);

        Assert.True(cleared.Succeeded, cleared.Error);
        Assert.Equal(PaymentOrderStatus.Approved, cleared.Value!.Status);

        var neighbour = await ApproveAsync(otherOrderId);

        Assert.False(neighbour.Succeeded);
        Assert.Equal(ApplicationErrorType.Validation, neighbour.ErrorType);
        Assert.Equal(PaymentOrderStatus.Draft, await ReadStatusAsync(otherOrderId));

        // 4. The guard runs AFTER the order is loaded, so an unknown id keeps its own honest
        // answer instead of being masked by a gate refusal.
        var unknown = await ApproveAsync(Guid.NewGuid());

        Assert.False(unknown.Succeeded);
        Assert.Equal(ApplicationErrorType.NotFound, unknown.ErrorType);
    }

    // ---------------------------------------------------------------- helpers

    private async Task<ApplicationResult<PaymentOrderResponse>> ApproveAsync(Guid orderId)
    {
        using var scope = _factory.Services.CreateScope();
        var treasury = scope.ServiceProvider.GetRequiredService<ITreasuryService>();

        return await treasury.ApprovePaymentOrderAsync(
            orderId,
            new OperationContext(Guid.NewGuid(), "controle.gestion", "127.0.0.1"),
            CancellationToken.None);
    }

    private async Task<Guid> CreateDraftPaymentOrderAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();

        var accountCode = await EnsureBankAccountAsync(dbContext);

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var order = new PaymentOrder(today, "Fournisseur Linge SARL", 125_000m, today.AddDays(30), accountCode);

        order.MarkCreated("tests", DateTimeOffset.UtcNow);

        dbContext.Set<PaymentOrder>().Add(order);
        await dbContext.SaveChangesAsync();

        return order.Id;
    }

    private static async Task<string> EnsureBankAccountAsync(RaqmiDbContext dbContext)
    {
        var existing = await dbContext.Set<BankAccount>()
            .Select(account => account.Code)
            .FirstOrDefaultAsync(code => code == "BNA-PRINCIPAL");

        if (existing is not null)
        {
            return existing;
        }

        var account = new BankAccount("BNA-PRINCIPAL", "Compte principal", "BNA", "00100999000012345678");
        account.MarkCreated("tests", DateTimeOffset.UtcNow);

        dbContext.Set<BankAccount>().Add(account);
        await dbContext.SaveChangesAsync();

        return account.Code;
    }

    private async Task<ApprovalCircuit> ActivateCircuitAsync(string code)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();

        var circuit = new ApprovalCircuit(code, "Validation des ordres de paiement", ApprovalSubjectType.PaymentOrder);

        circuit.ReplaceSteps(new[] { new ApprovalStep("Visa direction", RoleCatalog.Direction) });
        circuit.MarkCreated("tests", DateTimeOffset.UtcNow);
        circuit.Activate();

        dbContext.Set<ApprovalCircuit>().Add(circuit);
        await dbContext.SaveChangesAsync();

        return circuit;
    }

    private async Task ApproveInstanceAsync(ApprovalCircuit circuit, Guid subjectId)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();

        // The subject reference is the payment order's id, exactly the key the gate looks up.
        var instance = new ApprovalInstance(circuit, subjectId.ToString());
        instance.MarkCreated("tests", DateTimeOffset.UtcNow);

        dbContext.Set<ApprovalInstance>().Add(instance);
        await dbContext.SaveChangesAsync();

        instance.Decide("dg", new[] { RoleCatalog.Direction }, approved: true, comment: null, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync();

        Assert.Equal(ApprovalInstanceStatus.Approved, instance.Status);
    }

    private async Task<PaymentOrderStatus> ReadStatusAsync(Guid orderId)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();

        return await dbContext.Set<PaymentOrder>()
            .AsNoTracking()
            .Where(order => order.Id == orderId)
            .Select(order => order.Status)
            .SingleAsync();
    }
}
