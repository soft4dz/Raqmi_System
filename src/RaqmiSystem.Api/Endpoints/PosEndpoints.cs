using RaqmiSystem.Application.Pos;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Domain.Pos;

namespace RaqmiSystem.Api.Endpoints;

public static class PosEndpoints
{
    public static void MapPosEndpoints(this RouteGroupBuilder api)
    {
        var g=api.MapGroup("/pos").WithTags("Points de vente");
        g.MapGet("/outlets",async(string hotelUnitCode,bool? includeInactive,IPosService s,CancellationToken ct)=>Results.Ok(await s.ListOutletsAsync(hotelUnitCode,includeInactive==true,ct))).RequireAuthorization(PermissionCatalog.PosRead);
        g.MapPost("/outlets",async(SavePosOutletRequest r,IPosService s,HttpContext h,CancellationToken ct)=>(await s.SaveOutletAsync(null,r,h.ToOperationContext(),ct)).ToHttpResult()).RequireAuthorization(PermissionCatalog.PosManage);
        g.MapPut("/outlets/{id:guid}",async(Guid id,SavePosOutletRequest r,IPosService s,HttpContext h,CancellationToken ct)=>(await s.SaveOutletAsync(id,r,h.ToOperationContext(),ct)).ToHttpResult()).RequireAuthorization(PermissionCatalog.PosManage);
        g.MapGet("/outlets/{outletId:guid}/tables",async(Guid outletId,IPosService s,CancellationToken ct)=>Results.Ok(await s.ListTablesAsync(outletId,ct))).RequireAuthorization(PermissionCatalog.PosRead);
        g.MapPost("/outlets/{outletId:guid}/tables",async(Guid outletId,SavePosTableRequest r,IPosService s,HttpContext h,CancellationToken ct)=>(await s.SaveTableAsync(outletId,null,r,h.ToOperationContext(),ct)).ToHttpResult()).RequireAuthorization(PermissionCatalog.PosManage);
        g.MapPut("/outlets/{outletId:guid}/tables/{id:guid}",async(Guid outletId,Guid id,SavePosTableRequest r,IPosService s,HttpContext h,CancellationToken ct)=>(await s.SaveTableAsync(outletId,id,r,h.ToOperationContext(),ct)).ToHttpResult()).RequireAuthorization(PermissionCatalog.PosManage);
        g.MapGet("/outlets/{outletId:guid}/products",async(Guid outletId,bool? includeInactive,IPosService s,CancellationToken ct)=>Results.Ok(await s.ListProductsAsync(outletId,includeInactive==true,ct))).RequireAuthorization(PermissionCatalog.PosRead);
        g.MapPost("/outlets/{outletId:guid}/products",async(Guid outletId,SavePosProductRequest r,IPosService s,HttpContext h,CancellationToken ct)=>(await s.SaveProductAsync(outletId,null,r,h.ToOperationContext(),ct)).ToHttpResult()).RequireAuthorization(PermissionCatalog.PosManage);
        g.MapPut("/outlets/{outletId:guid}/products/{id:guid}",async(Guid outletId,Guid id,SavePosProductRequest r,IPosService s,HttpContext h,CancellationToken ct)=>(await s.SaveProductAsync(outletId,id,r,h.ToOperationContext(),ct)).ToHttpResult()).RequireAuthorization(PermissionCatalog.PosManage);
        g.MapPost("/tickets",async(CreatePosTicketRequest r,IPosService s,HttpContext h,CancellationToken ct)=>(await s.CreateTicketAsync(r,h.ToOperationContext(),ct)).ToHttpResult()).RequireAuthorization(PermissionCatalog.PosSell);
        g.MapGet("/outlets/{outletId:guid}/tickets",async(Guid outletId,DateOnly businessDate,IPosService s,CancellationToken ct)=>Results.Ok(await s.ListTicketsAsync(outletId,businessDate,ct))).RequireAuthorization(PermissionCatalog.PosRead);
        g.MapPost("/tickets/{id:guid}/pay",async(Guid id,PosPaymentMethod method,IPosService s,HttpContext h,CancellationToken ct)=>(await s.PayTicketAsync(id,method,h.ToOperationContext(),ct)).ToHttpResult()).RequireAuthorization(PermissionCatalog.PosSell);
        g.MapPost("/tickets/{id:guid}/cancel",async(Guid id,string reason,IPosService s,HttpContext h,CancellationToken ct)=>(await s.CancelTicketAsync(id,reason,h.ToOperationContext(),ct)).ToHttpResult()).RequireAuthorization(PermissionCatalog.PosCancel);
        g.MapGet("/dashboard",async(string hotelUnitCode,DateOnly businessDate,IPosService s,CancellationToken ct)=>Results.Ok(await s.GetDashboardAsync(hotelUnitCode,businessDate,ct))).RequireAuthorization(PermissionCatalog.PosRead);
    }
}
