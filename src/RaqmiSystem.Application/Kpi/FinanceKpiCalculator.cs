using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Kpi;
using RaqmiSystem.Domain.Receivables;
using RaqmiSystem.Domain.Revenue;
using RaqmiSystem.Domain.Treasury;

namespace RaqmiSystem.Application.Kpi;

/// <summary>
/// Les indicateurs financiers : chiffre d'affaires et sa ventilation, confrontation au budget,
/// tresorerie, creances, et le compte de resultat d'exploitation (GOP, EBE, marges) reconstruit
/// a partir des ecritures comptabilisees.
///
/// TROIS REGLES DE COMPTAGE, toutes reprises des modules proprietaires plutot que redecidees :
/// - RECETTES : seule une recette journaliere au statut Validee est du chiffre d'affaires.
/// - ENCAISSEMENTS : seul un encaissement Confirme est de l'argent entre ; seul un ordre de
///   paiement Regle est de l'argent sorti.
/// - CREANCES : seule une facture Emise, datee au plus tard a la fin de la periode, est due ;
///   son age court depuis la date de facture, faute d'echeance dans le systeme.
///
/// LE COMPTE DE RESULTAT NE S'INVENTE PAS. Le GOP et l'EBE sont construits a partir du mapping
/// de comptes configure par l'etablissement (<c>KpiAccountMapping</c>). Tant qu'aucune regle
/// n'est saisie, ces indicateurs repondent "donnee manquante" et disent quoi configurer. Sans ce
/// classement, un "resultat" affiche ne serait que le resultat comptable complet presente sous
/// le nom de GOP : un chiffre faux sous un nom juste, ce qui est pire qu'aucun chiffre.
/// </summary>
public sealed class FinanceKpiCalculator
{
    private const string NoRevenue = "Aucun chiffre d'affaires valide sur la periode.";

    private const string NoBudget =
        "Aucun budget fige (approuve ou cloture) ne couvre cette periode pour ce perimetre.";

    private const string NoMapping =
        "Aucun rattachement de comptes n'est configure : le moteur ne peut pas distinguer les "
        + "produits, les charges departementales, les charges non reparties et les charges fixes. "
        + "Configurez le mapping du plan comptable dans le parametrage de la bibliotheque KPI.";

    private const string NoCreditRevenue =
        "Aucune facture emise sur la periode : le delai de reglement n'a pas d'objet.";

    private const string NoReceivable = "Aucune creance en cours a la fin de la periode.";

    private const string NoCapacity = "Aucune nuitee disponible sur la periode.";

    private const string NoOccupancy = "Aucune nuitee occupee sur la periode.";

    public IEnumerable<KpiMeasure> Compute(
        KpiPeriod period,
        string? unitCode,
        KpiFactSet facts,
        KpiCapacity capacity,
        DateOnly today,
        KpiDsoMethod dsoMethod)
    {
        ArgumentNullException.ThrowIfNull(period);
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(capacity);

        foreach (var measure in ComputeRevenue(period, unitCode, facts))
        {
            yield return measure;
        }

        foreach (var measure in ComputeReceivables(period, unitCode, facts, dsoMethod))
        {
            yield return measure;
        }

        yield return KpiMeasure.Amount(
            KpiCodes.CashIn,
            unitCode,
            facts.Receipts
                .Where(receipt => receipt.Status == ReceiptStatus.Confirmed)
                .Sum(receipt => receipt.Amount));

        // Au-dela d'ici, tout est GROUPE : ni les ordres de paiement ni les ecritures comptables
        // ne portent d'unite hoteliere. L'appelant n'invoque donc cette partie que pour le
        // groupe ; par unite, le moteur emet des mesures "sans objet" avec la raison.
        if (unitCode is not null)
        {
            yield break;
        }

        foreach (var measure in ComputeTreasury(period, facts, today))
        {
            yield return measure;
        }

        foreach (var measure in ComputeOperatingResult(unitCode, facts, capacity))
        {
            yield return measure;
        }
    }

