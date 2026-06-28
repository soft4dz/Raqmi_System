using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Raqmi.Client;

public sealed class BusinessApiClient
{
    private readonly RaqmiApiClient _api;
    private readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };
    private string? _activeSiteId;

    public BusinessApiClient(RaqmiApiClient api) => _api = api;

    public void SetActiveSiteId(string? siteId) => _activeSiteId = siteId;

    public string? ActiveSiteId => _activeSiteId;

    private string WithSite(string path)
    {
        if (string.IsNullOrWhiteSpace(_activeSiteId)) return path;
        var sep = path.Contains('?') ? "&" : "?";
        return $"{path}{sep}siteId={Uri.EscapeDataString(_activeSiteId)}";
    }

    private HttpRequestMessage AuthGet(string path, bool withSite = false)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, withSite ? WithSite(path) : path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _api.Token);
        return request;
    }

    private HttpRequestMessage AuthPost(string path, object? body = null, bool withSite = false)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, withSite ? WithSite(path) : path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _api.Token);
        if (body is not null)
            request.Content = withSite ? CreateJsonContentWithSite(body) : JsonContent.Create(body);
        return request;
    }

    private HttpRequestMessage AuthPatch(string path, object body, bool withSite = false)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, withSite ? WithSite(path) : path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _api.Token);
        request.Content = withSite ? CreateJsonContentWithSite(body) : JsonContent.Create(body);
        return request;
    }

    private HttpRequestMessage AuthDelete(string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _api.Token);
        return request;
    }

    private JsonContent CreateJsonContentWithSite(object body)
    {
        var node = JsonSerializer.SerializeToNode(body, _json)?.AsObject() ?? new JsonObject();
        if (!string.IsNullOrWhiteSpace(_activeSiteId))
            node["siteId"] = _activeSiteId;
        return JsonContent.Create(node);
    }

    private async Task<T> SendAsync<T>(HttpRequestMessage request)
    {
        var response = await _api.Http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(_json))!;
    }

    private async Task SendNoContentAsync(HttpRequestMessage request)
    {
        var response = await _api.Http.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    public Task<DailyRevenueListResponse> GetDailyRevenueAsync() =>
        SendAsync<DailyRevenueListResponse>(AuthGet("/api/v1/finance/daily-revenue", withSite: true));

    public Task<DailyRevenueEntryDto> CreateDailyRevenueAsync(object body) =>
        SendAsync<DailyRevenueEntryDto>(AuthPost("/api/v1/finance/daily-revenue", body, withSite: true));

    public Task<DailyRevenueEntryDto> ValidateDailyRevenueAsync(string id) =>
        SendAsync<DailyRevenueEntryDto>(AuthPatch($"/api/v1/finance/daily-revenue/{id}/validate", new { }));

    public Task<InvoiceListResponse> GetInvoicesAsync() =>
        SendAsync<InvoiceListResponse>(AuthGet("/api/v1/finance/invoices", withSite: true));

    public Task<InvoiceDto> CreateInvoiceAsync(object body) =>
        SendAsync<InvoiceDto>(AuthPost("/api/v1/finance/invoices", body, withSite: true));

    public Task<TreasuryResponse> GetTreasuryAsync() =>
        SendAsync<TreasuryResponse>(AuthGet("/api/v1/finance/treasury/movements", withSite: true));

    public Task<TreasuryMovementDto> CreateTreasuryMovementAsync(object body) =>
        SendAsync<TreasuryMovementDto>(AuthPost("/api/v1/finance/treasury/movements", body, withSite: true));

    public Task<EmployeeListResponse> GetEmployeesAsync(string? search = null)
    {
        var path = "/api/v1/hr/employees";
        if (!string.IsNullOrWhiteSpace(search))
            path += $"?search={Uri.EscapeDataString(search)}";
        return SendAsync<EmployeeListResponse>(AuthGet(path, withSite: true));
    }

    public Task<EmployeeDto> CreateEmployeeAsync(object body) =>
        SendAsync<EmployeeDto>(AuthPost("/api/v1/hr/employees", body, withSite: true));

    public Task<ContractListResponse> GetContractsAsync(string employeeId) =>
        SendAsync<ContractListResponse>(AuthGet($"/api/v1/hr/employees/{employeeId}/contracts"));

    public Task<EmploymentContractDto> CreateContractAsync(string employeeId, object body) =>
        SendAsync<EmploymentContractDto>(AuthPost($"/api/v1/hr/employees/{employeeId}/contracts", body));

    public Task<AttendanceListResponse> GetAttendanceAsync() =>
        SendAsync<AttendanceListResponse>(AuthGet("/api/v1/hr/attendance", withSite: true));

    public Task<AttendanceDto> CreateAttendanceAsync(object body) =>
        SendAsync<AttendanceDto>(AuthPost("/api/v1/hr/attendance", body, withSite: true));

    public Task<ProductListResponse> GetProductsAsync() =>
        SendAsync<ProductListResponse>(AuthGet("/api/v1/stocks/products"));

    public Task<ProductDto> CreateProductAsync(object body) =>
        SendAsync<ProductDto>(AuthPost("/api/v1/stocks/products", body));

    public Task<StockMovementListResponse> GetStockMovementsAsync() =>
        SendAsync<StockMovementListResponse>(AuthGet("/api/v1/stocks/movements", withSite: true));

    public Task<StockMovementDto> CreateStockMovementAsync(object body) =>
        SendAsync<StockMovementDto>(AuthPost("/api/v1/stocks/movements", body, withSite: true));

    public Task<InventoryListResponse> GetInventoriesAsync() =>
        SendAsync<InventoryListResponse>(AuthGet("/api/v1/stocks/inventories", withSite: true));

    public Task<InventorySessionDto> CreateInventoryAsync(object body) =>
        SendAsync<InventorySessionDto>(AuthPost("/api/v1/stocks/inventories", body, withSite: true));

    public Task<DocumentListResponse> GetDocumentsAsync() =>
        SendAsync<DocumentListResponse>(AuthGet("/api/v1/ged/documents", withSite: true));

    public Task<UserListResponse> GetUsersAsync() =>
        SendAsync<UserListResponse>(AuthGet("/api/v1/admin/users"));

    public Task<SiteListResponse> GetAdminSitesAsync() =>
        SendAsync<SiteListResponse>(AuthGet("/api/v1/admin/sites"));

    public Task<SiteListResponse> GetSitesAsync() =>
        SendAsync<SiteListResponse>(AuthGet("/api/v1/sites"));

    public Task<SiteDto> CreateSiteAsync(object body) =>
        SendAsync<SiteDto>(AuthPost("/api/v1/sites", body));

    public Task<SiteDto> UpdateSiteAsync(string id, object body) =>
        SendAsync<SiteDto>(AuthPatch($"/api/v1/sites/{id}", body));

    public Task<TenantSettingsDto> GetTenantSettingsAsync() =>
        SendAsync<TenantSettingsDto>(AuthGet("/api/v1/settings"));

    public Task<TenantSettingsDto> UpdateTenantSettingsAsync(object body) =>
        SendAsync<TenantSettingsDto>(AuthPatch("/api/v1/settings", body));

    public Task<RoleListResponse> GetRolesAsync() =>
        SendAsync<RoleListResponse>(AuthGet("/api/v1/admin/roles"));

    public Task<PermissionListResponse> GetPermissionsAsync() =>
        SendAsync<PermissionListResponse>(AuthGet("/api/v1/admin/permissions"));

    public Task<RoleDetailDto> CreateRoleAsync(object body) =>
        SendAsync<RoleDetailDto>(AuthPost("/api/v1/admin/roles", body));

    public Task<RoleDetailDto> UpdateRoleAsync(string code, object body) =>
        SendAsync<RoleDetailDto>(AuthPatch($"/api/v1/admin/roles/{code}", body));

    public async Task DeleteRoleAsync(string code) =>
        await SendNoContentAsync(AuthDelete($"/api/v1/admin/roles/{code}"));

    public Task<AuditLogListResponse> GetAuditLogsAsync(string? action = null, string? moduleCode = null, string? q = null)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(action)) parts.Add($"action={Uri.EscapeDataString(action)}");
        if (!string.IsNullOrWhiteSpace(moduleCode)) parts.Add($"moduleCode={Uri.EscapeDataString(moduleCode)}");
        if (!string.IsNullOrWhiteSpace(q)) parts.Add($"q={Uri.EscapeDataString(q)}");
        var path = parts.Count > 0
            ? $"/api/v1/admin/audit-logs?{string.Join("&", parts)}"
            : "/api/v1/admin/audit-logs";
        return SendAsync<AuditLogListResponse>(AuthGet(path));
    }

    public Task<UserDto> CreateUserAsync(object body) =>
        SendAsync<UserDto>(AuthPost("/api/v1/admin/users", body));

    public Task<UserDto> UpdateUserAsync(string id, object body) =>
        SendAsync<UserDto>(AuthPatch($"/api/v1/admin/users/{id}", body));

    public async Task UploadDocumentAsync(string filePath)
    {
        using var content = new MultipartFormDataContent();
        await using var stream = File.OpenRead(filePath);
        content.Add(new StreamContent(stream), "file", Path.GetFileName(filePath));
        using var request = new HttpRequestMessage(HttpMethod.Post, WithSite("/api/v1/ged/documents"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _api.Token);
        request.Content = content;
        var response = await _api.Http.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteDocumentAsync(string id)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/ged/documents/{id}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _api.Token);
        var response = await _api.Http.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }
}

