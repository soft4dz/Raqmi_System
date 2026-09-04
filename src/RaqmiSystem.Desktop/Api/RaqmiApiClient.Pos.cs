using System.Net.Http;
using RaqmiSystem.Application.Pos;
using RaqmiSystem.Domain.Pos;

namespace RaqmiSystem.Desktop.Api;

public sealed partial class RaqmiApiClient
{
    private const string PosPath = "/api/v1/pos";
    public async Task<IReadOnlyCollection<PosOutletResponse>> GetPosOutletsAsync(string url,string unit,bool inactive=false,CancellationToken ct=default){EnsureAuthenticated();var p=$"{PosPath}/outlets?hotelUnitCode={Uri.EscapeDataString(unit)}&includeInactive={inactive.ToString().ToLowerInvariant()}";return await ReadResponseAsync<IReadOnlyCollection<PosOutletResponse>>(await SendAsync(url,HttpMethod.Get,p,null,true,ct),ct);}
    public async Task<PosOutletResponse> SavePosOutletAsync(string url,Guid? id,SavePosOutletRequest r,CancellationToken ct=default){EnsureAuthenticated();var p=id is null?$"{PosPath}/outlets":$"{PosPath}/outlets/{id}";return await ReadResponseAsync<PosOutletResponse>(await SendAsync(url,id is null?HttpMethod.Post:HttpMethod.Put,p,r,true,ct),ct);}
    public async Task<IReadOnlyCollection<PosTableResponse>> GetPosTablesAsync(string url,Guid outlet,CancellationToken ct=default){EnsureAuthenticated();return await ReadResponseAsync<IReadOnlyCollection<PosTableResponse>>(await SendAsync(url,HttpMethod.Get,$"{PosPath}/outlets/{outlet}/tables",null,true,ct),ct);}
    public async Task<PosTableResponse> SavePosTableAsync(string url,Guid outlet,Guid? id,SavePosTableRequest r,CancellationToken ct=default){EnsureAuthenticated();var p=$"{PosPath}/outlets/{outlet}/tables"+(id is null?"":$"/{id}");return await ReadResponseAsync<PosTableResponse>(await SendAsync(url,id is null?HttpMethod.Post:HttpMethod.Put,p,r,true,ct),ct);}
    public async Task<IReadOnlyCollection<PosProductResponse>> GetPosProductsAsync(string url,Guid outlet,bool inactive=false,CancellationToken ct=default){EnsureAuthenticated();return await ReadResponseAsync<IReadOnlyCollection<PosProductResponse>>(await SendAsync(url,HttpMethod.Get,$"{PosPath}/outlets/{outlet}/products?includeInactive={inactive.ToString().ToLowerInvariant()}",null,true,ct),ct);}
    public async Task<PosProductResponse> SavePosProductAsync(string url,Guid outlet,Guid? id,SavePosProductRequest r,CancellationToken ct=default){EnsureAuthenticated();var p=$"{PosPath}/outlets/{outlet}/products"+(id is null?"":$"/{id}");return await ReadResponseAsync<PosProductResponse>(await SendAsync(url,id is null?HttpMethod.Post:HttpMethod.Put,p,r,true,ct),ct);}
    public async Task<PosTicketResponse> CreatePosTicketAsync(string url,CreatePosTicketRequest r,CancellationToken ct=default){EnsureAuthenticated();return await ReadResponseAsync<PosTicketResponse>(await SendAsync(url,HttpMethod.Post,$"{PosPath}/tickets",r,true,ct),ct);}
    public async Task<IReadOnlyCollection<PosTicketResponse>> GetPosTicketsAsync(string url,Guid outlet,DateOnly date,CancellationToken ct=default){EnsureAuthenticated();return await ReadResponseAsync<IReadOnlyCollection<PosTicketResponse>>(await SendAsync(url,HttpMethod.Get,$"{PosPath}/outlets/{outlet}/tickets?businessDate={date:yyyy-MM-dd}",null,true,ct),ct);}
    public async Task<PosTicketResponse> PayPosTicketAsync(string url,Guid id,PosPaymentMethod method,CancellationToken ct=default){EnsureAuthenticated();return await ReadResponseAsync<PosTicketResponse>(await SendAsync(url,HttpMethod.Post,$"{PosPath}/tickets/{id}/pay?method={method}",null,true,ct),ct);}
    public async Task<PosDashboardResponse> GetPosDashboardAsync(string url,string unit,DateOnly date,CancellationToken ct=default){EnsureAuthenticated();return await ReadResponseAsync<PosDashboardResponse>(await SendAsync(url,HttpMethod.Get,$"{PosPath}/dashboard?hotelUnitCode={Uri.EscapeDataString(unit)}&businessDate={date:yyyy-MM-dd}",null,true,ct),ct);}
}
