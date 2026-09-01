using System.Text.RegularExpressions;
using RaqmiSystem.Application.Navigation;
using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Tests;

/// <summary>
/// Invariants du registre <see cref="PermissionRegistry"/> (lot 2.1) : forme des cles cibles,
/// completude de la couverture des 83 cles historiques, et surtout la regle d'equivalence qui
/// interdit toute extension silencieuse - une cle fine ne vaut jamais la cle historique qui en
/// couvre plusieurs. Ces tests ne passent par aucune base ni aucun serveur : ils fixent la
/// table que Program.cs, le seeder et le rapport de migration lisent tous les trois.
/// </summary>
public sealed class RbacPermissionRegistryTests
{
    private static readonly Regex TargetKeyFormat = new("^[a-z]+\\.[a-z_]+\\.[a-z_]+$", RegexOptions.Compiled);

    /// <summary>
    /// Les huit alias PMS que Program.cs declarait un par un avant le registre. Leur effet -
    /// la cle large historique satisfait la politique de la cle fine historique - doit
    /// survivre au mecanisme general, a l'identique.
    /// </summary>
    private static readonly (string FineKey, string LegacyKey)[] PmsAliases =
    [
        (PermissionCatalog.LodgingReserve, PermissionCatalog.LodgingWrite),
        (PermissionCatalog.LodgingCancel, PermissionCatalog.LodgingWrite),
        (PermissionCatalog.LodgingNoShow, PermissionCatalog.LodgingWrite),
        (PermissionCatalog.LodgingManageRooms, PermissionCatalog.LodgingWrite),
        (PermissionCatalog.LodgingManageRates, PermissionCatalog.LodgingWrite),
        (PermissionCatalog.LodgingNightAudit, PermissionCatalog.LodgingWrite),
        (PermissionCatalog.LodgingRoomMove, PermissionCatalog.LodgingCheckin),
        (PermissionCatalog.LodgingCheckout, PermissionCatalog.LodgingCheckin)
    ];

    /// <summary>Les cles historiques qui couvrent plusieurs cles cibles : composites, jamais equivalentes a une seule cle fine.</summary>
    private static readonly string[] CompositeLegacyKeys =
    [
        PermissionCatalog.UsersWrite,
        PermissionCatalog.AccountingWrite,
        PermissionCatalog.TreasuryWrite,
        PermissionCatalog.LodgingWrite,
        PermissionCatalog.LodgingCheckin,
        PermissionCatalog.InventoryWrite,
        PermissionCatalog.PurchasingWrite,
        PermissionCatalog.HrWrite
    ];

    [Fact]
    public void Target_keys_are_well_formed_unique_and_fully_described()
    {
        var keys = PermissionRegistry.All.Select(target => target.Key).ToArray();

        Assert.Equal(keys.Length, keys.Distinct(StringComparer.Ordinal).Count());

        Assert.All(PermissionRegistry.All, target =>
        {
            Assert.Matches(TargetKeyFormat, target.Key);
            Assert.False(string.IsNullOrWhiteSpace(target.Domain), $"{target.Key} : domaine vide.");
            Assert.False(string.IsNullOrWhiteSpace(target.Resource), $"{target.Key} : ressource vide.");
            Assert.False(string.IsNullOrWhiteSpace(target.Action), $"{target.Key} : action vide.");
            Assert.False(string.IsNullOrWhiteSpace(target.Name), $"{target.Key} : libelle vide.");
            Assert.False(string.IsNullOrWhiteSpace(target.Description), $"{target.Key} : description vide.");

            // La cle EST "prefixe.ressource.action" : la ressource et l'action ne sont pas des
            // metadonnees libres, elles sont lues dans la cle.
            Assert.Equal($"{target.Prefix}.{target.Resource}.{target.Action}", target.Key);
        });
    }