public sealed record DailyRevenueEntryDto(string Id, string SiteId, DateOnly BusinessDate, decimal Amount, string Category, string Status, string? Notes);
public sealed record DailyRevenueListResponse(List<DailyRevenueEntryDto> Items);
public sealed record InvoiceLineDto(string Description, decimal Quantity, decimal UnitPrice, decimal TaxRate);
public sealed record InvoiceDto(string Id, string Number, string ClientName, string Status, DateOnly IssueDate, decimal TotalAmount, List<InvoiceLineDto>? Lines);
public sealed record InvoiceListResponse(List<InvoiceDto> Items);
public sealed record TreasuryMovementDto(string Id, string Type, string Account, decimal Amount, DateOnly MovementDate, string Label);
public sealed record TreasuryResponse(List<TreasuryMovementDto> Items, decimal Balance);
public sealed record EmployeeDto(string Id, string Matricule, string FirstName, string LastName, string Department, string Status);
public sealed record EmployeeListResponse(List<EmployeeDto> Items);
public sealed record EmploymentContractDto(string Id, string ContractType, DateOnly StartDate, decimal BaseSalary);
public sealed record ContractListResponse(List<EmploymentContractDto> Items);
public sealed record AttendanceDto(string Id, string EmployeeId, DateOnly WorkDate, string? Notes);
public sealed record AttendanceListResponse(List<AttendanceDto> Items);
public sealed record ProductDto(string Id, string Code, string Name, string Unit, decimal MinStockLevel);
public sealed record ProductListResponse(List<ProductDto> Items);
public sealed record StockMovementDto(string Id, string ProductId, string Type, decimal Quantity, DateOnly MovementDate);
public sealed record StockMovementListResponse(List<StockMovementDto> Items, List<StockLevelDto> StockByProduct);
public sealed record StockLevelDto(string ProductId, decimal Quantity);
public sealed record InventorySessionDto(string Id, string SiteId, DateOnly SessionDate, string Status);
public sealed record InventoryListResponse(List<InventorySessionDto> Items);
public sealed record DocumentDto(string Id, string OriginalName, string? MimeType, long SizeBytes, DateTime UploadedAt, string? ModuleCode);
public sealed record DocumentListResponse(List<DocumentDto> Items);
public sealed record UserDto(string Id, string Email, string FullName, string RoleCode, bool Active, List<string> SiteIds);
public sealed record UserListResponse(List<UserDto> Items);
public sealed record SiteDto(string Id, string Code, string Name, string? Type, string? City, bool Active);
public sealed record SiteListResponse(List<SiteDto> Items);
public sealed record RoleDto(string Code, string Label);
public sealed record RoleDetailDto(string Code, string Label, bool IsSystem, List<string> Permissions);
public sealed record RoleListResponse(List<RoleDetailDto> Items);
public sealed record PermissionDto(string Key, string Label, string Module);
public sealed record PermissionListResponse(List<PermissionDto> Items);
public sealed record TenantSettingsDto(
    string? LegalName,
    string? Email,
    string? Phone,
    string? Address,
    string Currency,
    string DateFormat,
    string NumberFormat,
    string Timezone,
    int PaymentDelayDays,
    int ReminderDelayDays,
    string? BrandPrimaryColor,
    string? BrandLogoUrl);
public sealed record AuditLogDto(
    string Id,
    string? UserId,
    string Action,
    string? ModuleCode,
    string? EntityType,
    string? EntityId,
    string Description,
    DateTime CreatedAt);
public sealed record AuditLogListResponse(List<AuditLogDto> Items);