    private static IEnumerable<KpiMeasure> ComputeRevenue(
        KpiPeriod period,
        string? unitCode,
        KpiFactSet facts)
    {
        var validated = facts.Revenues
            .Where(revenue => revenue.Status == DailyRevenueStatus.Validated)
            .ToArray();

        var total = validated.Sum(revenue => revenue.Total);

        yield return KpiMeasure.Amount(KpiCodes.RevenueTotal, unitCode, total);
        yield return KpiMeasure.Amount(
            KpiCodes.RevenueAccommodation, unitCode, validated.Sum(revenue => revenue.Accommodation));
        yield return KpiMeasure.Amount(
            KpiCodes.RevenueFood, unitCode, validated.Sum(revenue => revenue.Food));
        yield return KpiMeasure.Amount(
            KpiCodes.RevenueBeverage, unitCode, validated.Sum(revenue => revenue.Beverage));
        yield return KpiMeasure.Amount(
            KpiCodes.RevenueOther, unitCode, validated.Sum(revenue => revenue.Other));

        var target = SumBudgetTargets(period, facts.BudgetTargets);

        if (target is null)
        {
            yield return KpiMeasure.Missing(KpiCodes.RevenueBudgetVariance, unitCode, NoBudget);
            yield return KpiMeasure.Missing(KpiCodes.RevenueBudgetAchievement, unitCode, NoBudget);
            yield break;
        }

        yield return KpiMeasure.Amount(KpiCodes.RevenueBudgetVariance, unitCode, total - target.Value);

        yield return KpiMeasure.Ratio(
            KpiCodes.RevenueBudgetAchievement,
            unitCode,
            total,
            target.Value,
            KpiMath.Percent,
            "L'objectif budgetaire de la periode est nul : le taux de realisation n'a pas d'objet.");
    }

    /// <summary>
    /// Objectif de la periode : la somme des objectifs MENSUELS des mois que la periode touche,
    /// un mois partiellement couvert comptant en entier. Le budget de Raqmi System est mensuel
    /// par construction ; le decouper au jour inventerait une saisonnalite que personne n'a
    /// budgetee. Regle deja posee par le tableau de bord groupe, reprise ici telle quelle.
    ///
    /// Renvoie null - et non zero - quand aucune ligne budgetaire ne couvre le perimetre : une
    /// unite sans plan fige n'a pas d'objectif de zero, elle n'a pas d'objectif.
    /// </summary>
    private static decimal? SumBudgetTargets(
        KpiPeriod period,
        IReadOnlyCollection<KpiBudgetTargetFact> targets)
    {
        if (targets.Count == 0)
        {
            return null;
        }

        var total = 0m;
        var matched = false;

        foreach (var target in targets)
        {
            var firstOfMonth = new DateOnly(target.Year, target.Month, 1);
            var lastOfMonth = firstOfMonth.AddMonths(1).AddDays(-1);

            if (firstOfMonth <= period.To && lastOfMonth >= period.From)
            {
                total += target.AmountTarget;
                matched = true;
            }
        }

        return matched ? total : null;
    }

    private static IEnumerable<KpiMeasure> ComputeReceivables(
        KpiPeriod period,
        string? unitCode,
        KpiFactSet facts,
        KpiDsoMethod dsoMethod)
    {
        // L'encours ne se limite PAS aux factures de la periode : une vieille facture impayee
        // precede la periode et reste pourtant due a sa fin.
        var outstanding = facts.Invoices
            .Where(invoice => invoice.Status == InvoiceStatus.Issued && invoice.InvoiceDate <= period.To)
            .ToArray();

        var receivables = outstanding.Sum(invoice => invoice.TotalInclVat);

        var over90 = outstanding
            .Where(invoice => AgingCalculator.Classify(invoice.InvoiceDate, period.To) == AgingBucket.Over90)
            .Sum(invoice => invoice.TotalInclVat);

        yield return KpiMeasure.Amount(KpiCodes.ReceivablesTotal, unitCode, receivables);
        yield return KpiMeasure.Amount(KpiCodes.ReceivablesOver90, unitCode, over90);

        yield return KpiMeasure.Ratio(
            KpiCodes.ReceivablesOverdueRate, unitCode, over90, receivables, KpiMath.Percent, NoReceivable);

        yield return ComputeDso(period, unitCode, facts, receivables, dsoMethod);
    }

