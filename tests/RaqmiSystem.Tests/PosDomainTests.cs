using RaqmiSystem.Domain.Pos;

namespace RaqmiSystem.Tests;

public sealed class PosDomainTests
{
    [Fact]
    public void Outlet_is_always_scoped_to_a_hotel_unit()
    {
        var outlet = new PosOutlet("resto", "Restaurant principal", "hotel-alger", "Restaurant");
        Assert.Equal("HOTEL-ALGER", outlet.HotelUnitCode);
        Assert.True(outlet.IsActive);
    }

    [Fact]
    public void Ticket_cannot_be_paid_without_lines()
    {
        var ticket = new PosTicket(Guid.NewGuid(), "T-1", PosOrderType.DineIn, null, null);
        Assert.Throws<InvalidOperationException>(() => ticket.Pay(PosPaymentMethod.Cash));
    }

    [Fact]
    public void Paid_ticket_is_closed_and_cannot_be_cancelled()
    {
        var ticket = new PosTicket(Guid.NewGuid(), "T-2", PosOrderType.DineIn, null, null);
        ticket.Lines.Add(new PosTicketLine(ticket.Id, Guid.NewGuid(), "Café", 2, 180m));
        ticket.Pay(PosPaymentMethod.Cib);
        Assert.Equal(PosTicketStatus.Paid, ticket.Status);
        Assert.NotNull(ticket.ClosedAt);
        Assert.Throws<InvalidOperationException>(() => ticket.Cancel("Erreur"));
    }

    [Fact]
    public void Table_rejects_invalid_capacity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PosTable(Guid.NewGuid(), "Salle", "12", 0));
    }
}