    [Fact]
    public void Target_domains_are_stable_ids_of_the_functional_catalog()
    {
        var knownDomainIds = FunctionalArchitectureCatalog.Domains
            .Select(domain => domain.Id)
            .ToHashSet(StringComparer.Ordinal);

        Assert.All(PermissionRegistry.All, target =>
            Assert.Contains(target.Domain, knownDomainIds));
    }

    [Fact]
    public void Every_target_key_is_in_the_catalog_and_every_legacy_key_exists_in_it()
    {
        var catalogKeys = PermissionCatalog.All.Select(definition => definition.Key).ToHashSet(StringComparer.Ordinal);

        Assert.All(PermissionRegistry.All, target =>
        {
            Assert.Contains(target.Key, catalogKeys);

            Assert.All(target.LegacyKeys, legacyKey =>
            {
                Assert.Contains(legacyKey, catalogKeys);
                Assert.False(PermissionRegistry.IsTargetKey(legacyKey),
                    $"{legacyKey} est declaree historique pour {target.Key} alors qu'elle est une cle cible.");
            });
        });
    }

    [Fact]
    public void No_catalog_key_is_orphan_and_the_historical_keys_are_all_covered()
    {
        // Chaque cle du catalogue est soit une cle cible, soit une cle historique couverte par
        // au moins une cible - il n'existe aucune troisieme categorie.
        Assert.All(PermissionCatalog.All, definition =>
            Assert.True(
                PermissionRegistry.IsTargetKey(definition.Key) || PermissionRegistry.IsLegacyKey(definition.Key),
                $"{definition.Key} n'est ni une cle cible ni une cle historique couverte."));

        // Les 83 definitions historiques sont toutes la, dans le meme etat : hr.payroll.close est
        // deja au format cible et devient sa propre cible, les 82 autres sont couvertes.
        Assert.Equal(83, PermissionCatalog.Legacy.Count);

        var historicalKeys = PermissionCatalog.Legacy.Select(definition => definition.Key).ToArray();
        Assert.Equal(82, historicalKeys.Count(PermissionRegistry.IsLegacyKey));
        Assert.Equal(PermissionCatalog.HrPayrollClose, historicalKeys.Single(PermissionRegistry.IsTargetKey));
        Assert.Empty(PermissionRegistry.LegacyKeysCovering(PermissionCatalog.HrPayrollClose));
        Assert.Equal(82, PermissionRegistry.LegacyKeys.Count);
    }

    [Fact]
    public void A_composite_legacy_key_accepts_only_itself()
    {
        Assert.All(CompositeLegacyKeys, legacyKey =>
        {
            Assert.False(PermissionRegistry.IsOneToOne(legacyKey), $"{legacyKey} devrait etre composite.");
            Assert.True(PermissionRegistry.TargetKeysCoveredBy(legacyKey).Count > 1);

            // La politique de la cle composite : elle-meme, rien d'autre. Aucune de ses cles
            // fines ne la vaut.
            Assert.Equal(new[] { legacyKey }, PermissionRegistry.AcceptedClaims(legacyKey));
        });

        // Et reciproquement, la liste des composites est exactement celle-ci : un nouveau
        // decoupage 1:n doit etre declare ici, en connaissance de cause.
        var composites = PermissionRegistry.LegacyKeys
            .Where(legacyKey => !PermissionRegistry.IsOneToOne(legacyKey))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(CompositeLegacyKeys.Order(StringComparer.Ordinal).ToArray(), composites);
    }

    [Fact]
    public void A_one_to_one_legacy_key_shares_exactly_the_policy_of_its_target()
    {
        var oneToOne = PermissionRegistry.LegacyKeys.Where(PermissionRegistry.IsOneToOne).ToArray();

        Assert.NotEmpty(oneToOne);

        Assert.All(oneToOne, legacyKey =>
        {
            var targetKey = PermissionRegistry.TargetKeysCoveredBy(legacyKey).Single();

            var legacyPolicy = PermissionRegistry.AcceptedClaims(legacyKey).Order(StringComparer.Ordinal).ToArray();
            var targetPolicy = PermissionRegistry.AcceptedClaims(targetKey).Order(StringComparer.Ordinal).ToArray();

            // Retaguer une route de la cle historique vers sa cible ne change jamais qui y accede.
            Assert.Equal(targetPolicy, legacyPolicy);
            Assert.Contains(legacyKey, targetPolicy);
            Assert.Contains(targetKey, targetPolicy);
        });
    }