    /// <summary>
    /// Le DSO, selon la methode retenue par l'etablissement.
    ///
    /// Methode simple : encours / chiffre d'affaires facture de la periode x jours de la periode.
    /// Le numerateur porte deja la multiplication par les jours, de sorte que la consolidation
    /// groupe reste un rapport de sommes et non une moyenne de delais - moyenner des DSO donnerait
    /// le meme poids a une unite qui facture un million et a une qui facture mille.
    ///
    /// Methode d'epuisement : on remonte les factures emises de la plus recente a la plus
    /// ancienne jusqu'a absorber l'encours, et le DSO est l'age de la derniere facture consommee.
    /// Elle ne depend pas du volume d'activite de la periode, ce qui la rend juste sur une
    /// exploitation saisonniere - mais elle ne se consolide pas par sommation : la valeur groupe
    /// est donc recalculee sur l'ensemble des factures du groupe, jamais reconstituee a partir
    /// des DSO par unite.
    /// </summary>
    private static KpiMeasure ComputeDso(
        KpiPeriod period,
        string? unitCode,
        KpiFactSet facts,
        decimal receivables,
        KpiDsoMethod dsoMethod)
    {
        if (receivables == 0m)
        {
            return KpiMeasure.Missing(KpiCodes.Dso, unitCode, NoReceivable);
        }

        if (dsoMethod == KpiDsoMethod.CountBack)
        {
            return ComputeCountBackDso(period, unitCode, facts, receivables);
        }

        var creditRevenue = facts.Invoices
            .Where(invoice => invoice.Status == InvoiceStatus.Issued
                && invoice.InvoiceDate >= period.From
                && invoice.InvoiceDate <= period.To)
            .Sum(invoice => invoice.TotalInclVat);

        return KpiMeasure.Ratio(
            KpiCodes.Dso,
            unitCode,
            receivables * period.DayCount,
            creditRevenue,
            KpiMath.Divide,
            NoCreditRevenue);
    }

    private static KpiMeasure ComputeCountBackDso(
        KpiPeriod period,
        string? unitCode,
        KpiFactSet facts,
        decimal receivables)
    {
        var issuedDescending = facts.Invoices
            .Where(invoice => invoice.Status == InvoiceStatus.Issued && invoice.InvoiceDate <= period.To)
            .OrderByDescending(invoice => invoice.InvoiceDate)
            .ToArray();

        if (issuedDescending.Length == 0)
        {
            return KpiMeasure.Missing(KpiCodes.Dso, unitCode, NoCreditRevenue);
        }

        var remaining = receivables;
        var age = 0;

        foreach (var invoice in issuedDescending)
        {
            age = AgingCalculator.AgeInDays(invoice.InvoiceDate, period.To);
            remaining -= invoice.TotalInclVat;

            if (remaining <= 0m)
            {
                break;
            }
        }

        // L'encours n'a pas pu etre entierement absorbe : il depasse tout ce qui a ete facture,
        // ce qui arrive quand l'historique charge ne remonte pas assez loin. La valeur est donnee
        // - c'est un plancher - mais signalee comme partielle plutot que presentee comme exacte.
        var measure = new KpiMeasure(
            KpiCodes.Dso,
            unitCode,
            KpiMath.Round(age),
            KpiMath.Round(receivables),
            null,
            KpiQuality.Valid,
            []);

        return remaining > 0m
            ? measure.WithWarning(
                "L'encours depasse le total des factures connues : le delai calcule est un minimum.")
            : measure;
    }

    private static IEnumerable<KpiMeasure> ComputeTreasury(
        KpiPeriod period,
        KpiFactSet facts,
        DateOnly today)
    {
        // Argent sorti : la date qui compte est celle du REGLEMENT, pas celle de l'ordre. Un
        // ordre saisi en mars et paye en avril est une sortie d'avril.
        var cashOut = facts.PaymentOrders
            .Where(order => order.Status == PaymentOrderStatus.Paid
                && order.PaidOn is not null
                && order.PaidOn.Value >= period.From
                && order.PaidOn.Value <= period.To)
            .Sum(order => order.Amount);

        var cashIn = facts.Receipts
            .Where(receipt => receipt.Status == ReceiptStatus.Confirmed)
            .Sum(receipt => receipt.Amount);

        yield return KpiMeasure.Amount(KpiCodes.CashOut, null, cashOut);
        yield return KpiMeasure.Amount(KpiCodes.OperatingCashFlow, null, cashIn - cashOut);

        // Engagements a echeance : ce qu'il faudra sortir, a partir d'AUJOURD'HUI et non des
        // bornes de la periode analysee - un engagement est une projection, pas une histoire.
        yield return CommittedOutflow(KpiCodes.CommittedOutflow7D, facts, today, 7);
        yield return CommittedOutflow(KpiCodes.CommittedOutflow30D, facts, today, 30);
        yield return CommittedOutflow(KpiCodes.CommittedOutflow90D, facts, today, 90);
    }

    private static KpiMeasure CommittedOutflow(string code, KpiFactSet facts, DateOnly today, int horizonDays)
    {
        var horizon = today.AddDays(horizonDays);

        var committed = facts.PaymentOrders
            .Where(order => order.Status == PaymentOrderStatus.Approved
                && order.DueDate >= today
                && order.DueDate <= horizon)
            .Sum(order => order.Amount);

        return KpiMeasure.Amount(code, null, committed);
    }

