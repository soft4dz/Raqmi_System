using RaqmiSystem.Application.Kpi;
using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Kpi;
using RaqmiSystem.Domain.Revenue;
using RaqmiSystem.Domain.Treasury;
using static RaqmiSystem.Tests.KpiTestData;

namespace RaqmiSystem.Tests;

/// <summary>
/// Les formules financieres : ventilation du chiffre d'affaires, budget, creances, DSO,
/// tresorerie et compte de resultat d'exploitation.
/// </summary>
public sealed class FinanceKpiCalculatorTests
{
    private readonly FinanceKpiCalculator calculator = new();

    private static readonly DateOnly Today = new(2026, 2, 1);

    private IReadOnlyDictionary<string, KpiMeasure> Compute(
        KpiFactSet facts,
        string? unitCode = UnitA,
        KpiCapacity? capacity = null,
        KpiDsoMethod dsoMethod = KpiDsoMethod.Simple)
    {
        return calculator
            .Compute(January, unitCode, facts, capacity ?? KpiCapacity.Empty, Today, dsoMethod)
            .ToDictionary(measure => measure.Code);
    }

    [Fact]
    public void Revenue_is_split_across_the_four_columns_and_totals()
    {
        var facts = Facts(revenues:
        [
            Revenue(Jan1, accommodation: 100m, food: 50m, beverage: 30m, other: 20m),
            Revenue(Jan31, accommodation: 200m)
        ]);

        var measures = Compute(facts);

        Assert.Equal(400m, measures[KpiCodes.RevenueTotal].Value);
        Assert.Equal(300m, measures[KpiCodes.RevenueAccommodation].Value);
        Assert.Equal(50m, measures[KpiCodes.RevenueFood].Value);
        Assert.Equal(30m, measures[KpiCodes.RevenueBeverage].Value);
        Assert.Equal(20m, measures[KpiCodes.RevenueOther].Value);
    }

    [Fact]
    public void A_unit_without_a_frozen_budget_has_no_target_not_a_zero_target()
    {
        var measures = Compute(Facts(revenues: [Revenue(Jan1, accommodation: 1_000m)]));

        var variance = measures[KpiCodes.RevenueBudgetVariance];

        Assert.Null(variance.Value);
        Assert.Equal(KpiQuality.MissingData, variance.Quality);
        Assert.Null(measures[KpiCodes.RevenueBudgetAchievement].Value);
    }

    [Fact]
    public void A_month_the_period_touches_counts_in_full()
    {
        // Le budget est mensuel par construction ; le decouper au jour inventerait une
        // saisonnalite que personne n'a budgetee.
        var facts = Facts(
            revenues: [Revenue(Jan1, accommodation: 800m)],
            budgetTargets: [BudgetTarget(1_000m, month: 1)]);

        var measures = Compute(facts);

        Assert.Equal(-200m, measures[KpiCodes.RevenueBudgetVariance].Value);
        Assert.Equal(80m, measures[KpiCodes.RevenueBudgetAchievement].Value);
    }

    [Fact]
    public void Only_issued_invoices_dated_up_to_the_period_end_are_outstanding()
    {
        var facts = Facts(invoices:
        [
            Invoice(new DateOnly(2025, 11, 1), 500m),
            Invoice(Jan1, 300m),
            Invoice(Jan1, 900m, status: InvoiceStatus.Paid),
            Invoice(new DateOnly(2026, 3, 1), 700m)
        ]);

        var measures = Compute(facts);

        Assert.Equal(800m, measures[KpiCodes.ReceivablesTotal].Value);

        // La facture de novembre a plus de 90 jours au 31 janvier ; celle du 1er janvier non.
        Assert.Equal(500m, measures[KpiCodes.ReceivablesOver90].Value);
        Assert.Equal(62.5m, measures[KpiCodes.ReceivablesOverdueRate].Value);
    }

