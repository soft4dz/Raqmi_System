using RaqmiSystem.Domain.Accounting;

namespace RaqmiSystem.Tests;

/// <summary>
/// Pure domain coverage of the SCF accounting invariants. Everything asserted here is enforced
/// by the entities themselves, with no database and no service in the way: these rules must hold
/// for every caller, present and future, not only for the ones that go through the API.
/// </summary>
public sealed class AccountingTests
{
    private const string ClientsAccount = "411100";

    private const string SalesAccount = "706100";

    [Fact]
    public void A_line_cannot_carry_a_debit_and_a_credit_at_once()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new JournalEntryLine(ClientsAccount, "Facture 42", 1_000m, 250m));

        Assert.Contains("never both", exception.Message);
    }

    [Fact]
    public void A_line_cannot_be_empty()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new JournalEntryLine(ClientsAccount, "Facture 42", 0m, 0m));

        Assert.Contains("non-zero", exception.Message);
    }

    [Fact]
    public void A_line_refuses_a_negative_amount()
    {
        // The side of the ledger is the sign: a negative debit is a credit written the wrong way
        // and must be refused rather than silently reinterpreted.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new JournalEntryLine(ClientsAccount, "Facture 42", -1_000m, 0m));
    }

    [Fact]
    public void A_line_refuses_more_than_two_decimals()
    {
        Assert.Throws<ArgumentException>(
            () => new JournalEntryLine(ClientsAccount, "Facture 42", 1_000.005m, 0m));
    }

    [Fact]
    public void An_unbalanced_entry_cannot_be_posted_and_stays_a_draft()
    {
        var entry = new JournalEntry(new DateOnly(2026, 4, 15), "VE", "Facture 42");

        entry.ReplaceLines(new[]
        {
            new JournalEntryLine(ClientsAccount, "Client", 11_900m, 0m),
            new JournalEntryLine(SalesAccount, "Vente", 0m, 10_000m)
        });

        Assert.False(entry.IsBalanced);

        var exception = Assert.Throws<InvalidOperationException>(
            () => entry.Post("comptable", DateTimeOffset.UtcNow));

        Assert.Contains("balanced", exception.Message);

        // A refused posting leaves the entry exactly as it was - still editable.
        Assert.Equal(EntryStatus.Draft, entry.Status);
        Assert.True(entry.CanEdit);
        Assert.Null(entry.PostedAt);
    }

    [Fact]
    public void A_single_line_entry_cannot_be_posted()
    {
        var entry = new JournalEntry(new DateOnly(2026, 4, 15), "OD", "Ecriture unilaterale");

        // A lone line has no counterpart, which is precisely what double entry forbids - the
        // line-count check fires before the balance check, so this is the error we expect.
        entry.ReplaceLines(new[] { new JournalEntryLine(ClientsAccount, "Client", 0m, 500m) });

        var exception = Assert.Throws<InvalidOperationException>(
            () => entry.Post("comptable", DateTimeOffset.UtcNow));

        Assert.Contains("two lines", exception.Message);
        Assert.Equal(EntryStatus.Draft, entry.Status);
    }

    [Fact]
    public void A_posted_entry_is_immutable()
    {
        var entry = BuildPostedEntry();

        var linesFailure = Assert.Throws<InvalidOperationException>(() => entry.ReplaceLines(new[]
        {
            new JournalEntryLine(ClientsAccount, "Client", 1m, 0m),
            new JournalEntryLine(SalesAccount, "Vente", 0m, 1m)
        }));

        Assert.Contains("immutable", linesFailure.Message);

        var headerFailure = Assert.Throws<InvalidOperationException>(
            () => entry.UpdateHeader(new DateOnly(2026, 5, 1), "OD", "Reecriture", null));

        Assert.Contains("immutable", headerFailure.Message);

        // Nothing moved.
        Assert.Equal(new DateOnly(2026, 4, 15), entry.EntryDate);
        Assert.Equal(2, entry.Lines.Count);
        Assert.Equal(11_900m, entry.TotalDebit);
    }

    [Fact]
    public void A_posted_entry_cannot_be_cancelled()
    {
        var entry = BuildPostedEntry();

        var exception = Assert.Throws<InvalidOperationException>(
            () => entry.Cancel("Erreur de saisie", "comptable", DateTimeOffset.UtcNow));

        Assert.Contains("reversing entry", exception.Message);
        Assert.Equal(EntryStatus.Posted, entry.Status);
    }

    [Fact]
    public void A_reversal_is_the_exact_mirror_of_the_entry_it_corrects()
    {
        var entry = BuildPostedEntry();
        var now = DateTimeOffset.UtcNow;

        var reversal = entry.CreateReversal(reversalDate: null, reference: null, "comptable", now);

        // The reversal is a real, posted entry - not a flag on the original.
        Assert.Equal(EntryStatus.Posted, reversal.Status);
        Assert.Equal(entry.Id, reversal.ReversesEntryId);
        Assert.Equal(entry.JournalCode, reversal.JournalCode);
        Assert.Equal(entry.EntryDate, reversal.EntryDate);
        Assert.StartsWith("Extourne - ", reversal.Label);

        // Every line is mirrored: same account, debit and credit swapped.
        var original = entry.Lines.OrderBy(line => line.LineNumber).ToArray();
        var mirrored = reversal.Lines.OrderBy(line => line.LineNumber).ToArray();

        Assert.Equal(original.Length, mirrored.Length);

        for (var index = 0; index < original.Length; index++)
        {
            Assert.Equal(original[index].AccountCode, mirrored[index].AccountCode);
            Assert.Equal(original[index].Debit, mirrored[index].Credit);
            Assert.Equal(original[index].Credit, mirrored[index].Debit);
        }

        // The two entries cancel out exactly.
        Assert.Equal(entry.TotalDebit, reversal.TotalCredit);
        Assert.Equal(entry.TotalCredit, reversal.TotalDebit);
        Assert.True(reversal.IsBalanced);

        // The corrected entry stays POSTED and stays in the books - it is flagged, not erased.
        Assert.Equal(EntryStatus.Posted, entry.Status);
        Assert.True(entry.IsReversed);
        Assert.Equal(reversal.Id, entry.ReversedByEntryId);
        Assert.Equal(now, entry.ReversedAt);
    }

    [Fact]
    public void An_entry_can_only_be_reversed_once()
    {
        var entry = BuildPostedEntry();

        entry.CreateReversal(null, null, "comptable", DateTimeOffset.UtcNow);

        var exception = Assert.Throws<InvalidOperationException>(
            () => entry.CreateReversal(null, null, "comptable", DateTimeOffset.UtcNow));

        Assert.Contains("already been reversed", exception.Message);
    }

    [Fact]
    public void A_draft_cannot_be_reversed()
    {
        var entry = new JournalEntry(new DateOnly(2026, 4, 15), "VE", "Facture 42");

        entry.ReplaceLines(new[]
        {
            new JournalEntryLine(ClientsAccount, "Client", 11_900m, 0m),
            new JournalEntryLine(SalesAccount, "Vente", 0m, 11_900m)
        });

        Assert.Throws<InvalidOperationException>(
            () => entry.CreateReversal(null, null, "comptable", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void A_draft_can_be_cancelled_and_then_no_longer_edited()
    {
        var entry = new JournalEntry(new DateOnly(2026, 4, 15), "VE", "Brouillon abandonne");

        entry.Cancel("Saisie en double", "comptable", DateTimeOffset.UtcNow);

        Assert.Equal(EntryStatus.Cancelled, entry.Status);
        Assert.Equal("Saisie en double", entry.CancellationReason);
        Assert.False(entry.CanEdit);

        Assert.Throws<InvalidOperationException>(() => entry.ReplaceLines(Array.Empty<JournalEntryLine>()));
    }

    [Fact]
    public void An_account_class_is_derived_from_its_code()
    {
        var account = new ChartAccount("706100", "Ventes de prestations hotelieres", AccountKind.Revenue);

        Assert.Equal(7, account.AccountClass);
        Assert.Equal("706100", account.Code);
        Assert.True(account.IsActive);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("41A100")]
    [InlineData("811000")]
    [InlineData("011000")]
    [InlineData("4111000000000")]
    public void An_account_code_outside_the_scf_codification_is_refused(string code)
    {
        Assert.Throws<ArgumentException>(() => new ChartAccount(code, "Compte", AccountKind.Asset));
    }

    [Fact]
    public void An_account_kind_must_be_coherent_with_its_class()
    {
        // Class 6 holds charges: it cannot carry a revenue.
        var exception = Assert.Throws<ArgumentException>(
            () => new ChartAccount("606100", "Achats", AccountKind.Revenue));

        Assert.Contains("class 6", exception.Message);

        // ... and the same class accepts the kind it is made for.
        var charge = new ChartAccount("606100", "Achats", AccountKind.Expense);
        Assert.Equal(AccountKind.Expense, charge.Kind);
    }

    [Fact]
    public void Classes_that_hold_both_sides_accept_both_kinds()
    {
        // Class 4 (comptes de tiers) holds receivables AND payables, which is exactly why the
        // kind cannot be deduced from the class and has to be entered.
        Assert.Equal(AccountKind.Asset, new ChartAccount("411100", "Clients", AccountKind.Asset).Kind);
        Assert.Equal(AccountKind.Liability, new ChartAccount("401100", "Fournisseurs", AccountKind.Liability).Kind);

        // Class 1 holds equity and long-term debt.
        Assert.Equal(AccountKind.Equity, new ChartAccount("101000", "Capital emis", AccountKind.Equity).Kind);
        Assert.Equal(AccountKind.Liability, new ChartAccount("164000", "Emprunts", AccountKind.Liability).Kind);

        // Class 2 does not: an immobilisation is an asset, full stop.
        Assert.Throws<ArgumentException>(() => new ChartAccount("213000", "Constructions", AccountKind.Liability));
    }

    [Fact]
    public void The_seven_scf_classes_are_declared_exactly_once_each()
    {
        var classes = AccountClassCatalog.All.Select(definition => definition.AccountClass).ToArray();

        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7 }, classes);
        Assert.All(AccountClassCatalog.All, definition => Assert.NotEmpty(definition.AllowedKinds));
        Assert.All(AccountClassCatalog.All, definition => Assert.False(string.IsNullOrWhiteSpace(definition.Label)));
    }

    private static JournalEntry BuildPostedEntry()
    {
        var entry = new JournalEntry(new DateOnly(2026, 4, 15), "VE", "Facture 42", "FAC-2026-000042");

        entry.ReplaceLines(new[]
        {
            new JournalEntryLine(ClientsAccount, "Client Sonatrach", 11_900m, 0m),
            new JournalEntryLine(SalesAccount, "Prestations hotelieres", 0m, 11_900m)
        });

        entry.Post("comptable", DateTimeOffset.UtcNow);

        return entry;
    }
}
