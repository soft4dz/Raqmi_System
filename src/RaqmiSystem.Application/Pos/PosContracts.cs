using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Pos;

namespace RaqmiSystem.Application.Pos;

public sealed record PosOutletResponse(Guid Id, string Code, string Name, string HotelUnitCode, string Kind, bool IsActive);
public sealed record SavePosOutletRequest(string Code, string Name, string HotelUnitCode, string Kind, bool IsActive = true);
public sealed record PosTableResponse(Guid Id, Guid OutletId, string Zone, string Number, int Seats, bool IsActive);
public sealed record SavePosTableRequest(string Zone, string Number, int Seats, bool IsActive = true);
public sealed record PosProductResponse(Guid Id, Guid OutletId, string Code, string Name, string Category, decimal Price, bool IsActive);
public sealed record SavePosProductRequest(string Code, string Name, string Category, decimal Price, bool IsActive = true);
public sealed record PosTicketLineRequest(Guid ProductId, int Quantity);
public sealed record CreatePosTicketRequest(Guid OutletId, PosOrderType OrderType, Guid? TableId, string? RoomNumber, IReadOnlyCollection<PosTicketLineRequest> Lines);
public sealed record PosTicketLineResponse(Guid Id, Guid ProductId, string ProductName, int Quantity, decimal UnitPrice, decimal Total);
public sealed record PosTicketResponse(Guid Id, Guid OutletId, string Number, PosOrderType OrderType, Guid? TableId, string? RoomNumber, PosTicketStatus Status, DateTimeOffset OpenedAt, DateTimeOffset? ClosedAt, PosPaymentMethod? PaymentMethod, decimal Total, IReadOnlyCollection<PosTicketLineResponse> Lines);
public sealed record PosDashboardResponse(decimal Revenue, int PaidTickets, int OpenTickets, decimal AverageTicket, IReadOnlyCollection<PosOutletSalesResponse> ByOutlet);
public sealed record PosOutletSalesResponse(Guid OutletId, string OutletName, decimal Revenue, int Tickets);

public interface IPosService
{
    Task<IReadOnlyCollection<PosOutletResponse>> ListOutletsAsync(string hotelUnitCode, bool includeInactive, CancellationToken ct);
    Task<ApplicationResult<PosOutletResponse>> SaveOutletAsync(Guid? id, SavePosOutletRequest request, OperationContext context, CancellationToken ct);
    Task<IReadOnlyCollection<PosTableResponse>> ListTablesAsync(Guid outletId, CancellationToken ct);
    Task<ApplicationResult<PosTableResponse>> SaveTableAsync(Guid outletId, Guid? id, SavePosTableRequest request, OperationContext context, CancellationToken ct);
    Task<IReadOnlyCollection<PosProductResponse>> ListProductsAsync(Guid outletId, bool includeInactive, CancellationToken ct);
    Task<ApplicationResult<PosProductResponse>> SaveProductAsync(Guid outletId, Guid? id, SavePosProductRequest request, OperationContext context, CancellationToken ct);
    Task<ApplicationResult<PosTicketResponse>> CreateTicketAsync(CreatePosTicketRequest request, OperationContext context, CancellationToken ct);
    Task<IReadOnlyCollection<PosTicketResponse>> ListTicketsAsync(Guid outletId, DateOnly businessDate, CancellationToken ct);
    Task<ApplicationResult<PosTicketResponse>> PayTicketAsync(Guid id, PosPaymentMethod method, OperationContext context, CancellationToken ct);
    Task<ApplicationResult<PosTicketResponse>> CancelTicketAsync(Guid id, string reason, OperationContext context, CancellationToken ct);
    Task<PosDashboardResponse> GetDashboardAsync(string hotelUnitCode, DateOnly businessDate, CancellationToken ct);
}