    [Fact]
    public void Simple_dso_is_receivables_over_credit_revenue_times_period_days()
    {
        // 3 100 d'encours, 3 100 factures dans le mois, 31 jours : DSO = 31 jours.
        var facts = Facts(invoices: [Invoice(Jan1, 3_100m)]);

        Assert.Equal(31m, Compute(facts)[KpiCodes.Dso].Value);
    }

    [Fact]
    public void Count_back_dso_walks_invoices_backwards_until_the_receivable_is_absorbed()
    {
        // Encours 300 : la facture du 21 janvier (100, age 10 j) puis celle du 11 (200, age 20 j)
        // l'epuisent. Le delai est donc de 20 jours, insensible au volume du mois.
        var facts = Facts(invoices:
        [
            new KpiInvoiceFact(UnitA, "CLI-1", new DateOnly(2026, 1, 21), 100m, InvoiceStatus.Issued),
            new KpiInvoiceFact(UnitA, "CLI-1", new DateOnly(2026, 1, 11), 200m, InvoiceStatus.Issued)
        ]);

        Assert.Equal(20m, Compute(facts, dsoMethod: KpiDsoMethod.CountBack)[KpiCodes.Dso].Value);
    }

    [Fact]
    public void Dso_without_any_receivable_has_no_object()
    {
        var dso = Compute(Facts())[KpiCodes.Dso];

        Assert.Null(dso.Value);
        Assert.Equal(KpiQuality.MissingData, dso.Quality);
    }

    [Fact]
    public void Only_confirmed_receipts_are_money_in()
    {
        var facts = Facts(receipts:
        [
            Receipt(Jan1, 1_000m),
            Receipt(Jan1, 5_000m, status: ReceiptStatus.Draft),
            Receipt(Jan1, 5_000m, status: ReceiptStatus.Cancelled)
        ]);

        Assert.Equal(1_000m, Compute(facts)[KpiCodes.CashIn].Value);
    }

    [Fact]
    public void Cash_out_and_the_operating_flow_exist_only_at_group_level()
    {
        var facts = Facts(
            receipts: [Receipt(Jan1, 1_000m)],
            paymentOrders:
            [
                PaymentOrder(Jan1, Jan31, 400m, PaymentOrderStatus.Paid, paidOn: new DateOnly(2026, 1, 20))
            ]);

        // Par unite : rien, la donnee source ne porte pas d'unite hoteliere.
        Assert.DoesNotContain(KpiCodes.CashOut, Compute(facts).Keys);

        var group = Compute(facts, unitCode: null);

        Assert.Equal(400m, group[KpiCodes.CashOut].Value);
        Assert.Equal(600m, group[KpiCodes.OperatingCashFlow].Value);
    }

    [Fact]
    public void Cash_out_is_dated_by_the_settlement_not_by_the_order()
    {
        // Un ordre saisi en janvier et regle en fevrier est une sortie de fevrier.
        var facts = Facts(paymentOrders:
        [
            PaymentOrder(Jan1, Jan31, 400m, PaymentOrderStatus.Paid, paidOn: new DateOnly(2026, 2, 3))
        ]);

        Assert.Equal(0m, Compute(facts, unitCode: null)[KpiCodes.CashOut].Value);
    }

    [Fact]
    public void Committed_outflows_look_forward_from_today_not_from_the_period()
    {
        var facts = Facts(paymentOrders:
        [
            PaymentOrder(Jan1, Today.AddDays(3), 100m),
            PaymentOrder(Jan1, Today.AddDays(20), 200m),
            PaymentOrder(Jan1, Today.AddDays(60), 400m),
            PaymentOrder(Jan1, Today.AddDays(5), 800m, PaymentOrderStatus.Draft)
        ]);

        var group = Compute(facts, unitCode: null);

        Assert.Equal(100m, group[KpiCodes.CommittedOutflow7D].Value);
        Assert.Equal(300m, group[KpiCodes.CommittedOutflow30D].Value);
        Assert.Equal(700m, group[KpiCodes.CommittedOutflow90D].Value);
    }

