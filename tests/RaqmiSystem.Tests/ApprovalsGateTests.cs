using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaqmiSystem.Application.Approvals;
using RaqmiSystem.Domain.Approvals;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Tests;

/// <summary>
/// Semantics of <see cref="IApprovalGate"/>, the question consuming modules (first: the treasury
/// payment-order approval) ask before letting a subject proceed. The whole progression - no
/// circuit, inactive circuit, active circuit, approved instance - runs as ONE sequential test
/// because the "no circuit yet" baseline is a property of the whole database: its own dedicated
/// factory guarantees no other test class has configured a circuit here.
/// </summary>
public sealed class ApprovalsGateTests : IClassFixture<RaqmiApiFactory>
{
    private readonly RaqmiApiFactory _factory;

    public ApprovalsGateTests(RaqmiApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task The_gate_is_open_without_an_active_circuit_and_demands_an_approved_instance_with_one()
    {
        var reference = Guid.NewGuid().ToString();

        // 1. No circuit at all: the gate is OPEN. This is the backward-compatibility contract -
        // an installation that never configured approvals behaves exactly as before the module
        // existed, and wiring the gate into TreasuryService changes nothing for it.
        Assert.True(await AskGateAsync(reference));

        // 2. A circuit exists but is INACTIVE: still open. Configuring is not yet governing.
        var circuit = new ApprovalCircuit("CIRC-GATE", "Validation des OP", ApprovalSubjectType.PaymentOrder);

        circuit.ReplaceSteps(new[] { new ApprovalStep("Visa direction", RoleCatalog.Direction) });
        circuit.MarkCreated("tests", DateTimeOffset.UtcNow);

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();
            dbContext.Set<ApprovalCircuit>().Add(circuit);
            await dbContext.SaveChangesAsync();
        }

        Assert.True(await AskGateAsync(reference));

        // 3. The circuit is ACTIVATED: the gate now closes on anything not approved - an
        // absent instance...
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();

            // The steps must travel with the circuit: Activate() enforces the
            // at-least-one-step invariant against the loaded collection.
            var tracked = await dbContext.Set<ApprovalCircuit>()
                .Include(current => current.Steps)
                .SingleAsync(current => current.Id == circuit.Id);

            tracked.Activate();
            await dbContext.SaveChangesAsync();
        }

        Assert.False(await AskGateAsync(reference));

        // ...and an instance still in progress.
        var instance = OpenInstance(circuit, reference);

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();
            dbContext.Set<ApprovalInstance>().Add(instance);
            await dbContext.SaveChangesAsync();
        }

        Assert.False(await AskGateAsync(reference));

        // 4. The single step is approved: the gate opens for THIS reference only.
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();
            var tracked = await dbContext.Set<ApprovalInstance>().FindAsync(instance.Id);
            await dbContext.Entry(tracked!).Collection(current => current.Steps).LoadAsync();
            await dbContext.Entry(tracked!).Collection(current => current.Decisions).LoadAsync();

            tracked!.Decide("dg", new[] { RoleCatalog.Direction }, approved: true, comment: null, DateTimeOffset.UtcNow);
            await dbContext.SaveChangesAsync();
        }

        Assert.True(await AskGateAsync(reference));
        Assert.False(await AskGateAsync(Guid.NewGuid().ToString()));
    }

    [Fact]
    public async Task The_gate_refuses_a_blank_reference()
    {
        using var scope = _factory.Services.CreateScope();
        var gate = scope.ServiceProvider.GetRequiredService<IApprovalGate>();

        var answer = await gate.IsApprovedAsync(ApprovalSubjectType.PaymentOrder, "   ", CancellationToken.None);

        Assert.False(answer.Succeeded);
    }

    private async Task<bool> AskGateAsync(string reference)
    {
        using var scope = _factory.Services.CreateScope();
        var gate = scope.ServiceProvider.GetRequiredService<IApprovalGate>();

        var answer = await gate.IsApprovedAsync(ApprovalSubjectType.PaymentOrder, reference, CancellationToken.None);

        Assert.True(answer.Succeeded);

        return answer.Value;
    }

    private static ApprovalInstance OpenInstance(ApprovalCircuit circuit, string reference)
    {
        // The circuit entity used here was activated in the database; mirror it in memory so
        // the instance constructor sees an active circuit.
        if (!circuit.IsActive)
        {
            circuit.Activate();
        }

        var instance = new ApprovalInstance(circuit, reference);
        instance.MarkCreated("tests", DateTimeOffset.UtcNow);

        return instance;
    }
}