    [Fact]
    public void A_target_key_is_satisfied_by_itself_or_by_a_covering_legacy_key_only()
    {
        Assert.All(PermissionRegistry.All, target =>
        {
            var expected = new[] { target.Key }
                .Concat(target.LegacyKeys)
                .Order(StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(expected, PermissionRegistry.AcceptedClaims(target.Key).Order(StringComparer.Ordinal).ToArray());
        });
    }

    [Fact]
    public void The_eight_pms_aliases_keep_their_effect_and_the_three_engaging_keys_stay_without_alias()
    {
        Assert.All(PmsAliases, alias =>
        {
            var accepted = PermissionRegistry.AcceptedClaims(alias.FineKey);

            Assert.Contains(alias.FineKey, accepted);
            Assert.Contains(alias.LegacyKey, accepted);

            // La cle large et la cle fine couvrent la meme cible : c'est ainsi que l'alias est
            // exprime, sans declaration a part.
            var targetKey = PermissionRegistry.TargetKeysCoveredBy(alias.FineKey).Single();
            Assert.Contains(targetKey, PermissionRegistry.TargetKeysCoveredBy(alias.LegacyKey));
        });

        // change_rate, override_restriction et overbooking : jamais herites de lodging.write ni
        // de lodging.checkin - ce sont des gestes qui n'existaient pas quand ces cles ont ete
        // donnees.
        foreach (var engagingKey in new[]
        {
            PermissionCatalog.LodgingChangeRate,
            PermissionCatalog.LodgingOverrideRestriction,
            PermissionCatalog.LodgingOverbooking
        })
        {
            var accepted = PermissionRegistry.AcceptedClaims(engagingKey);
            Assert.DoesNotContain(PermissionCatalog.LodgingWrite, accepted);
            Assert.DoesNotContain(PermissionCatalog.LodgingCheckin, accepted);

            var targetKey = PermissionRegistry.TargetKeysCoveredBy(engagingKey).Single();
            Assert.Equal(new[] { engagingKey }, PermissionRegistry.LegacyKeysCovering(targetKey));
        }
    }

    [Fact]
    public void No_legacy_key_is_accepted_by_another_legacy_key_policy_beyond_the_pms_aliases()
    {
        // La seule extension entre cles HISTORIQUES tolere est celle qui existait deja : les
        // huit alias PMS. Toute autre paire serait un droit accorde en silence a un profil
        // existant.
        var knownPairs = PmsAliases
            .Select(alias => (alias.FineKey, alias.LegacyKey))
            .ToHashSet();

        var unexpected = new List<string>();

        foreach (var legacyKey in PermissionRegistry.LegacyKeys)
        {
            var otherLegacyKeysAccepted = PermissionRegistry.AcceptedClaims(legacyKey)
                .Where(claim => claim != legacyKey && PermissionRegistry.IsLegacyKey(claim));

            foreach (var other in otherLegacyKeysAccepted)
            {
                if (!knownPairs.Contains((legacyKey, other)))
                {
                    unexpected.Add($"{legacyKey} accepte {other}");
                }
            }
        }

        Assert.Empty(unexpected);
    }

    [Fact]
    public void A_fine_target_key_never_satisfies_the_policy_of_a_composite_legacy_key()
    {
        Assert.All(CompositeLegacyKeys, legacyKey =>
        {
            var policy = PermissionRegistry.AcceptedClaims(legacyKey);

            Assert.All(PermissionRegistry.TargetKeysCoveredBy(legacyKey), fineKey =>
                Assert.DoesNotContain(fineKey, policy));
        });
    }

    /// <summary>
    /// Les correspondances explicitement citees par le livrable 4, section 4.E : le registre
    /// les reprend telles quelles.
    /// </summary>
    [Theory]
    [InlineData(PermissionCatalog.RevenueValidate, PermissionCatalog.FinanceRevenueValidate)]
    [InlineData(PermissionCatalog.ClosingClose, PermissionCatalog.LodgingClosingClose)]
    [InlineData(PermissionCatalog.TreasuryApprove, PermissionCatalog.FinancePaymentOrderApprove)]
    [InlineData(PermissionCatalog.AccountingPost, PermissionCatalog.FinanceEntryPost)]
    [InlineData(PermissionCatalog.AccountingClose, PermissionCatalog.FinancePeriodClose)]
    [InlineData(PermissionCatalog.AccountingReverse, PermissionCatalog.FinanceEntryReverse)]
    [InlineData(PermissionCatalog.CrmLoyalty, PermissionCatalog.CrmLoyaltyPost)]
    [InlineData(PermissionCatalog.InvoicesIssue, PermissionCatalog.BillingInvoiceIssue)]
    [InlineData(PermissionCatalog.LodgingReserve, PermissionCatalog.LodgingReservationCreate)]
    [InlineData(PermissionCatalog.PurchasingApprove, PermissionCatalog.PurchasingOrderApprove)]
    [InlineData(PermissionCatalog.HrPayroll, PermissionCatalog.HrPayrollProcess)]
    [InlineData(PermissionCatalog.ApprovalsDecide, PermissionCatalog.WorkflowRequestDecide)]
    [InlineData(PermissionCatalog.SecuritySeed, PermissionCatalog.AdminSecuritySeed)]
    [InlineData(PermissionCatalog.AuditRead, PermissionCatalog.AuditLogRead)]
    [InlineData(PermissionCatalog.SyncRead, PermissionCatalog.SystemWorkstationRead)]
    public void The_documented_one_to_one_mappings_are_in_the_registry(string legacyKey, string targetKey)
    {
        Assert.True(PermissionRegistry.IsOneToOne(legacyKey));
        Assert.Equal(targetKey, PermissionRegistry.TargetKeysCoveredBy(legacyKey).Single());
    }

    [Fact]
    public void The_documented_users_write_split_is_in_the_registry()
    {
        Assert.Equal(
            new[] { PermissionCatalog.AdminUserCreate, PermissionCatalog.AdminUserDeactivate, PermissionCatalog.AdminUserUpdate },
            PermissionRegistry.TargetKeysCoveredBy(PermissionCatalog.UsersWrite).Order(StringComparer.Ordinal).ToArray());

        // lodging.checkin ("operer le comptoir") couvre l'arrivee, le depart, le changement de
        // chambre et la tenue des folios - le depart et le changement de chambre etant aussi
        // couverts par leur propre cle fine historique.
        Assert.Equal(
            new[]
            {
                PermissionCatalog.LodgingCheckinExecute,
                PermissionCatalog.LodgingCheckoutExecute,
                PermissionCatalog.LodgingFolioManage,
                PermissionCatalog.LodgingStayMove
            },
            PermissionRegistry.TargetKeysCoveredBy(PermissionCatalog.LodgingCheckin).Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void An_unknown_key_is_accepted_by_itself_only()
    {
        // Une cle hors registre (permission locale creee a la main) garde le comportement
        // d'avant : sa politique n'accepte qu'elle.
        Assert.Equal(new[] { "custom.local" }, PermissionRegistry.AcceptedClaims("custom.local"));
        Assert.False(PermissionRegistry.IsLegacyKey("custom.local"));
        Assert.False(PermissionRegistry.IsTargetKey("custom.local"));
        Assert.Null(PermissionRegistry.Find("custom.local"));
    }
}
