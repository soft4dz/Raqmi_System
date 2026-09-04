using RaqmiSystem.Domain.Common;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Domain.Pos;

public enum PosOrderType { DineIn, TakeAway, RoomService, Retail }
public enum PosTicketStatus { Open, Paid, Cancelled }
public enum PosPaymentMethod { Cash, Card, Cib, RoomCharge, Transfer }

public sealed class PosOutlet : AuditableEntity
{
    private PosOutlet() { }
    public PosOutlet(string code, string name, string hotelUnitCode, string kind)
    {
        Code = Required(code, 40).ToUpperInvariant(); Name = Required(name, 160);
        HotelUnitCode = HotelUnit.NormalizeCode(hotelUnitCode); Kind = Required(kind, 60); IsActive = true;
    }
    public string Code { get; private set; } = "";
    public string Name { get; private set; } = "";
    public string HotelUnitCode { get; private set; } = "";
    public string Kind { get; private set; } = "";
    public bool IsActive { get; private set; }
    public void Update(string name, string kind, bool active) { Name = Required(name, 160); Kind = Required(kind, 60); IsActive = active; }
    internal static string Required(string value, int max) { var text = value?.Trim(); if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Value is required."); if (text.Length > max) throw new ArgumentException($"Value cannot exceed {max} characters."); return text; }
}

public sealed class PosTable : AuditableEntity
{
    private PosTable() { }
    public PosTable(Guid outletId, string zone, string number, int seats)
    { OutletId = outletId; Zone = PosOutlet.Required(zone, 80); Number = PosOutlet.Required(number, 30); SetSeats(seats); IsActive = true; }
    public Guid OutletId { get; private set; }
    public string Zone { get; private set; } = "";
    public string Number { get; private set; } = "";
    public int Seats { get; private set; }
    public bool IsActive { get; private set; }
    public void Update(string zone, string number, int seats, bool active) { Zone = PosOutlet.Required(zone, 80); Number = PosOutlet.Required(number, 30); SetSeats(seats); IsActive = active; }
    private void SetSeats(int seats) { if (seats is < 1 or > 50) throw new ArgumentOutOfRangeException(nameof(seats)); Seats = seats; }
}

public sealed class PosProduct : AuditableEntity
{
    private PosProduct() { }
    public PosProduct(Guid outletId, string code, string name, string category, decimal price)
    { OutletId = outletId; Code = PosOutlet.Required(code, 40).ToUpperInvariant(); Name = PosOutlet.Required(name, 160); Category = PosOutlet.Required(category, 80); SetPrice(price); IsActive = true; }
    public Guid OutletId { get; private set; }
    public string Code { get; private set; } = "";
    public string Name { get; private set; } = "";
    public string Category { get; private set; } = "";
    public decimal Price { get; private set; }
    public bool IsActive { get; private set; }
    public void Update(string name, string category, decimal price, bool active) { Name = PosOutlet.Required(name, 160); Category = PosOutlet.Required(category, 80); SetPrice(price); IsActive = active; }
    private void SetPrice(decimal value) { if (value < 0) throw new ArgumentOutOfRangeException(nameof(value)); Price = decimal.Round(value, 2); }
}

public sealed class PosTicket : AuditableEntity
{
    private PosTicket() { }
    public PosTicket(Guid outletId, string number, PosOrderType orderType, Guid? tableId, string? roomNumber)
    { OutletId = outletId; Number = PosOutlet.Required(number, 40); OrderType = orderType; TableId = tableId; RoomNumber = roomNumber?.Trim(); Status = PosTicketStatus.Open; OpenedAt = DateTimeOffset.UtcNow; }
    public Guid OutletId { get; private set; }
    public string Number { get; private set; } = "";
    public PosOrderType OrderType { get; private set; }
    public Guid? TableId { get; private set; }
    public string? RoomNumber { get; private set; }
    public PosTicketStatus Status { get; private set; }
    public DateTimeOffset OpenedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public PosPaymentMethod? PaymentMethod { get; private set; }
    public string? CancellationReason { get; private set; }
    public ICollection<PosTicketLine> Lines { get; private set; } = new List<PosTicketLine>();
    public void Pay(PosPaymentMethod method) { if (Status != PosTicketStatus.Open || Lines.Count == 0) throw new InvalidOperationException("Only a non-empty open ticket can be paid."); PaymentMethod = method; Status = PosTicketStatus.Paid; ClosedAt = DateTimeOffset.UtcNow; }
    public void Cancel(string reason) { if (Status != PosTicketStatus.Open) throw new InvalidOperationException("Only an open ticket can be cancelled."); CancellationReason = PosOutlet.Required(reason, 240); Status = PosTicketStatus.Cancelled; ClosedAt = DateTimeOffset.UtcNow; }
}

public sealed class PosTicketLine : AuditableEntity
{
    private PosTicketLine() { }
    public PosTicketLine(Guid ticketId, Guid productId, string productName, int quantity, decimal unitPrice)
    { TicketId = ticketId; ProductId = productId; ProductName = PosOutlet.Required(productName, 160); SetQuantity(quantity); UnitPrice = unitPrice; }
    public Guid TicketId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = "";
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal Total => Quantity * UnitPrice;
    public void ChangeQuantity(int quantity) => SetQuantity(quantity);
    private void SetQuantity(int value) { if (value is < 1 or > 999) throw new ArgumentOutOfRangeException(nameof(value)); Quantity = value; }
}
