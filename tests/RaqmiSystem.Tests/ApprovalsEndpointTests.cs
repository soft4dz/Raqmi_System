using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaqmiSystem.Application.Approvals;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Approvals;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Tests;

/// <summary>
/// Full-HTTP integration coverage for the workflows &amp; validations module. Each test
/// provisions its own users; a user carries BOTH a real system role (RoleCatalog - the circuits
/// demand roles, and the role claims in the JWT are what the decide path matches) AND a
/// single-purpose test role holding exactly the approvals permission keys it needs.
///
/// The permission keys (approvals.read / approvals.write / approvals.decide) are seeded from
/// PermissionCatalog by SecuritySeeder during factory startup, and Program.cs registers one
/// authorization policy per catalog key.
///
/// The tests share one in-memory database (one factory per test class) and the module allows a
/// single ACTIVE circuit per subject type, so every test that needs an active circuit goes
/// through EnsureActiveCircuitAsync (idempotent) and works on its own subject references. That
/// keeps them independent of the order xunit runs them in.
/// </summary>
public sealed class ApprovalsEndpointTests : IClassFixture<RaqmiApiFactory>
{
    private const string Password = "Correct-Horse-Battery-42!";

    private const string ApprovalsRead = "approvals.read";
    private const string ApprovalsWrite = "approvals.write";
    private const string ApprovalsDecide = "approvals.decide";

    private const string SharedCircuitCode = "CIRC-OP-SHARED";

    private readonly RaqmiApiFactory _factory;