    /// <summary>
    /// Le compte de resultat d'exploitation, reconstruit selon la grammaire du controle de
    /// gestion hotelier :
    /// <code>
    /// Produits - Charges departementales                  = marge brute
    /// marge brute - Charges non reparties                 = GOP
    /// GOP - Charges fixes de propriete                    = EBE
    /// </code>
    /// Les produits sont pris en solde crediteur (credit - debit) et les charges en solde
    /// debiteur (debit - credit), de sorte qu'un avoir ou une extourne diminue naturellement le
    /// poste qu'il corrige au lieu de s'y ajouter.
    /// </summary>
    private static IEnumerable<KpiMeasure> ComputeOperatingResult(
        string? unitCode,
        KpiFactSet facts,
        KpiCapacity capacity)
    {
        if (facts.AccountRules.Count == 0)
        {
            yield return KpiMeasure.Missing(KpiCodes.GrossOperatingProfit, unitCode, NoMapping);
            yield return KpiMeasure.Missing(KpiCodes.Ebitda, unitCode, NoMapping);
            yield return KpiMeasure.Missing(KpiCodes.GrossMarginRate, unitCode, NoMapping);
            yield return KpiMeasure.Missing(KpiCodes.OperatingMarginRate, unitCode, NoMapping);
            yield return KpiMeasure.Missing(KpiCodes.GopPar, unitCode, NoMapping);
            yield return KpiMeasure.Missing(KpiCodes.Cpor, unitCode, NoMapping);
            yield break;
        }

        var byGroup = SumByGroup(facts);

        var revenue = byGroup.GetValueOrDefault(KpiAccountGroup.Revenue);
        var departmental = byGroup.GetValueOrDefault(KpiAccountGroup.DepartmentalExpense);
        var undistributed = byGroup.GetValueOrDefault(KpiAccountGroup.UndistributedExpense);
        var fixedCharges = byGroup.GetValueOrDefault(KpiAccountGroup.FixedCharge);

        var grossMargin = revenue - departmental;
        var gop = grossMargin - undistributed;
        var ebitda = gop - fixedCharges;
        var operatingExpenses = departmental + undistributed;

        yield return KpiMeasure.Amount(KpiCodes.GrossOperatingProfit, unitCode, gop);
        yield return KpiMeasure.Amount(KpiCodes.Ebitda, unitCode, ebitda);

        yield return KpiMeasure.Ratio(
            KpiCodes.GrossMarginRate, unitCode, grossMargin, revenue, KpiMath.Percent, NoRevenue);

        yield return KpiMeasure.Ratio(
            KpiCodes.OperatingMarginRate, unitCode, gop, revenue, KpiMath.Percent, NoRevenue);

        yield return KpiMeasure.Ratio(
            KpiCodes.GopPar, unitCode, gop, capacity.AvailableNights, KpiMath.Divide, NoCapacity);

        yield return KpiMeasure.Ratio(
            KpiCodes.Cpor, unitCode, operatingExpenses, capacity.OccupiedNights, KpiMath.Divide, NoOccupancy);
    }

    /// <summary>
    /// Solde de chaque groupe de gestion. Le rattachement d'une ligne suit la regle du PREFIXE
    /// LE PLUS LONG : declarer "6" en charges non reparties puis "603" en charges
    /// departementales est une facon legitime d'ecrire une exception, et c'est l'exception qui
    /// doit gagner. Une ligne dont le compte n'est couvert par aucun prefixe est simplement
    /// ignoree - elle appartient au bilan ou a un poste hors exploitation.
    /// </summary>
    private static Dictionary<KpiAccountGroup, decimal> SumByGroup(KpiFactSet facts)
    {
        var rules = facts.AccountRules
            .OrderByDescending(rule => rule.AccountPrefix.Length)
            .ToArray();

        var totals = new Dictionary<KpiAccountGroup, decimal>();

        foreach (var line in facts.LedgerLines)
        {
            var rule = rules.FirstOrDefault(candidate =>
                line.AccountCode.StartsWith(candidate.AccountPrefix, StringComparison.Ordinal));

            if (rule is null)
            {
                continue;
            }

            // Les produits vivent au credit, les charges au debit : chaque groupe est somme dans
            // son sens naturel pour que tous les postes ressortent positifs.
            var amount = rule.Group == KpiAccountGroup.Revenue
                ? line.Credit - line.Debit
                : line.Debit - line.Credit;

            totals[rule.Group] = totals.GetValueOrDefault(rule.Group) + amount;
        }

        return totals;
    }
}
