using Microsoft.EntityFrameworkCore;
using Npgsql;
using RaqmiSystem.Domain.Accounting;
using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Domain.Organization;
using RaqmiSystem.Domain.Revenue;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Tests.Postgres;

/// <summary>
/// Les contraintes que le schema PostgreSQL tient reellement, provoquees une a une.
///
/// Chaque test fait deux choses : il declenche la violation avec des entites du domaine (ou en
/// SQL brut quand le domaine refuse de construire la donnee illegale, ce qui est precisement le
/// cas que la contrainte de base couvre), puis il verifie que l'exception remontee est celle sur
/// laquelle les services de production s'appuient - code SQLSTATE et nom de contrainte, tels que
/// <see cref="DbUpdateExceptionExtensions"/> les lit pour repondre 409 plutot que 500. SQLite
/// remonte ces memes violations sous une autre forme (message, pas de nom d'index) : c'est ici,
/// et ici seulement, que la lecture Npgsql de ce classifieur est exercee.
///
/// Les donnees sont isolees par des codes uniques par test : la base est partagee par la
/// collection et un nettoyage entre tests ajouterait de l'ordre la ou il n'en faut pas.
/// </summary>
[Collection(PostgresCollection.Name)]
[Trait(PostgresCollection.CategoryTraitName, PostgresCollection.CategoryTraitValue)]
public sealed class PostgresConstraintTests(PostgresDatabaseFixture fixture)
{
    [PostgresFact]
    public async Task Deux_comptes_utilisateur_ne_peuvent_pas_partager_le_meme_email()
    {
        var suffix = Suffix();
        var email = $"doublon-{suffix}@example.com";

        await using (var dbContext = fixture.CreateDbContext())
        {
            dbContext.Users.Add(new User($"premier-{suffix}", email, "Premier compte", "hash", mustChangePassword: false));
            await dbContext.SaveChangesAsync();
        }

        await using var second = fixture.CreateDbContext();

        // Casse differente a dessein : l'unicite porte sur normalized_email, pas sur la saisie.
        second.Users.Add(new User($"second-{suffix}", email.ToUpperInvariant(), "Second compte", "hash", mustChangePassword: false));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());