    public ApprovalsEndpointTests(RaqmiApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task A_two_step_circuit_governs_a_payment_order_from_opening_to_full_approval()
    {
        await CreateApprovalsUserAsync("wf.control", "wf.control@example.com", "Configurateur", null, ApprovalsRead, ApprovalsWrite);
        await CreateApprovalsUserAsync("wf.manager", "wf.manager@example.com", "Chef d'unite", RoleCatalog.UnitManager, ApprovalsRead, ApprovalsDecide);
        await CreateApprovalsUserAsync("wf.direction", "wf.direction@example.com", "Direction", RoleCatalog.Direction, ApprovalsRead, ApprovalsDecide);

        using var controlClient = await _factory.CreateAuthenticatedClientAsync("wf.control", Password);
        using var managerClient = await _factory.CreateAuthenticatedClientAsync("wf.manager", Password);
        using var directionClient = await _factory.CreateAuthenticatedClientAsync("wf.direction", Password);

        var circuit = await EnsureActiveCircuitAsync();
        Assert.True(circuit.IsActive);
        Assert.Equal(new[] { 1, 2 }, circuit.Steps.Select(step => step.Rank));

        var reference = Guid.NewGuid().ToString();

        // Opening freezes the circuit's steps into the instance.
        var openResponse = await controlClient.PostAsJsonAsync(
            "/api/v1/approvals/instances",
            new OpenApprovalInstanceRequest(ApprovalSubjectType.PaymentOrder, reference),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, openResponse.StatusCode);

        var instance = await openResponse.Content.ReadFromJsonAsync<ApprovalInstanceResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(instance);
        Assert.Equal(ApprovalInstanceStatus.InProgress, instance!.Status);
        Assert.Equal(1, instance.CurrentRank);
        Assert.Equal(RoleCatalog.UnitManager, instance.CurrentStepRequiredRole);

        // The configurator holds approvals.write but not approvals.decide: deciding is a
        // DISTINCT act, refused by the authorization policy itself.
        var forbiddenDecide = await controlClient.PostAsJsonAsync(
            $"/api/v1/approvals/instances/{instance.Id}/approve",
            new DecideApprovalRequest(null),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, forbiddenDecide.StatusCode);

        // Steps are decided IN ORDER: the direction, decider of step 2, cannot jump the queue
        // while step 1 (unit.manager) is pending.
        var outOfOrder = await directionClient.PostAsJsonAsync(
            $"/api/v1/approvals/instances/{instance.Id}/approve",
            new DecideApprovalRequest(null),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, outOfOrder.StatusCode);
        Assert.Contains("unit.manager", await outOfOrder.Content.ReadAsStringAsync());

        // "My pending decisions": the instance sits in the manager's queue, not the direction's.
        Assert.Contains(await GetPendingIdsAsync(managerClient), id => id == instance.Id);
        Assert.DoesNotContain(await GetPendingIdsAsync(directionClient), id => id == instance.Id);

        var stepOne = await ApproveAsync(managerClient, instance.Id, "Conforme au marche.");
        Assert.Equal(ApprovalInstanceStatus.InProgress, stepOne.Status);
        Assert.Equal(2, stepOne.CurrentRank);

        // The queue moved on with the workflow.
        Assert.DoesNotContain(await GetPendingIdsAsync(managerClient), id => id == instance.Id);
        Assert.Contains(await GetPendingIdsAsync(directionClient), id => id == instance.Id);

        var stepTwo = await ApproveAsync(directionClient, instance.Id, null);
        Assert.Equal(ApprovalInstanceStatus.Approved, stepTwo.Status);
        Assert.Null(stepTwo.CurrentRank);
        Assert.Equal(2, stepTwo.Decisions.Count);

        // An approved instance is immutable: one more decision answers 409, not 400.
        var tooLate = await directionClient.PostAsJsonAsync(
            $"/api/v1/approvals/instances/{instance.Id}/approve",
            new DecideApprovalRequest(null),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, tooLate.StatusCode);

        // The gate the treasury will call: approved reference passes, unknown reference does not
        // (an active circuit covers payment orders in this database).
        using var scope = _factory.Services.CreateScope();
        var gate = scope.ServiceProvider.GetRequiredService<IApprovalGate>();

        var approvedAnswer = await gate.IsApprovedAsync(ApprovalSubjectType.PaymentOrder, reference, CancellationToken.None);
        Assert.True(approvedAnswer.Succeeded);
        Assert.True(approvedAnswer.Value);

        var unknownAnswer = await gate.IsApprovedAsync(ApprovalSubjectType.PaymentOrder, Guid.NewGuid().ToString(), CancellationToken.None);
        Assert.True(unknownAnswer.Succeeded);
        Assert.False(unknownAnswer.Value);
    }

    [Fact]
    public async Task A_rejection_requires_a_comment_and_closes_the_instance()
    {
        await CreateApprovalsUserAsync("wf.opener", "wf.opener@example.com", "Ouvreur", null, ApprovalsRead, ApprovalsWrite);
        await CreateApprovalsUserAsync("wf.rejecter", "wf.rejecter@example.com", "Chef rejeteur", RoleCatalog.UnitManager, ApprovalsRead, ApprovalsDecide);

        using var openerClient = await _factory.CreateAuthenticatedClientAsync("wf.opener", Password);
        using var rejecterClient = await _factory.CreateAuthenticatedClientAsync("wf.rejecter", Password);

        await EnsureActiveCircuitAsync();

        var reference = Guid.NewGuid().ToString();

        var openResponse = await openerClient.PostAsJsonAsync(
            "/api/v1/approvals/instances",
            new OpenApprovalInstanceRequest(ApprovalSubjectType.PaymentOrder, reference),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, openResponse.StatusCode);
        var instance = await openResponse.Content.ReadFromJsonAsync<ApprovalInstanceResponse>(RaqmiApiFactory.JsonOptions);

        // No comment, no rejection: a refusal without a stated reason is not auditable.
        var noComment = await rejecterClient.PostAsJsonAsync(
            $"/api/v1/approvals/instances/{instance!.Id}/reject",
            new DecideApprovalRequest("   "),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, noComment.StatusCode);

        var rejectResponse = await rejecterClient.PostAsJsonAsync(
            $"/api/v1/approvals/instances/{instance.Id}/reject",
            new DecideApprovalRequest("Montant errone."),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, rejectResponse.StatusCode);

        var rejected = await rejectResponse.Content.ReadFromJsonAsync<ApprovalInstanceResponse>(RaqmiApiFactory.JsonOptions);
        Assert.Equal(ApprovalInstanceStatus.Rejected, rejected!.Status);
        Assert.NotNull(rejected.ClosedAt);

        var decision = Assert.Single(rejected.Decisions);
        Assert.False(decision.Approved);
        Assert.Equal("Montant errone.", decision.Comment);

        // Closed for good: the second step's decider gets a 409, not a turn.
        var afterClose = await rejecterClient.PostAsJsonAsync(
            $"/api/v1/approvals/instances/{instance.Id}/approve",
            new DecideApprovalRequest(null),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, afterClose.StatusCode);

        // A rejected subject stays behind the gate.
        using var scope = _factory.Services.CreateScope();
        var gate = scope.ServiceProvider.GetRequiredService<IApprovalGate>();
        var answer = await gate.IsApprovedAsync(ApprovalSubjectType.PaymentOrder, reference, CancellationToken.None);
        Assert.True(answer.Succeeded);
        Assert.False(answer.Value);

        // ...but the subject is NOT stuck: a rejection closed the instance, so a corrected
        // submission may open a fresh one for the same reference.
        var reopenResponse = await openerClient.PostAsJsonAsync(
            "/api/v1/approvals/instances",
            new OpenApprovalInstanceRequest(ApprovalSubjectType.PaymentOrder, reference),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, reopenResponse.StatusCode);
    }

    [Fact]
    public async Task At_most_one_approval_instance_is_in_progress_per_subject()
    {
        await CreateApprovalsUserAsync("wf.dup", "wf.dup@example.com", "Ouvreur double", null, ApprovalsRead, ApprovalsWrite);

        using var client = await _factory.CreateAuthenticatedClientAsync("wf.dup", Password);

        await EnsureActiveCircuitAsync();

        var reference = Guid.NewGuid().ToString();

        var first = await client.PostAsJsonAsync(
            "/api/v1/approvals/instances",
            new OpenApprovalInstanceRequest(ApprovalSubjectType.PaymentOrder, reference),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var duplicate = await client.PostAsJsonAsync(
            "/api/v1/approvals/instances",
            new OpenApprovalInstanceRequest(ApprovalSubjectType.PaymentOrder, reference),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task A_circuit_without_steps_cannot_be_activated()
    {
        await CreateApprovalsUserAsync("wf.writer", "wf.writer@example.com", "Configurateur nu", null, ApprovalsRead, ApprovalsWrite);

        using var client = await _factory.CreateAuthenticatedClientAsync("wf.writer", Password);

        var createResponse = await client.PostAsJsonAsync(
            "/api/v1/approvals/circuits",
            new CreateApprovalCircuitRequest(
                "CIRC-EMPTY",
                "Circuit sans etape",
                ApprovalSubjectType.PaymentOrder,
                Array.Empty<ApprovalStepRequest>()),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<ApprovalCircuitResponse>(RaqmiApiFactory.JsonOptions);
        Assert.False(created!.IsActive);

        var activateResponse = await client.PostAsync("/api/v1/approvals/circuits/CIRC-EMPTY/activate", content: null);
        Assert.Equal(HttpStatusCode.BadRequest, activateResponse.StatusCode);
        Assert.Contains("at least one step", await activateResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Configuring_circuits_requires_the_write_permission()
    {
        await CreateApprovalsUserAsync("wf.reader", "wf.reader@example.com", "Lecteur", RoleCatalog.Reader, ApprovalsRead);

        using var readerClient = await _factory.CreateAuthenticatedClientAsync("wf.reader", Password);

        // Reading is allowed...
        var listResponse = await readerClient.GetAsync("/api/v1/approvals/circuits?includeInactive=true");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        // ...writing is not: approvals.write (circuit configuration) is a distinct grant.
        var createResponse = await readerClient.PostAsJsonAsync(
            "/api/v1/approvals/circuits",
            new CreateApprovalCircuitRequest(
                "CIRC-FORBIDDEN",
                "Tentative sans droit",
                ApprovalSubjectType.PaymentOrder,
                new[] { new ApprovalStepRequest("Visa", RoleCatalog.Direction) }),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, createResponse.StatusCode);

        // And the pending queue is gated on approvals.decide, which the reader lacks.
        var pendingResponse = await readerClient.GetAsync("/api/v1/approvals/instances/pending");
        Assert.Equal(HttpStatusCode.Forbidden, pendingResponse.StatusCode);
    }

    /// <summary>
    /// A circuit may only demand a role that can DECIDE. cashier is a real system role that
    /// never receives approvals.decide, so a step demanding it would be undecidable for life
    /// (and the snapshot would freeze that dead end into every instance opened on it). The
    /// refusal happens server-side, at creation: it does not depend on the desktop's role picker
    /// offering the right list.
    /// </summary>
    [Fact]
    public async Task A_circuit_demanding_a_role_that_cannot_decide_is_refused_at_creation()
    {
        await CreateApprovalsUserAsync(
            "wf.roles", "wf.roles@example.com", "Configurateur roles", null, ApprovalsRead, ApprovalsWrite);

        using var client = await _factory.CreateAuthenticatedClientAsync("wf.roles", Password);

        var refused = await client.PostAsJsonAsync(
            "/api/v1/approvals/circuits",
            new CreateApprovalCircuitRequest(
                "CIRC-CASHIER",
                "Circuit indécidable",
                ApprovalSubjectType.PaymentOrder,
                new[]
                {
                    new ApprovalStepRequest("Visa du responsable", RoleCatalog.UnitManager),
                    new ApprovalStepRequest("Visa de la caisse", RoleCatalog.Cashier)
                }),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);

        // Refused as a whole: no half-created circuit is left behind.
        var lookup = await client.GetAsync("/api/v1/approvals/circuits/CIRC-CASHIER");
        Assert.Equal(HttpStatusCode.NotFound, lookup.StatusCode);

        // reader is refused for the same reason...
        var refusedReader = await client.PostAsJsonAsync(
            "/api/v1/approvals/circuits",
            new CreateApprovalCircuitRequest(
                "CIRC-READER",
                "Circuit indécidable (lecture)",
                ApprovalSubjectType.PaymentOrder,
                new[] { new ApprovalStepRequest("Visa lecture", RoleCatalog.Reader) }),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, refusedReader.StatusCode);

        // ...while a decider role is accepted (the circuit stays inactive: another one is active).
        var accepted = await client.PostAsJsonAsync(
            "/api/v1/approvals/circuits",
            new CreateApprovalCircuitRequest(
                "CIRC-DECIDERS",
                "Circuit décidable",
                ApprovalSubjectType.PaymentOrder,
                new[] { new ApprovalStepRequest("Visa du contrôle", RoleCatalog.ExploitationControl) }),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
    }

    /// <summary>
    /// The opening-time snapshot, proven THROUGH THE DATABASE rather than between two objects of
    /// the same process: an instance is opened, the circuit it came from is then rewritten in
    /// base (different labels, different required roles, fewer steps), and the instance is
    /// reloaded from scratch by the API. Its steps must be exactly those of the opening, and the
    /// decision must still obey them - the circuit edit reaches new instances only.
    /// </summary>
    [Fact]
    public async Task An_instance_reloaded_from_the_database_still_carries_the_steps_of_its_opening()
    {
        await CreateApprovalsUserAsync(
            "wf.snap.opener", "wf.snap.opener@example.com", "Ouvreur snapshot", null, ApprovalsRead, ApprovalsWrite);
        await CreateApprovalsUserAsync(
            "wf.snap.decider", "wf.snap.decider@example.com", "Chef snapshot", RoleCatalog.UnitManager, ApprovalsRead, ApprovalsDecide);

        using var openerClient = await _factory.CreateAuthenticatedClientAsync("wf.snap.opener", Password);
        using var deciderClient = await _factory.CreateAuthenticatedClientAsync("wf.snap.decider", Password);

        await EnsureActiveCircuitAsync();

        var reference = Guid.NewGuid().ToString();

        var openResponse = await openerClient.PostAsJsonAsync(
            "/api/v1/approvals/instances",
            new OpenApprovalInstanceRequest(ApprovalSubjectType.PaymentOrder, reference),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, openResponse.StatusCode);

        var opened = await openResponse.Content.ReadFromJsonAsync<ApprovalInstanceResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(opened);

        var openedSteps = opened!.Steps
            .OrderBy(step => step.Rank)
            .Select(step => (step.Rank, step.Label, step.RequiredRole))
            .ToArray();

        Assert.Equal(2, openedSteps.Length);

        try
        {
            // The circuit is REWRITTEN IN BASE, behind the instance's back.
            using (var scope = _factory.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();

                var circuit = await dbContext.Set<ApprovalCircuit>()
                    .Include(current => current.Steps)
                    .SingleAsync(current => current.Code == SharedCircuitCode);

                circuit.ReplaceSteps(new[]
                {
                    new ApprovalStep("Visa unique du contrôle", RoleCatalog.ExploitationControl)
                });

                await dbContext.SaveChangesAsync();
            }

            // The circuit really did change - the read below is not a no-op.
            var circuitAfterEdit = await openerClient.GetFromJsonAsync<ApprovalCircuitResponse>(
                $"/api/v1/approvals/circuits/{SharedCircuitCode}",
                RaqmiApiFactory.JsonOptions);

            Assert.NotNull(circuitAfterEdit);
            var editedStep = Assert.Single(circuitAfterEdit!.Steps);
            Assert.Equal(RoleCatalog.ExploitationControl, editedStep.RequiredRole);

            // The instance is reloaded from the database by a NEW request, on a fresh scope.
            var reloaded = await openerClient.GetFromJsonAsync<ApprovalInstanceResponse>(
                $"/api/v1/approvals/instances/{opened.Id}",
                RaqmiApiFactory.JsonOptions);

            Assert.NotNull(reloaded);
            Assert.Equal(
                openedSteps,
                reloaded!.Steps.OrderBy(step => step.Rank).Select(step => (step.Rank, step.Label, step.RequiredRole)).ToArray());

            Assert.Equal(RoleCatalog.UnitManager, reloaded.CurrentStepRequiredRole);

            // And the snapshot governs the DECISION too: the role the circuit now demands
            // (exploitation.control) is irrelevant to this instance, which still asks for the
            // unit manager it was opened with.
            var decided = await ApproveAsync(deciderClient, opened.Id, "Visa conforme au circuit d'origine.");

            Assert.Equal(2, decided.CurrentRank);
            Assert.Equal(RoleCatalog.Direction, decided.CurrentStepRequiredRole);
        }
        finally
        {
            // The circuit is shared by the whole class: put its two steps back.
            using var scope = _factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();

            var circuit = await dbContext.Set<ApprovalCircuit>()
                .Include(current => current.Steps)
                .SingleAsync(current => current.Code == SharedCircuitCode);

            circuit.ReplaceSteps(new[]
            {
                new ApprovalStep("Visa du responsable d'unite", RoleCatalog.UnitManager),
                new ApprovalStep("Signature de la direction", RoleCatalog.Direction)
            });

            await dbContext.SaveChangesAsync();
        }
    }

    /// <summary>
    /// The one ACTIVE circuit of this test database (the module allows a single active circuit
    /// per subject type): two steps, unit.manager then direction. Created directly through the
    /// DbContext so the tests that only consume the workflow do not incidentally re-test the
    /// configuration endpoints. Idempotent whatever order xunit runs the class's tests in.
    /// </summary>
    private async Task<ApprovalCircuit> EnsureActiveCircuitAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();

        var existing = await dbContext.Set<ApprovalCircuit>()
            .Include(circuit => circuit.Steps)
            .SingleOrDefaultAsync(circuit => circuit.Code == SharedCircuitCode);

        if (existing is not null)
        {
            return existing;
        }

        var circuit = new ApprovalCircuit(SharedCircuitCode, "Validation des ordres de paiement", ApprovalSubjectType.PaymentOrder);

        circuit.ReplaceSteps(new[]
        {
            new ApprovalStep("Visa du responsable d'unite", RoleCatalog.UnitManager),
            new ApprovalStep("Signature de la direction", RoleCatalog.Direction)
        });

        circuit.Activate();
        circuit.MarkCreated("tests", DateTimeOffset.UtcNow);

        dbContext.Set<ApprovalCircuit>().Add(circuit);
        await dbContext.SaveChangesAsync();

        return circuit;
    }

    private async Task<ApprovalInstanceResponse> ApproveAsync(HttpClient client, Guid instanceId, string? comment)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/v1/approvals/instances/{instanceId}/approve",
            new DecideApprovalRequest(comment),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var instance = await response.Content.ReadFromJsonAsync<ApprovalInstanceResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(instance);

        return instance!;
    }

    private static async Task<Guid[]> GetPendingIdsAsync(HttpClient client)
    {
        var pending = await client.GetFromJsonAsync<ApprovalInstanceResponse[]>(
            "/api/v1/approvals/instances/pending",
            RaqmiApiFactory.JsonOptions);

        Assert.NotNull(pending);

        return pending!.Select(instance => instance.Id).ToArray();
    }

    /// <summary>
    /// Creates a user carrying an optional REAL system role (what the circuit steps demand: the
    /// role names travel as role claims in the JWT) plus a single-purpose test role holding
    /// exactly the approvals permission keys the test needs (what the authorization policies
    /// demand).
    /// </summary>
    private async Task CreateApprovalsUserAsync(
        string userName,
        string email,
        string displayName,
        string? systemRoleName,
        params string[] permissionKeys)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var permissions = await dbContext.Permissions
            .Where(permission => permissionKeys.Contains(permission.Key))
            .ToArrayAsync();

        Assert.True(
            permissions.Length == permissionKeys.Length,
            "Approvals permission keys are missing from the seeded PermissionCatalog: " +
            string.Join(", ", permissionKeys.Except(permissions.Select(permission => permission.Key))));

        var testRole = new Role(
            $"test.approvals.{Guid.NewGuid():N}",
            "Approvals test role",
            "Role dedicated to approvals endpoint tests.");

        foreach (var permission in permissions)
        {
            testRole.GrantPermission(permission, DateTimeOffset.UtcNow);
        }

        dbContext.Roles.Add(testRole);

        var user = new User(userName, email, displayName, passwordHasher.Hash(Password), mustChangePassword: false);
        user.AssignRole(testRole, DateTimeOffset.UtcNow);

        if (systemRoleName is not null)
        {
            var systemRole = await dbContext.Roles.SingleAsync(role => role.Name == systemRoleName);
            user.AssignRole(systemRole, DateTimeOffset.UtcNow);
        }

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
    }
}
