using RaqmiSystem.Domain.Approvals;
using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Tests;

/// <summary>
/// Pure domain tests for the approvals workflow: circuit invariants (contiguous ranks, at least
/// one step to activate), instance invariants (steps decided in order, decider role checked,
/// mandatory rejection comment, closed instance immutable) and the opening-time snapshot.
/// </summary>
public sealed class ApprovalsTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Replace_steps_assigns_contiguous_ranks_from_one_in_the_given_order()
    {
        var circuit = NewTwoStepCircuit();

        circuit.ReplaceSteps(new[]
        {
            new ApprovalStep("Visa du responsable", RoleCatalog.UnitManager),
            new ApprovalStep("Visa du controle", RoleCatalog.ExploitationControl),
            new ApprovalStep("Signature direction", RoleCatalog.Direction)
        });

        Assert.Equal(new[] { 1, 2, 3 }, circuit.Steps.OrderBy(step => step.Rank).Select(step => step.Rank));
        Assert.Equal("Visa du responsable", circuit.Steps.Single(step => step.Rank == 1).Label);
        Assert.Equal("Signature direction", circuit.Steps.Single(step => step.Rank == 3).Label);
    }

    [Fact]
    public void A_step_role_must_be_a_real_system_role()
    {
        Assert.Throws<ArgumentException>(() => new ApprovalStep("Visa", "chief.happiness.officer"));
    }

    /// <summary>
    /// A step may only demand a role that can DECIDE. cashier and reader are real system roles,
    /// but they never receive approvals.decide: a step demanding one of them would be
    /// undecidable for life - its holder is refused by the authorization policy, every holder of
    /// approvals.decide fails the step's role check, and the opening-time snapshot would freeze
    /// that dead end into every instance already opened. The refusal therefore happens at
    /// construction, while it is still recoverable.
    /// </summary>
    [Fact]
    public void A_step_cannot_demand_a_role_that_never_receives_the_decide_permission()
    {
        Assert.Throws<ArgumentException>(() => new ApprovalStep("Visa caisse", RoleCatalog.Cashier));
        Assert.Throws<ArgumentException>(() => new ApprovalStep("Visa lecture", RoleCatalog.Reader));

        // The snapshot copied into an instance is held to the very same rule.
        Assert.Throws<ArgumentException>(() => new ApprovalInstanceStep(1, "Visa caisse", RoleCatalog.Cashier));

        // Every proposable role is a decider role, and each of them is accepted.
        Assert.Equal(RoleCatalog.ApprovalDeciderRoles, ApprovalStep.AllowedRoles);

        foreach (var role in ApprovalStep.AllowedRoles)
        {
            Assert.Equal(role, new ApprovalStep("Visa", role).RequiredRole);
        }
    }

    [Fact]
    public void A_circuit_without_steps_cannot_be_activated()
    {
        var circuit = new ApprovalCircuit("CIRC-OP", "Validation des OP", ApprovalSubjectType.PaymentOrder);

        Assert.False(circuit.IsActive);
        Assert.Throws<InvalidOperationException>(circuit.Activate);
    }

    [Fact]
    public void An_active_circuit_cannot_be_left_without_steps()
    {
        var circuit = NewTwoStepCircuit();
        circuit.Activate();

        Assert.Throws<InvalidOperationException>(() => circuit.ReplaceSteps(Array.Empty<ApprovalStep>()));
    }

    [Fact]
    public void An_instance_snapshots_the_circuit_steps_so_later_edits_never_reach_it()
    {
        var circuit = NewTwoStepCircuit();
        circuit.Activate();

        var instance = new ApprovalInstance(circuit, "OP-0001");

        // The circuit is rewritten AFTER the instance opened: one different step, new labels.
        circuit.ReplaceSteps(new[] { new ApprovalStep("Visa unique direction", RoleCatalog.Direction) });
        circuit.UpdateDetails("Circuit reecrit");

        // The in-flight instance still demands the two ORIGINAL steps, in order.
        Assert.Equal(2, instance.Steps.Count);
        Assert.Equal("Visa du responsable", instance.Steps.Single(step => step.Rank == 1).Label);
        Assert.Equal(RoleCatalog.UnitManager, instance.Steps.Single(step => step.Rank == 1).RequiredRole);
        Assert.Equal("Signature direction", instance.Steps.Single(step => step.Rank == 2).Label);

        // And its full lifecycle keeps following the snapshot: the direction alone cannot close
        // it in one decision, the unit manager's step still comes first.
        Assert.Throws<InvalidOperationException>(() =>
            instance.Decide("dg", new[] { RoleCatalog.Direction }, approved: true, comment: null, Now));
    }

    [Fact]
    public void An_instance_cannot_be_opened_on_an_inactive_circuit()
    {
        var circuit = NewTwoStepCircuit();

        Assert.Throws<InvalidOperationException>(() => new ApprovalInstance(circuit, "OP-0001"));
    }

    [Fact]
    public void Steps_are_decided_in_order_and_the_decider_must_carry_the_step_role()
    {
        var instance = NewOpenInstance();

        // Step 1 requires unit.manager: the direction, though a decider of step 2, cannot jump
        // the queue - the current step is not theirs.
        Assert.Throws<InvalidOperationException>(() =>
            instance.Decide("dg", new[] { RoleCatalog.Direction }, approved: true, comment: null, Now));

        // Someone with no role at all is refused too.
        Assert.Throws<InvalidOperationException>(() =>
            instance.Decide("nobody", Array.Empty<string>(), approved: true, comment: null, Now));

        instance.Decide("chef.unite", new[] { RoleCatalog.UnitManager }, approved: true, comment: null, Now);

        Assert.Equal(ApprovalInstanceStatus.InProgress, instance.Status);
        Assert.Equal(2, instance.CurrentStep!.Rank);

        // Now it is the direction's turn - and the unit manager can no longer decide.
        Assert.Throws<InvalidOperationException>(() =>
            instance.Decide("chef.unite", new[] { RoleCatalog.UnitManager }, approved: true, comment: null, Now));

        instance.Decide("dg", new[] { RoleCatalog.Direction }, approved: true, comment: null, Now.AddMinutes(5));

        Assert.Equal(ApprovalInstanceStatus.Approved, instance.Status);
        Assert.Null(instance.CurrentStep);
        Assert.Equal(Now.AddMinutes(5), instance.ClosedAt);
        Assert.Equal("dg", instance.ClosedBy);
        Assert.Equal(2, instance.Decisions.Count);
    }

    [Fact]
    public void A_rejection_requires_a_comment()
    {
        var instance = NewOpenInstance();

        Assert.Throws<ArgumentException>(() =>
            instance.Decide("chef.unite", new[] { RoleCatalog.UnitManager }, approved: false, comment: "   ", Now));

        // The refused rejection recorded nothing: the instance is still deciding step 1.
        Assert.Equal(ApprovalInstanceStatus.InProgress, instance.Status);
        Assert.Empty(instance.Decisions);
        Assert.Equal(1, instance.CurrentStep!.Rank);
    }

    [Fact]
    public void A_rejection_closes_the_instance_and_makes_it_immutable()
    {
        var instance = NewOpenInstance();

        instance.Decide("chef.unite", new[] { RoleCatalog.UnitManager }, approved: false, "Montant non conforme au marche.", Now);

        Assert.Equal(ApprovalInstanceStatus.Rejected, instance.Status);
        Assert.Null(instance.CurrentStep);
        Assert.Equal(Now, instance.ClosedAt);

        var decision = Assert.Single(instance.Decisions);
        Assert.False(decision.Approved);
        Assert.Equal("Montant non conforme au marche.", decision.Comment);

        // Rejected means closed for good: even the very role of the next step is refused.
        Assert.Throws<InvalidOperationException>(() =>
            instance.Decide("dg", new[] { RoleCatalog.Direction }, approved: true, comment: null, Now.AddMinutes(1)));
    }

    [Fact]
    public void An_approved_instance_is_immutable()
    {
        var instance = NewOpenInstance();

        instance.Decide("chef.unite", new[] { RoleCatalog.UnitManager }, approved: true, comment: null, Now);
        instance.Decide("dg", new[] { RoleCatalog.Direction }, approved: true, comment: null, Now);

        Assert.Equal(ApprovalInstanceStatus.Approved, instance.Status);

        Assert.Throws<InvalidOperationException>(() =>
            instance.Decide("dg", new[] { RoleCatalog.Direction }, approved: false, "Trop tard.", Now));
    }

    [Fact]
    public void Role_matching_ignores_claim_casing()
    {
        var instance = NewOpenInstance();

        instance.Decide("chef.unite", new[] { "UNIT.MANAGER" }, approved: true, comment: null, Now);

        Assert.Equal(2, instance.CurrentStep!.Rank);
    }

    private static ApprovalCircuit NewTwoStepCircuit()
    {
        var circuit = new ApprovalCircuit("circ-op", "Validation des ordres de paiement", ApprovalSubjectType.PaymentOrder);

        circuit.ReplaceSteps(new[]
        {
            new ApprovalStep("Visa du responsable", RoleCatalog.UnitManager),
            new ApprovalStep("Signature direction", RoleCatalog.Direction)
        });

        // The code is normalized upper-case, like every code of the system.
        Assert.Equal("CIRC-OP", circuit.Code);

        return circuit;
    }

    private static ApprovalInstance NewOpenInstance()
    {
        var circuit = NewTwoStepCircuit();
        circuit.Activate();

        return new ApprovalInstance(circuit, "OP-0001");
    }
}