        AssertPostgresViolation(exception, PostgresErrorCodes.UniqueViolation, "IX_users_normalized_email");
        Assert.True(exception.IsUniqueViolation("IX_users_normalized_email"));
    }

    [PostgresFact]
    public async Task Deux_factures_emises_ne_peuvent_pas_porter_le_meme_numero()
    {
        var suffix = Suffix();
        var unitCode = $"PGU{suffix}";
        var customerCode = $"PGC{suffix}";

        // Annee et sequence tirees au hasard dans la plage legale : la base est neuve a chaque
        // execution, seul ce test emet des factures, et deux executions ne se rencontrent jamais.
        var year = Random.Shared.Next(2000, 3000);
        var sequence = Random.Shared.Next(1, 1_000_000);

        await using (var dbContext = fixture.CreateDbContext())
        {
            dbContext.HotelUnits.Add(new HotelUnit(unitCode, "Unite PostgreSQL", HotelUnitType.Hotel));
            dbContext.Customers.Add(new Customer(customerCode, "Client PostgreSQL", CustomerType.Company));
            dbContext.Invoices.Add(BuildIssuedInvoice(customerCode, unitCode, year, sequence));
            await dbContext.SaveChangesAsync();
        }

        await using var second = fixture.CreateDbContext();
        second.Invoices.Add(BuildIssuedInvoice(customerCode, unitCode, year, sequence));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());

        // Le numero FAC-{annee}-{sequence} et le couple (annee, sequence) qui le forme sont
        // proteges par deux index uniques ; PostgreSQL signale le premier qu'il verifie. L'un
        // comme l'autre est exactement ce que BillingService.IssueInvoiceAsync attend pour
        // rejouer l'allocation, puis repondre 409.
        var postgres = AssertPostgresViolation(exception, PostgresErrorCodes.UniqueViolation);

        Assert.Contains(postgres.ConstraintName, new[] { "ux_invoices_number", "ux_invoices_issued_year_sequence" });
        Assert.True(exception.IsUniqueViolation());
    }

    [PostgresFact]
    public async Task Un_seul_chiffre_d_affaires_par_jour_et_par_unite()
    {
        var suffix = Suffix();
        var unitCode = $"PGR{suffix}";
        var businessDate = new DateOnly(2030, 6, 15);

        await using (var dbContext = fixture.CreateDbContext())
        {
            dbContext.HotelUnits.Add(new HotelUnit(unitCode, "Unite CA journalier", HotelUnitType.Hotel));
            dbContext.DailyRevenues.Add(new DailyRevenue(businessDate, unitCode, 120_000m, 30_000m, 12_000m, 0m));
            await dbContext.SaveChangesAsync();
        }

        await using var second = fixture.CreateDbContext();
        second.DailyRevenues.Add(new DailyRevenue(businessDate, unitCode, 1m, 0m, 0m, 0m, "Doublon"));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());

        AssertPostgresViolation(
            exception,
            PostgresErrorCodes.UniqueViolation,
            "IX_daily_revenues_business_date_hotel_unit_code");
        Assert.True(exception.IsUniqueViolation("IX_daily_revenues_business_date_hotel_unit_code"));
    }

    [PostgresFact]
    public async Task Une_ligne_d_ecriture_ne_peut_pas_viser_un_compte_absent_du_plan()
    {
        var journalCode = $"T{Suffix()}";

        await using var dbContext = fixture.CreateDbContext();

        dbContext.AccountingJournals.Add(new AccountingJournal(journalCode, "Journal de test"));

        // Les deux codes sont bien formes (classe 7) mais n'existent pas dans accounting.chart_accounts.
        var entry = new JournalEntry(new DateOnly(2030, 1, 15), journalCode, "Ecriture sur comptes fantomes");
        entry.ReplaceLines(
        [
            new JournalEntryLine("799990", "Produit inexistant", 100m, 0m),
            new JournalEntryLine("799991", "Contrepartie inexistante", 0m, 100m)
        ]);

        dbContext.JournalEntries.Add(entry);

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());

        AssertPostgresViolation(
            exception,
            PostgresErrorCodes.ForeignKeyViolation,
            "FK_journal_entry_lines_chart_accounts_account_code");

        // Rien de l'ecriture ne doit rester : l'entete et ses lignes partent dans la meme transaction.
        await using var verification = fixture.CreateDbContext();
        Assert.Equal(0, await verification.JournalEntries.CountAsync(current => current.JournalCode == journalCode));
    }

    [PostgresFact]
    public async Task Une_ligne_d_ecriture_ecrite_hors_du_domaine_ne_peut_porter_ni_deux_cotes_ni_aucun()
    {
        var suffix = Suffix();
        var journalCode = $"T{suffix}";

        // Un code de compte numerique unique par execution : classe 7, puis cinq chiffres.
        var accountCode = $"7{Random.Shared.Next(10_000, 100_000)}";

        Guid entryId;

        await using (var dbContext = fixture.CreateDbContext())
        {
            dbContext.AccountingJournals.Add(new AccountingJournal(journalCode, "Journal de test"));
            dbContext.ChartAccounts.Add(new ChartAccount(accountCode, "Produit de test", AccountKind.Revenue));

            var entry = new JournalEntry(new DateOnly(2030, 1, 15), journalCode, "Ecriture d'accueil");
            entry.ReplaceLines(
            [
                new JournalEntryLine(accountCode, "Debit", 100m, 0m),
                new JournalEntryLine(accountCode, "Credit", 0m, 100m)
            ]);

            dbContext.JournalEntries.Add(entry);
            await dbContext.SaveChangesAsync();

            entryId = entry.Id;
        }

        // Le constructeur de JournalEntryLine refuse ces deux lignes : c'est justement pour la
        // donnee ecrite SANS lui (import, script, correction manuelle) que la contrainte existe.
        // Le seul moyen de la solliciter est donc le SQL brut.
        var illegalAmounts = new (decimal Debit, decimal Credit, string Case)[]
        {
            (100m, 100m, "debit ET credit"),
            (0m, 0m, "ni debit ni credit")
        };

        foreach (var (debit, credit, illegalCase) in illegalAmounts)
        {
            await using var dbContext = fixture.CreateDbContext();

            var exception = await Assert.ThrowsAsync<PostgresException>(() => dbContext.Database.ExecuteSqlAsync(
                $"""
                INSERT INTO accounting.journal_entry_lines
                    (id, journal_entry_id, line_number, account_code, label, debit, credit, party_id)
                VALUES
                    ({Guid.NewGuid()}, {entryId}, 3, {accountCode}, {"Ligne illegale : " + illegalCase}, {debit}, {credit}, NULL)
                """));

            Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
            Assert.Equal("ck_journal_entry_lines_debit_credit_exclusive", exception.ConstraintName);
        }
    }

    private static Invoice BuildIssuedInvoice(string customerCode, string unitCode, int year, int sequence)
    {
        var invoice = new Invoice(customerCode, unitCode, new DateOnly(2030, 3, 1));

        invoice.ReplaceLines([new InvoiceLine("Nuitee", 1m, 10_000m, 19m)]);
        invoice.CaptureCustomerSnapshot("Client PostgreSQL", null, null, null, null, null);
        invoice.CaptureIssuerSnapshot("Hotel PostgreSQL", "098765432112345", null, null, null, null);
        invoice.Issue(year, sequence, "tests", DateTimeOffset.UtcNow);

        return invoice;
    }

    /// <summary>
    /// L'exception de base d'un echec de SaveChanges sur Npgsql est la <see cref="PostgresException"/>
    /// du serveur : c'est elle qui porte SQLSTATE et le nom de la contrainte, et c'est elle que
    /// <see cref="DbUpdateExceptionExtensions"/> inspecte.
    /// </summary>
    private static PostgresException AssertPostgresViolation(
        DbUpdateException exception,
        string expectedSqlState,
        string? expectedConstraintName = null)
    {
        var postgres = Assert.IsType<PostgresException>(exception.GetBaseException());

        Assert.Equal(expectedSqlState, postgres.SqlState);

        if (expectedConstraintName is not null)
        {
            Assert.Equal(expectedConstraintName, postgres.ConstraintName);
        }

        return postgres;
    }

    /// <summary>Six caracteres hexadecimaux : lettres et chiffres seulement, valides dans tous les codes du domaine.</summary>
    private static string Suffix()
    {
        return Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
    }
}