    [Fact]
    public void Without_an_account_mapping_the_operating_result_says_so_instead_of_showing_a_number()
    {
        var facts = Facts(ledgerLines: [Ledger("701", 0m, 1_000_000m), Ledger("601", 400_000m, 0m)]);

        var gop = Compute(facts, unitCode: null)[KpiCodes.GrossOperatingProfit];

        Assert.Null(gop.Value);
        Assert.Equal(KpiQuality.MissingData, gop.Quality);
        Assert.Contains(gop.MissingData, reason => reason.Contains("rattachement de comptes", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Gop_and_ebitda_follow_the_configured_account_groups()
    {
        var facts = Facts(
            ledgerLines:
            [
                Ledger("701", 0m, 1_000_000m),
                Ledger("601", 300_000m, 0m),
                Ledger("611", 200_000m, 0m),
                Ledger("613", 100_000m, 0m),
                Ledger("681", 50_000m, 0m)
            ],
            accountRules: StandardAccountRules);

        var measures = Compute(facts, unitCode: null);

        // Produits 1 000 000 - departementales 300 000 - non reparties 200 000 = GOP 500 000.
        // Le compte 613 est capte par le prefixe le plus long et devient une charge FIXE.
        Assert.Equal(500_000m, measures[KpiCodes.GrossOperatingProfit].Value);

        // EBE = GOP - charges fixes ; les dotations restent en dehors des deux.
        Assert.Equal(400_000m, measures[KpiCodes.Ebitda].Value);

        Assert.Equal(70m, measures[KpiCodes.GrossMarginRate].Value);
        Assert.Equal(50m, measures[KpiCodes.OperatingMarginRate].Value);
    }

    [Fact]
    public void A_credit_note_reduces_the_revenue_it_corrects()
    {
        var facts = Facts(
            ledgerLines: [Ledger("701", 0m, 1_000_000m), Ledger("701", 150_000m, 0m)],
            accountRules: StandardAccountRules);

        Assert.Equal(850_000m, Compute(facts, unitCode: null)[KpiCodes.GrossOperatingProfit].Value);
    }

    [Fact]
    public void An_account_covered_by_no_prefix_is_simply_left_out()
    {
        // Un compte de bilan n'a rien a faire dans un compte de resultat d'exploitation.
        var facts = Facts(
            ledgerLines: [Ledger("701", 0m, 1_000m), Ledger("512", 5_000m, 0m)],
            accountRules: StandardAccountRules);

        Assert.Equal(1_000m, Compute(facts, unitCode: null)[KpiCodes.GrossOperatingProfit].Value);
    }

    [Fact]
    public void Goppar_and_cpor_divide_by_the_capacity_of_the_period()
    {
        var facts = Facts(
            ledgerLines:
            [
                Ledger("701", 0m, 1_000_000m),
                Ledger("601", 300_000m, 0m),
                Ledger("611", 200_000m, 0m)
            ],
            accountRules: StandardAccountRules);

        var measures = Compute(facts, unitCode: null, capacity: new KpiCapacity(1_000, 400));

        Assert.Equal(500m, measures[KpiCodes.GopPar].Value);
        Assert.Equal(1_250m, measures[KpiCodes.Cpor].Value);
    }

    [Fact]
    public void Goppar_without_capacity_is_a_dash_not_a_zero()
    {
        var facts = Facts(
            ledgerLines: [Ledger("701", 0m, 1_000m)],
            accountRules: StandardAccountRules);

        Assert.Null(Compute(facts, unitCode: null)[KpiCodes.GopPar].Value);
    }

    [Fact]
    public void Draft_revenue_never_reaches_a_financial_indicator()
    {
        var facts = Facts(revenues:
        [
            Revenue(Jan1, accommodation: 100m),
            Revenue(Jan1, accommodation: 9_000m, status: DailyRevenueStatus.Draft)
        ]);

        Assert.Equal(100m, Compute(facts)[KpiCodes.RevenueTotal].Value);
    }
}
