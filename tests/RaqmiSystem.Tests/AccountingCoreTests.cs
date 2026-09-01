using RaqmiSystem.Domain.Accounting;

namespace RaqmiSystem.Tests;

public sealed class AccountingCoreTests
{
    [Fact]
    public void Fiscal_year_rejects_inverted_dates() => Assert.Throws<ArgumentException>(() =>
        new FiscalYear("2026", new DateOnly(2026,12,31), new DateOnly(2026,1,1)));

    [Fact]
    public void Closed_period_cannot_be_closed_twice()
    {
        var period=new AccountingPeriod(Guid.NewGuid(),1,new DateOnly(2026,1,1),new DateOnly(2026,1,31));
        period.Close("chief",DateTimeOffset.UtcNow);
        Assert.Equal(AccountingPeriodStatus.Closed,period.Status);
        Assert.Throws<InvalidOperationException>(()=>period.Close("chief",DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Reconciliation_supports_partial_and_total_matching()
    {
        var party=Guid.NewGuid();
        var total=new Reconciliation("L001",party,[new(Guid.NewGuid(),ReconciliationSide.Debit,100m),new(Guid.NewGuid(),ReconciliationSide.Credit,100m)]);
        var partial=new Reconciliation("L002",party,[new(Guid.NewGuid(),ReconciliationSide.Debit,100m),new(Guid.NewGuid(),ReconciliationSide.Credit,60m)]);
        Assert.Equal(ReconciliationStatus.Complete,total.Status);
        Assert.Equal(ReconciliationStatus.Partial,partial.Status);
        Assert.Equal(60m,partial.MatchedAmount);
    }

    [Fact]
    public void Journal_sequence_allocates_strictly_increasing_numbers()
    {
        var sequence=new JournalSequence("VE",Guid.NewGuid());
        Assert.Equal(1,sequence.Next());
        Assert.Equal(2,sequence.Next());
    }

    [Fact]
    public void Reversal_preserves_the_auxiliary_party()
    {
        var party=Guid.NewGuid();
        var entry=new JournalEntry(new DateOnly(2026,1,15),"VE","Facture");
        entry.ReplaceLines([new("411000","Client",100m,0m,party),new("706000","Vente",0m,100m)]);
        entry.Post("tester",DateTimeOffset.UtcNow);
        var reversal=entry.CreateReversal(null,null,"tester",DateTimeOffset.UtcNow);
        Assert.Equal(party,reversal.Lines.Single(x=>x.AccountCode=="411000").PartyId);
    }
}
