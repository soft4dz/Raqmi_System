namespace Raqmi.Server;

public static class DemoBusinessStore
{
    private static readonly List<DailyRevenueRecord> DailyRevenues =
    [
        new("rev-001", DemoSites.MainSiteId, DateOnly.FromDateTime(DateTime.UtcNow), 125000, "restaurant", "validated", "Recette déjeuner"),
        new("rev-002", DemoSites.AnnexeSiteId, DateOnly.FromDateTime(DateTime.UtcNow), 48000, "bar", "draft", "Annexe plage"),
    ];

    private static readonly List<InvoiceRecord> Invoices =
    [
        new("inv-001", "FAC-2026-001", "Client Demo", "draft", DateOnly.FromDateTime(DateTime.UtcNow),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)), 35700, DemoSites.MainSiteId,
            [new InvoiceLineRecord("Hébergement", 2, 15000, 19)]),
    ];

    private static readonly List<TreasuryRecord> Treasury =
    [
        new("tr-001", DemoSites.MainSiteId, "in", "cash", 50000, DateOnly.FromDateTime(DateTime.UtcNow), "Encaissement demo"),
    ];

    private static readonly List<EmployeeRecord> Employees =
    [
        new("emp-001", "EMP-001", "Karim", "Benali", DemoSites.MainSiteId, "Réception", "active", DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-1))),
        new("emp-002", "EMP-002", "Samira", "Khelifi", DemoSites.AnnexeSiteId, "Restauration", "active", DateOnly.FromDateTime(DateTime.UtcNow)),
    ];

    private static readonly List<ContractRecord> Contracts =
    [
        new("ctr-001", "emp-001", "cdi", Employees[0].HireDate, null, 65000),
    ];

    private static readonly List<AttendanceRecord> Attendance =
    [
        new("att-001", "emp-001", DemoSites.MainSiteId, DateOnly.FromDateTime(DateTime.UtcNow), "08:00", "17:00", null),
    ];

    private static readonly List<ProductRecord> Products =
    [
        new("prod-001", "PROD-001", "Serviette de bain", "u", 50, true),
        new("prod-002", "PROD-002", "Shampoing accueil", "u", 100, true),
    ];

    private static readonly List<StockMovementRecord> StockMovements =
    [
        new("sm-001", "prod-001", DemoSites.MainSiteId, "in", 200, DateOnly.FromDateTime(DateTime.UtcNow), "INIT"),
    ];

    private static readonly List<InventoryRecord> Inventories =
    [
        new("inv-s-001", DemoSites.MainSiteId, DateOnly.FromDateTime(DateTime.UtcNow), "open"),
    ];

    private static readonly List<DocumentRecord> Documents =
    [
        new("doc-001", "contrat-demo.pdf", "application/pdf", 245000, DateTime.UtcNow, "ged", DemoSites.MainSiteId),
    ];

    public static IReadOnlyList<object> ListDailyRevenue(string? siteId, string userId, string roleCode) =>
        FilterBySite(DailyRevenues, siteId, userId, roleCode).Select(ToDto).Cast<object>().ToList();

    public static DailyRevenueRecord CreateDailyRevenue(string? siteId, decimal amount, string? category, string? notes, string? userId)
    {
        var entry = new DailyRevenueRecord(Guid.NewGuid().ToString(), ResolveSite(siteId), DateOnly.FromDateTime(DateTime.UtcNow), amount, category ?? "general", "draft", notes);
        DailyRevenues.Insert(0, entry);
        DemoAuditStore.Add("create", "daily_revenue", "DailyRevenueEntry", entry.Id, "Recette créée", userId);
        return entry;
    }

    public static DailyRevenueRecord? ValidateDailyRevenue(string id)
    {
        var index = DailyRevenues.FindIndex(x => x.Id == id);
        if (index < 0) return null;
        var updated = DailyRevenues[index] with { Status = "validated" };
        DailyRevenues[index] = updated;
        return updated;
    }

    public static IReadOnlyList<object> ListInvoices(string? siteId, string userId, string roleCode) =>
        FilterBySite(Invoices, siteId, userId, roleCode).Select(ToInvoiceDto).Cast<object>().ToList();

    public static InvoiceRecord CreateInvoice(string? siteId, string? clientName, string? number, List<InvoiceLineRecord>? lines, string? userId)
    {
        var lineItems = lines ?? [new InvoiceLineRecord("Prestation", 1, 10000, 19)];
        var total = lineItems.Sum(l => l.Quantity * l.UnitPrice * (1 + l.TaxRate / 100));
        var invoice = new InvoiceRecord(Guid.NewGuid().ToString(), number ?? $"FAC-{DateTime.UtcNow:yyyyMMdd-HHmmss}", clientName ?? "Client", "draft",
            DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)), total, ResolveSite(siteId), lineItems);
        Invoices.Insert(0, invoice);
        DemoAuditStore.Add("create", "billing", "Invoice", invoice.Id, $"Facture {invoice.Number}", userId);
        return invoice;
    }

    public static InvoiceRecord? PatchInvoice(string id, string? status, string? clientName)
    {
        var index = Invoices.FindIndex(x => x.Id == id);
        if (index < 0) return null;
        var current = Invoices[index];
        Invoices[index] = current with { Status = status ?? current.Status, ClientName = clientName ?? current.ClientName };
        return Invoices[index];
    }

    public static (IReadOnlyList<object> Items, decimal Balance) ListTreasury(string? siteId, string userId, string roleCode)
    {
        var items = FilterBySite(Treasury, siteId, userId, roleCode);
        return (items.Select(ToTreasuryDto).Cast<object>().ToList(), items.Sum(x => x.Type == "in" ? x.Amount : -x.Amount));
    }

    public static TreasuryRecord CreateTreasury(string? siteId, string? type, string? account, decimal amount, string? label)
    {
        var movement = new TreasuryRecord(Guid.NewGuid().ToString(), ResolveSite(siteId), type ?? "in", account ?? "cash", amount, DateOnly.FromDateTime(DateTime.UtcNow), label ?? "Mouvement");
        Treasury.Insert(0, movement);
        return movement;
    }

    public static IReadOnlyList<object> ListEmployees(string? siteId, string userId, string roleCode, string? search)
    {
        var items = FilterBySite(Employees, siteId, userId, roleCode);
        if (!string.IsNullOrWhiteSpace(search))
        {
            items = items.Where(e => e.FirstName.Contains(search, StringComparison.OrdinalIgnoreCase) || e.LastName.Contains(search, StringComparison.OrdinalIgnoreCase) || e.Matricule.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        return items.Select(ToEmployeeDto).Cast<object>().ToList();
    }

    public static EmployeeRecord CreateEmployee(string? siteId, string? firstName, string? lastName, string? department, string? matricule, string? userId)
    {
        var employee = new EmployeeRecord(Guid.NewGuid().ToString(), matricule ?? $"EMP-{Guid.NewGuid().ToString()[..6]}", firstName ?? string.Empty, lastName ?? string.Empty, ResolveSite(siteId), department ?? string.Empty, "active", DateOnly.FromDateTime(DateTime.UtcNow));
        Employees.Add(employee);
        DemoAuditStore.Add("create", "hr", "Employee", employee.Id, $"Employé {employee.FirstName} {employee.LastName}", userId);
        return employee;
    }

    public static IReadOnlyList<object> ListContracts(string employeeId) => Contracts.Where(c => c.EmployeeId == employeeId).Select(ToContractDto).Cast<object>().ToList();

    public static ContractRecord CreateContract(string employeeId, string? contractType, decimal baseSalary, DateOnly? startDate)
    {
        var contract = new ContractRecord(Guid.NewGuid().ToString(), employeeId, contractType ?? "cdi", startDate ?? DateOnly.FromDateTime(DateTime.UtcNow), null, baseSalary);
        Contracts.Add(contract);
        return contract;
    }

    public static IReadOnlyList<object> ListAttendance(string? siteId, string userId, string roleCode, DateOnly? date)
    {
        var d = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        return FilterBySite(Attendance, siteId, userId, roleCode).Where(a => a.WorkDate == d).Select(ToAttendanceDto).Cast<object>().ToList();
    }

    public static AttendanceRecord CreateAttendance(string? siteId, string? employeeId, DateOnly? workDate, string? checkIn, string? checkOut, string? notes)
    {
        var record = new AttendanceRecord(Guid.NewGuid().ToString(), employeeId ?? Employees.FirstOrDefault()?.Id ?? string.Empty, ResolveSite(siteId), workDate ?? DateOnly.FromDateTime(DateTime.UtcNow), checkIn, checkOut, notes);
        Attendance.Insert(0, record);
        return record;
    }

    public static IReadOnlyList<object> ListProducts() => Products.Where(p => p.Active).Select(ToProductDto).Cast<object>().ToList();

    public static ProductRecord CreateProduct(string? code, string? name, string? unit, decimal minStockLevel)
    {
        var product = new ProductRecord(Guid.NewGuid().ToString(), code ?? $"P-{Guid.NewGuid().ToString()[..6]}", name ?? string.Empty, unit ?? "u", minStockLevel, true);
        Products.Add(product);
        return product;
    }

    public static (IReadOnlyList<object> Items, IReadOnlyList<object> StockByProduct) ListStockMovements(string? siteId, string userId, string roleCode, string? productId)
    {
        var items = FilterBySite(StockMovements, siteId, userId, roleCode);
        if (!string.IsNullOrWhiteSpace(productId)) items = items.Where(m => m.ProductId == productId).ToList();
        return (items.Select(ToStockMovementDto).Cast<object>().ToList(), StockLevels());
    }

    public static StockMovementRecord CreateStockMovement(string? siteId, string? productId, string? type, decimal quantity, string? reference)
    {
        var movement = new StockMovementRecord(Guid.NewGuid().ToString(), productId ?? Products.FirstOrDefault()?.Id ?? string.Empty, ResolveSite(siteId), type ?? "in", quantity, DateOnly.FromDateTime(DateTime.UtcNow), reference);
        StockMovements.Insert(0, movement);
        return movement;
    }

    public static IReadOnlyList<object> ListInventories(string? siteId, string userId, string roleCode) =>
        FilterBySite(Inventories, siteId, userId, roleCode).Select(ToInventoryDto).Cast<object>().ToList();

    public static InventoryRecord CreateInventory(string? siteId)
    {
        var session = new InventoryRecord(Guid.NewGuid().ToString(), ResolveSite(siteId), DateOnly.FromDateTime(DateTime.UtcNow), "open");
        Inventories.Insert(0, session);
        return session;
    }

    public static IReadOnlyList<object> ListDocuments(string? siteId, string userId, string roleCode) =>
        FilterBySite(Documents, siteId, userId, roleCode).Select(ToDocumentDto).Cast<object>().ToList();

    public static DocumentRecord CreateDocument(string originalName, string? mimeType, long sizeBytes, string? siteId, string? userId)
    {
        var doc = new DocumentRecord(Guid.NewGuid().ToString(), originalName, mimeType, sizeBytes, DateTime.UtcNow, "ged", ResolveSite(siteId));
        Documents.Insert(0, doc);
        DemoAuditStore.Add("create", "ged", "Document", doc.Id, $"Document uploadé : {originalName}", userId);
        return doc;
    }

    public static bool DeleteDocument(string id) => Documents.RemoveAll(d => d.Id == id) > 0;

    private static string ResolveSite(string? siteId) =>
        !string.IsNullOrWhiteSpace(siteId) && DemoSiteStore.Get(siteId) is not null ? siteId : DemoSites.MainSiteId;

    private static List<T> FilterBySite<T>(List<T> items, string? siteId, string userId, string roleCode) where T : ISiteScoped
    {
        IEnumerable<T> result = items;
        if (!string.Equals(roleCode, "admin", StringComparison.OrdinalIgnoreCase))
        {
            var allowed = DemoUserStore.GetSiteIds(DemoData.Tenant.Id, userId);
            result = result.Where(i => allowed.Contains(i.SiteId));
        }
        if (!string.IsNullOrWhiteSpace(siteId)) result = result.Where(i => i.SiteId == siteId);
        return result.ToList();
    }

    private static IReadOnlyList<object> StockLevels()
    {
        var levels = new Dictionary<string, decimal>();
        foreach (var m in StockMovements)
            levels[m.ProductId] = levels.GetValueOrDefault(m.ProductId) + (m.Type == "out" ? -m.Quantity : m.Quantity);
        return levels.Select(kv => (object)new { productId = kv.Key, quantity = kv.Value }).ToList();
    }

    private static object ToDto(DailyRevenueRecord r) => new { id = r.Id, siteId = r.SiteId, businessDate = r.BusinessDate, amount = r.Amount, category = r.Category, status = r.Status, notes = r.Notes };
    private static object ToInvoiceDto(InvoiceRecord i) => new { id = i.Id, number = i.Number, clientName = i.ClientName, status = i.Status, issueDate = i.IssueDate, dueDate = i.DueDate, totalAmount = i.TotalAmount, siteId = i.SiteId, lines = i.Lines };
    private static object ToTreasuryDto(TreasuryRecord t) => new { id = t.Id, siteId = t.SiteId, type = t.Type, account = t.Account, amount = t.Amount, movementDate = t.MovementDate, label = t.Label };
    private static object ToEmployeeDto(EmployeeRecord e) => new { id = e.Id, matricule = e.Matricule, firstName = e.FirstName, lastName = e.LastName, siteId = e.SiteId, department = e.Department, status = e.Status, hireDate = e.HireDate };
    private static object ToContractDto(ContractRecord c) => new { id = c.Id, employeeId = c.EmployeeId, contractType = c.ContractType, startDate = c.StartDate, endDate = c.EndDate, baseSalary = c.BaseSalary };
    private static object ToAttendanceDto(AttendanceRecord a) => new { id = a.Id, employeeId = a.EmployeeId, siteId = a.SiteId, workDate = a.WorkDate, checkIn = a.CheckIn, checkOut = a.CheckOut, notes = a.Notes };
    private static object ToProductDto(ProductRecord p) => new { id = p.Id, code = p.Code, name = p.Name, unit = p.Unit, minStockLevel = p.MinStockLevel, active = p.Active };
    private static object ToStockMovementDto(StockMovementRecord m) => new { id = m.Id, productId = m.ProductId, siteId = m.SiteId, type = m.Type, quantity = m.Quantity, movementDate = m.MovementDate, reference = m.Reference };
    private static object ToInventoryDto(InventoryRecord i) => new { id = i.Id, siteId = i.SiteId, sessionDate = i.SessionDate, status = i.Status };
    private static object ToDocumentDto(DocumentRecord d) => new { id = d.Id, originalName = d.OriginalName, mimeType = d.MimeType, sizeBytes = d.SizeBytes, uploadedAt = d.UploadedAt, moduleCode = d.ModuleCode, siteId = d.SiteId };
}

public interface ISiteScoped { string SiteId { get; } }
public sealed record DailyRevenueRecord(string Id, string SiteId, DateOnly BusinessDate, decimal Amount, string Category, string Status, string? Notes) : ISiteScoped;
public sealed record InvoiceLineRecord(string Description, decimal Quantity, decimal UnitPrice, decimal TaxRate);
public sealed record InvoiceRecord(string Id, string Number, string ClientName, string Status, DateOnly IssueDate, DateOnly? DueDate, decimal TotalAmount, string SiteId, List<InvoiceLineRecord> Lines) : ISiteScoped;
public sealed record TreasuryRecord(string Id, string SiteId, string Type, string Account, decimal Amount, DateOnly MovementDate, string Label) : ISiteScoped;
public sealed record EmployeeRecord(string Id, string Matricule, string FirstName, string LastName, string SiteId, string Department, string Status, DateOnly HireDate) : ISiteScoped;
public sealed record ContractRecord(string Id, string EmployeeId, string ContractType, DateOnly StartDate, DateOnly? EndDate, decimal BaseSalary);
public sealed record AttendanceRecord(string Id, string EmployeeId, string SiteId, DateOnly WorkDate, string? CheckIn, string? CheckOut, string? Notes) : ISiteScoped;
public sealed record ProductRecord(string Id, string Code, string Name, string Unit, decimal MinStockLevel, bool Active);
public sealed record StockMovementRecord(string Id, string ProductId, string SiteId, string Type, decimal Quantity, DateOnly MovementDate, string? Reference) : ISiteScoped;
public sealed record InventoryRecord(string Id, string SiteId, DateOnly SessionDate, string Status) : ISiteScoped;
public sealed record DocumentRecord(string Id, string OriginalName, string? MimeType, long SizeBytes, DateTime UploadedAt, string? ModuleCode, string SiteId) : ISiteScoped;
