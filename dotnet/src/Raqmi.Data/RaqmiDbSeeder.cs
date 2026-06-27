using Microsoft.EntityFrameworkCore;
using Raqmi.Data.Entities;
using Raqmi.Data.Entities.Finance;
using Raqmi.Data.Entities.Hr;
using Raqmi.Data.Entities.Stocks;
using Raqmi.Licensing;

namespace Raqmi.Data;

public static class RaqmiDbSeeder
{
    public const string DemoTenantId = "demo-tenant-001";
    public const string DemoSiteId = "demo-site-001";
    public const string DemoSite2Id = "demo-site-002";
    public const string DemoUserId = "demo-user-001";

    public static async Task SeedAsync(RaqmiDbContext db, CancellationToken ct = default)
    {
        if (await db.Tenants.AnyAsync(ct)) return;

        var tenant = new Tenant
        {
            Id = DemoTenantId,
            Code = "demo-hotel",
            Name = "Hotel Demo Raqmi",
            Status = "ACTIVE",
        };
        var site = new Site
        {
            Id = DemoSiteId,
            TenantId = DemoTenantId,
            Code = "main",
            Name = "Hotel Demo — Siège",
            City = "Alger",
        };
        var user = new User
        {
            Id = DemoUserId,
            TenantId = DemoTenantId,
            Email = "admin@demo.raqmi.local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("demo1234"),
            FullName = "Administrateur Demo",
            RoleCode = "admin",
        };

        var site2 = new Site
        {
            Id = DemoSite2Id,
            TenantId = DemoTenantId,
            Code = "annexe",
            Name = "Annexe Plage",
            City = "Tipaza",
        };

        db.Tenants.Add(tenant);
        db.Sites.Add(site);
        db.Sites.Add(site2);
        db.Users.Add(user);
        db.UserSiteAssignments.Add(new UserSiteAssignment { UserId = DemoUserId, SiteId = DemoSiteId });
        db.UserSiteAssignments.Add(new UserSiteAssignment { UserId = DemoUserId, SiteId = DemoSite2Id });

        var pack = LicensePacks.All.First(p => p.Kind == LicenseKind.Professional);

        db.DailyRevenueEntries.Add(new DailyRevenueEntry
        {
            TenantId = DemoTenantId,
            SiteId = DemoSiteId,
            BusinessDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Amount = 125000,
            Category = "restaurant",
            Status = "validated",
            Notes = "Recette demo",
        });

        var invoice = new Invoice
        {
            TenantId = DemoTenantId,
            Number = "FAC-2026-001",
            ClientName = "Client Demo",
            Status = "draft",
            IssueDate = DateOnly.FromDateTime(DateTime.UtcNow),
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
        };
        invoice.Lines.Add(new InvoiceLine { Description = "Hébergement", Quantity = 2, UnitPrice = 15000, TaxRate = 19 });
        invoice.TotalAmount = invoice.Lines.Sum(l => l.Quantity * l.UnitPrice * (1 + l.TaxRate / 100));
        db.Invoices.Add(invoice);

        db.TreasuryMovements.Add(new TreasuryMovement
        {
            TenantId = DemoTenantId,
            SiteId = DemoSiteId,
            Type = "in",
            Account = "cash",
            Amount = 50000,
            MovementDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Label = "Encaissement demo",
        });

        var employeeId = Guid.NewGuid().ToString();
        var employee = new Employee
        {
            Id = employeeId,
            TenantId = DemoTenantId,
            Matricule = "EMP-001",
            FirstName = "Karim",
            LastName = "Benali",
            SiteId = DemoSiteId,
            Department = "Réception",
            HireDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-1)),
        };
        employee.Contracts.Add(new EmploymentContract
        {
            ContractType = "cdi",
            StartDate = employee.HireDate,
            BaseSalary = 65000,
        });
        db.Employees.Add(employee);

        db.AttendanceRecords.Add(new AttendanceRecord
        {
            TenantId = DemoTenantId,
            EmployeeId = employee.Id,
            SiteId = DemoSiteId,
            WorkDate = DateOnly.FromDateTime(DateTime.UtcNow),
            CheckIn = new TimeOnly(8, 0),
            CheckOut = new TimeOnly(17, 0),
        });

        var product = new Product
        {
            TenantId = DemoTenantId,
            Code = "PROD-001",
            Name = "Serviette de bain",
            Unit = "u",
            MinStockLevel = 50,
        };
        db.Products.Add(product);
        var productId = product.Id;
        db.StockMovements.Add(new StockMovement
        {
            TenantId = DemoTenantId,
            ProductId = productId,
            SiteId = DemoSiteId,
            Type = "in",
            Quantity = 200,
            MovementDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Reference = "INIT",
        });

        await db.SaveChangesAsync(ct);
    }
}
