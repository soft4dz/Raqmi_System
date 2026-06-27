using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Raqmi.Client;

public sealed class BusinessApiClient
{
    private readonly RaqmiApiClient _api;
    private readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    public BusinessApiClient(RaqmiApiClient api) => _api = api;

    private HttpRequestMessage AuthGet(string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _api.Token);
        return request;
    }

    private HttpRequestMessage AuthPost(string path, object? body = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _api.Token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return request;
    }

    private HttpRequestMessage AuthPatch(string path, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _api.Token);
        request.Content = JsonContent.Create(body);
        return request;
    }

    private async Task<T> SendAsync<T>(HttpRequestMessage request)
    {
        var response = await _api.Http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(_json))!;
    }

    public Task<DailyRevenueListResponse> GetDailyRevenueAsync() =>
        SendAsync<DailyRevenueListResponse>(AuthGet("/api/v1/finance/daily-revenue"));

    public Task<DailyRevenueEntryDto> CreateDailyRevenueAsync(object body) =>
        SendAsync<DailyRevenueEntryDto>(AuthPost("/api/v1/finance/daily-revenue", body));

    public Task<DailyRevenueEntryDto> ValidateDailyRevenueAsync(string id) =>
        SendAsync<DailyRevenueEntryDto>(AuthPatch($"/api/v1/finance/daily-revenue/{id}/validate", new { }));

    public Task<InvoiceListResponse> GetInvoicesAsync() =>
        SendAsync<InvoiceListResponse>(AuthGet("/api/v1/finance/invoices"));

    public Task<InvoiceDto> CreateInvoiceAsync(object body) =>
        SendAsync<InvoiceDto>(AuthPost("/api/v1/finance/invoices", body));

    public Task<TreasuryResponse> GetTreasuryAsync() =>
        SendAsync<TreasuryResponse>(AuthGet("/api/v1/finance/treasury/movements"));

    public Task<TreasuryMovementDto> CreateTreasuryMovementAsync(object body) =>
        SendAsync<TreasuryMovementDto>(AuthPost("/api/v1/finance/treasury/movements", body));

    public Task<EmployeeListResponse> GetEmployeesAsync(string? search = null) =>
        SendAsync<EmployeeListResponse>(AuthGet($"/api/v1/hr/employees{(string.IsNullOrWhiteSpace(search) ? "" : $"?search={Uri.EscapeDataString(search)}")}"));

    public Task<EmployeeDto> CreateEmployeeAsync(object body) =>
        SendAsync<EmployeeDto>(AuthPost("/api/v1/hr/employees", body));

    public Task<ContractListResponse> GetContractsAsync(string employeeId) =>
        SendAsync<ContractListResponse>(AuthGet($"/api/v1/hr/employees/{employeeId}/contracts"));

    public Task<EmploymentContractDto> CreateContractAsync(string employeeId, object body) =>
        SendAsync<EmploymentContractDto>(AuthPost($"/api/v1/hr/employees/{employeeId}/contracts", body));

    public Task<AttendanceListResponse> GetAttendanceAsync() =>
        SendAsync<AttendanceListResponse>(AuthGet("/api/v1/hr/attendance"));

    public Task<AttendanceDto> CreateAttendanceAsync(object body) =>
        SendAsync<AttendanceDto>(AuthPost("/api/v1/hr/attendance", body));

    public Task<ProductListResponse> GetProductsAsync() =>
        SendAsync<ProductListResponse>(AuthGet("/api/v1/stocks/products"));

    public Task<ProductDto> CreateProductAsync(object body) =>
        SendAsync<ProductDto>(AuthPost("/api/v1/stocks/products", body));

    public Task<StockMovementListResponse> GetStockMovementsAsync() =>
        SendAsync<StockMovementListResponse>(AuthGet("/api/v1/stocks/movements"));

    public Task<StockMovementDto> CreateStockMovementAsync(object body) =>
        SendAsync<StockMovementDto>(AuthPost("/api/v1/stocks/movements", body));

    public Task<InventoryListResponse> GetInventoriesAsync() =>
        SendAsync<InventoryListResponse>(AuthGet("/api/v1/stocks/inventories"));

    public Task<InventorySessionDto> CreateInventoryAsync(object body) =>
        SendAsync<InventorySessionDto>(AuthPost("/api/v1/stocks/inventories", body));

    public Task<DocumentListResponse> GetDocumentsAsync() =>
        SendAsync<DocumentListResponse>(AuthGet("/api/v1/ged/documents"));

    public Task<UserListResponse> GetUsersAsync() =>
        SendAsync<UserListResponse>(AuthGet("/api/v1/admin/users"));

    public Task<SiteListResponse> GetAdminSitesAsync() =>
        SendAsync<SiteListResponse>(AuthGet("/api/v1/admin/sites"));

    public Task<RoleListResponse> GetRolesAsync() =>
        SendAsync<RoleListResponse>(AuthGet("/api/v1/admin/roles"));

    public Task<UserDto> CreateUserAsync(object body) =>
        SendAsync<UserDto>(AuthPost("/api/v1/admin/users", body));

    public Task<UserDto> UpdateUserAsync(string id, object body) =>
        SendAsync<UserDto>(AuthPatch($"/api/v1/admin/users/{id}", body));

    public async Task UploadDocumentAsync(string filePath)
    {
        using var content = new MultipartFormDataContent();
        await using var stream = File.OpenRead(filePath);
        content.Add(new StreamContent(stream), "file", Path.GetFileName(filePath));
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ged/documents");
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
public sealed record SiteDto(string Id, string Code, string Name, string? City, bool Active);
public sealed record SiteListResponse(List<SiteDto> Items);
public sealed record RoleDto(string Code, string Label);
public sealed record RoleListResponse(List<RoleDto> Items);
