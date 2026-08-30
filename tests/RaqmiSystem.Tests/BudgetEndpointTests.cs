using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaqmiSystem.Application.Budgeting;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Budgeting;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Domain.Revenue;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Tests;

/// <summary>
/// Full-HTTP integration coverage for the budgeting module (yearly plans, monthly targets, and
/// the budget-versus-actual variance report). Each test provisions its own dedicated role
/// carrying exactly the budget permission keys it needs (seeded from PermissionCatalog by
/// SecuritySeeder during factory startup), so the per-permission authorization policies
/// registered in Program.cs are enforced for real.
/// </summary>
public sealed class BudgetEndpointTests : IClassFixture<RaqmiApiFactory>
{
    private const string Password = "Correct-Horse-Battery-42!";

    private const string BudgetRead = "budget.read";
    private const string BudgetWrite = "budget.write";
    private const string BudgetApprove = "budget.approve";

    private readonly RaqmiApiFactory _factory;

    public BudgetEndpointTests(RaqmiApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Budget_plan_is_frozen_once_approved_and_approval_needs_its_own_permission()
    {
        var hotelUnitCode = await _factory.CreateHotelUnitAsync("BUDLIF", "Budget Lifecycle Hotel");

        await CreateBudgetUserAsync(
            "budget.writer",
            "budget.writer@example.com",
            "Budget Writer",
            BudgetRead, BudgetWrite);

        await CreateBudgetUserAsync(
            "budget.approver",
            "budget.approver@example.com",
            "Budget Approver",
            BudgetRead, BudgetApprove);

        using var writerClient = await _factory.CreateAuthenticatedClientAsync("budget.writer", Password);

        var createResponse = await writerClient.PostAsJsonAsync(
            "/api/v1/budget/plans",
            new CreateBudgetPlanRequest(
                Year: 2026,
                HotelUnitCode: hotelUnitCode,
                Label: "Budget 2026",
                Lines: new[]
                {
                    new BudgetLineRequest(1, BudgetCategory.Accommodation, 100_000.00m),
                    new BudgetLineRequest(1, BudgetCategory.Food, 50_000.00m)
                }),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var plan = await createResponse.Content.ReadFromJsonAsync<BudgetPlanResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(plan);
        Assert.Equal(BudgetStatus.Draft, plan!.Status);
        Assert.True(plan.CanEdit);
        Assert.Equal(150_000.00m, plan.TotalTarget);
        Assert.Equal(2, plan.Lines.Count);

        // A draft is freely editable: one more cell, then one dropped again.
        var addLineResponse = await writerClient.PostAsJsonAsync(
            $"/api/v1/budget/plans/{plan.Id}/lines",
            new BudgetLineRequest(2, BudgetCategory.Beverage, 25_000.00m),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, addLineResponse.StatusCode);

        var withExtraLine = await addLineResponse.Content.ReadFromJsonAsync<BudgetPlanResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(withExtraLine);
        Assert.Equal(3, withExtraLine!.Lines.Count);

        var beverageLineId = withExtraLine.Lines.Single(line => line.Category == BudgetCategory.Beverage).Id;

        var deleteResponse = await writerClient.DeleteAsync(
            $"/api/v1/budget/plans/{plan.Id}/lines/{beverageLineId}");

        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        // The writer has budget.write but not budget.approve: approving a budget engages the
        // direction and is a distinct act.
        var forbiddenApprove = await writerClient.PostAsync($"/api/v1/budget/plans/{plan.Id}/approve", content: null);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenApprove.StatusCode);

        using var approverClient = await _factory.CreateAuthenticatedClientAsync("budget.approver", Password);

        var approveResponse = await approverClient.PostAsync($"/api/v1/budget/plans/{plan.Id}/approve", content: null);
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);

        var approved = await approveResponse.Content.ReadFromJsonAsync<BudgetPlanResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(approved);
        Assert.Equal(BudgetStatus.Approved, approved!.Status);
        Assert.False(approved.CanEdit);
        Assert.Equal("budget.approver", approved.ApprovedBy);
        Assert.NotNull(approved.ApprovedAt);
        Assert.Equal(150_000.00m, approved.TotalTarget);

        // From here on the budget is the frozen reference every variance is measured against:
        // renaming it, retargeting a cell or replacing the grid are all refused.
        var refusedRename = await writerClient.PutAsJsonAsync(
            $"/api/v1/budget/plans/{plan.Id}",
            new UpdateBudgetPlanRequest("Budget 2026 revise"),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, refusedRename.StatusCode);

        var refusedLine = await writerClient.PostAsJsonAsync(
            $"/api/v1/budget/plans/{plan.Id}/lines",
            new BudgetLineRequest(1, BudgetCategory.Accommodation, 1.00m),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, refusedLine.StatusCode);

        var refusedReplace = await writerClient.PutAsJsonAsync(
            $"/api/v1/budget/plans/{plan.Id}/lines",
            new ReplaceBudgetLinesRequest(new[]
            {
                new BudgetLineRequest(1, BudgetCategory.Accommodation, 1.00m)
            }),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, refusedReplace.StatusCode);

        var reread = await writerClient.GetFromJsonAsync<BudgetPlanResponse>(
            $"/api/v1/budget/plans/{plan.Id}",
            RaqmiApiFactory.JsonOptions);

        Assert.NotNull(reread);
        Assert.Equal("Budget 2026", reread!.Label);
        Assert.Equal(150_000.00m, reread.TotalTarget);

        // A second plan for the same (year, unit) is refused: a unit cannot be steered against
        // two competing budgets for the same exercise.
        var duplicateResponse = await writerClient.PostAsJsonAsync(
            "/api/v1/budget/plans",
            new CreateBudgetPlanRequest(2026, hotelUnitCode, "Budget 2026 bis"),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
    }

    [Fact]
    public async Task Empty_budget_plan_cannot_be_approved_and_a_cell_cannot_be_targeted_twice()
    {
        var hotelUnitCode = await _factory.CreateHotelUnitAsync("BUDGRD", "Budget Guard Hotel");

        await CreateBudgetUserAsync(
            "budget.guard",
            "budget.guard@example.com",
            "Budget Guard",
            BudgetRead, BudgetWrite, BudgetApprove);

        using var client = await _factory.CreateAuthenticatedClientAsync("budget.guard", Password);

        var createResponse = await client.PostAsJsonAsync(
            "/api/v1/budget/plans",
            new CreateBudgetPlanRequest(2027, hotelUnitCode, "Budget 2027"),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var plan = await createResponse.Content.ReadFromJsonAsync<BudgetPlanResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(plan);
        Assert.Empty(plan!.Lines);

        // An empty budget commits to nothing while looking like a decision.
        var refusedApprove = await client.PostAsync($"/api/v1/budget/plans/{plan.Id}/approve", content: null);
        Assert.Equal(HttpStatusCode.BadRequest, refusedApprove.StatusCode);

        // Two different targets for the same (month, category) cell is a caller mistake, not a
        // last-one-wins situation.
        var duplicateCell = await client.PutAsJsonAsync(
            $"/api/v1/budget/plans/{plan.Id}/lines",
            new ReplaceBudgetLinesRequest(new[]
            {
                new BudgetLineRequest(4, BudgetCategory.Accommodation, 100_000.00m),
                new BudgetLineRequest(4, BudgetCategory.Accommodation, 250_000.00m)
            }),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, duplicateCell.StatusCode);

        var outOfRangeMonth = await client.PutAsJsonAsync(
            $"/api/v1/budget/plans/{plan.Id}/lines",
            new ReplaceBudgetLinesRequest(new[]
            {
                new BudgetLineRequest(13, BudgetCategory.Accommodation, 100_000.00m)
            }),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, outOfRangeMonth.StatusCode);

        var stillEmpty = await client.GetFromJsonAsync<BudgetPlanResponse>(
            $"/api/v1/budget/plans/{plan.Id}",
            RaqmiApiFactory.JsonOptions);

        Assert.NotNull(stillEmpty);
        Assert.Empty(stillEmpty!.Lines);
    }

    [Fact]
    public async Task Variance_confronts_the_budget_with_validated_revenue_only()
    {
        var hotelUnitCode = await _factory.CreateHotelUnitAsync("BUDVAR", "Budget Variance Hotel");

        await CreateBudgetUserAsync(
            "budget.analyst",
            "budget.analyst@example.com",
            "Budget Analyst",
            BudgetRead, BudgetWrite);

        using var client = await _factory.CreateAuthenticatedClientAsync("budget.analyst", Password);

        var createResponse = await client.PostAsJsonAsync(
            "/api/v1/budget/plans",
            new CreateBudgetPlanRequest(
                Year: 2026,
                HotelUnitCode: hotelUnitCode,
                Label: "Budget 2026",
                Lines: new[]
                {
                    new BudgetLineRequest(1, BudgetCategory.Accommodation, 100_000.00m),
                    new BudgetLineRequest(1, BudgetCategory.Food, 50_000.00m),
                    new BudgetLineRequest(2, BudgetCategory.Accommodation, 200_000.00m)
                }),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        await SeedDailyRevenueAsync(hotelUnitCode, new DateOnly(2026, 1, 5), 60_000.00m, 55_000.00m, 0m, 0m, validate: true);
        await SeedDailyRevenueAsync(hotelUnitCode, new DateOnly(2026, 1, 20), 30_000.00m, 0m, 1_000.00m, 0m, validate: true);

        // Left at the Draft status: an uncontrolled keystroke is not money the establishment made,
        // and these deliberately enormous amounts would be impossible to miss if it leaked in.
        await SeedDailyRevenueAsync(
            hotelUnitCode,
            new DateOnly(2026, 1, 25),
            999_999.00m, 999_999.00m, 999_999.00m, 999_999.00m,
            validate: false);

        var report = await client.GetFromJsonAsync<BudgetVarianceResponse>(
            $"/api/v1/budget/variance?year=2026&hotelUnitCode={hotelUnitCode}",
            RaqmiApiFactory.JsonOptions);

        Assert.NotNull(report);
        Assert.Equal(2026, report!.Year);
        Assert.Equal(hotelUnitCode, report.HotelUnitCode);
        Assert.Equal(BudgetStatus.Draft, report.PlanStatus);
        Assert.Equal(12, report.Months.Count);

        var january = report.Months.Single(month => month.Month == 1);

        var accommodation = january.Categories.Single(row => row.Category == BudgetCategory.Accommodation);
        Assert.Equal(100_000.00m, accommodation.BudgetAmount);
        Assert.Equal(90_000.00m, accommodation.ActualAmount);
        Assert.Equal(-10_000.00m, accommodation.VarianceAmount);
        Assert.Equal(-10.00m, accommodation.VariancePercentage);

        var food = january.Categories.Single(row => row.Category == BudgetCategory.Food);
        Assert.Equal(50_000.00m, food.BudgetAmount);
        Assert.Equal(55_000.00m, food.ActualAmount);
        Assert.Equal(5_000.00m, food.VarianceAmount);
        Assert.Equal(10.00m, food.VariancePercentage);

        // Nothing was budgeted for beverage: the gap in value is reported, the percentage is not.
        var beverage = january.Categories.Single(row => row.Category == BudgetCategory.Beverage);
        Assert.Equal(0m, beverage.BudgetAmount);
        Assert.Equal(1_000.00m, beverage.ActualAmount);
        Assert.Equal(1_000.00m, beverage.VarianceAmount);
        Assert.Null(beverage.VariancePercentage);

        Assert.Equal(150_000.00m, january.BudgetAmount);
        Assert.Equal(146_000.00m, january.ActualAmount);
        Assert.Equal(-4_000.00m, january.VarianceAmount);
        Assert.Equal(-2.67m, january.VariancePercentage);

        Assert.Equal(350_000.00m, report.BudgetAmount);
        Assert.Equal(146_000.00m, report.ActualAmount);
        Assert.Equal(-204_000.00m, report.VarianceAmount);

        // Validating the draft entry afterwards is exactly what makes it count.
        await ValidateDailyRevenueAsync(hotelUnitCode, new DateOnly(2026, 1, 25));

        var afterValidation = await client.GetFromJsonAsync<BudgetVarianceResponse>(
            $"/api/v1/budget/variance?year=2026&hotelUnitCode={hotelUnitCode}&month=1",
            RaqmiApiFactory.JsonOptions);

        Assert.NotNull(afterValidation);
        var januaryOnly = Assert.Single(afterValidation!.Months);
        Assert.Equal(1, januaryOnly.Month);
        Assert.Equal(150_000.00m, januaryOnly.BudgetAmount);
        Assert.Equal(146_000.00m + (4 * 999_999.00m), januaryOnly.ActualAmount);

        // A year with no plan at all is reported as such, rather than as a unit wildly beating a
        // budget of zero.
        var missingPlan = await client.GetAsync(
            $"/api/v1/budget/variance?year=2029&hotelUnitCode={hotelUnitCode}");

        Assert.Equal(HttpStatusCode.NotFound, missingPlan.StatusCode);

        var missingUnit = await client.GetAsync("/api/v1/budget/variance?year=2026&hotelUnitCode=NOSUCHUNIT");
        Assert.Equal(HttpStatusCode.NotFound, missingUnit.StatusCode);

        var badMonth = await client.GetAsync(
            $"/api/v1/budget/variance?year=2026&hotelUnitCode={hotelUnitCode}&month=13");

        Assert.Equal(HttpStatusCode.BadRequest, badMonth.StatusCode);
    }

    /// <summary>
    /// Writes a daily revenue entry straight through the DbContext (bypassing the revenue module's
    /// endpoints and their permissions), optionally carrying it all the way to the Validated
    /// status through the entity's own workflow.
    /// </summary>
    private async Task SeedDailyRevenueAsync(
        string hotelUnitCode,
        DateOnly businessDate,
        decimal accommodation,
        decimal food,
        decimal beverage,
        decimal other,
        bool validate)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();

        var revenue = new DailyRevenue(businessDate, hotelUnitCode, accommodation, food, beverage, other);
        revenue.MarkCreated("tests", DateTimeOffset.UtcNow);

        if (validate)
        {
            revenue.Submit("tests", DateTimeOffset.UtcNow);
            revenue.Validate("tests", DateTimeOffset.UtcNow);
        }

        dbContext.Set<DailyRevenue>().Add(revenue);
        await dbContext.SaveChangesAsync();
    }

    private async Task ValidateDailyRevenueAsync(string hotelUnitCode, DateOnly businessDate)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();

        var revenue = await dbContext.Set<DailyRevenue>()
            .SingleAsync(current => current.HotelUnitCode == hotelUnitCode && current.BusinessDate == businessDate);

        revenue.Submit("tests", DateTimeOffset.UtcNow);
        revenue.Validate("tests", DateTimeOffset.UtcNow);

        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Creates a user attached to a fresh single-purpose role carrying exactly the given budget
    /// permission keys. The permissions themselves must already exist (SecuritySeeder seeds every
    /// PermissionCatalog entry during factory initialization) - the assertion below fails fast
    /// with a clear signal if the budget keys have not been added to PermissionCatalog yet.
    /// </summary>
    private async Task CreateBudgetUserAsync(
        string userName,
        string email,
        string displayName,
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
            "Budget permission keys are missing from the seeded PermissionCatalog: " +
            string.Join(", ", permissionKeys.Except(permissions.Select(permission => permission.Key))));

        var role = new Role(
            $"test.budget.{Guid.NewGuid():N}",
            "Budget test role",
            "Role dedicated to budgeting endpoint tests.");

        foreach (var permission in permissions)
        {
            role.GrantPermission(permission, DateTimeOffset.UtcNow);
        }

        dbContext.Roles.Add(role);

        var user = new User(userName, email, displayName, passwordHasher.Hash(Password), mustChangePassword: false);
        user.AssignRole(role, DateTimeOffset.UtcNow);
        dbContext.Users.Add(user);

        await dbContext.SaveChangesAsync();
    }
}
