namespace RaqmiSystem.Tests.Postgres;

/// <summary>
/// Point d'entree unique de la configuration des tests sur PostgreSQL reel.
///
/// Toute la suite (914 tests) tourne sur SQLite ou InMemory et ne connait pas PostgreSQL. Ce
/// dossier est la seule exception : ses tests exercent les migrations, les contraintes et
/// l'isolation Serializable du VRAI fournisseur, parce que c'est exactement ce que SQLite
/// n'imite pas (risque R10 du dossier de reorganisation). Ils ne doivent pourtant jamais rendre
/// <c>dotnet test</c> rouge sur un poste sans base : la variable d'environnement
/// <see cref="ConnectionStringVariable"/> est donc le seul interrupteur. Absente, chaque test de
/// la collection est marque Skipped (voir <see cref="PostgresFactAttribute"/>) et la fixture ne
/// tente aucune connexion.
/// </summary>
public static class PostgresTestEnvironment
{
    /// <summary>
    /// Chaine de connexion Npgsql d'un role autorise a creer et supprimer des bases (CREATEDB).
    /// La base nommee dans cette chaine sert uniquement de point d'entree administratif : les
    /// tests creent leurs propres bases <c>raqmi_test_*</c> et les suppriment en sortie.
    /// </summary>
    public const string ConnectionStringVariable = "RAQMI_TEST_POSTGRES";

    /// <summary>
    /// Motif affiche par le lanceur de tests pour chaque test ignore : il doit dire au
    /// developpeur QUOI faire, pas seulement que rien ne s'est passe.
    /// </summary>
    public const string SkipReason =
        "RAQMI_TEST_POSTGRES n'est pas defini : test sur PostgreSQL reel ignore. Lancez " +
        "tests/run-postgres-tests.ps1 (Docker) ou definissez la variable avec la chaine de connexion " +
        "Npgsql d'un role pouvant creer des bases.";

    /// <summary>
    /// La chaine de connexion administrative, ou null quand la variable est absente ou vide.
    /// </summary>
    public static string? AdminConnectionString
    {
        get
        {
            var value = Environment.GetEnvironmentVariable(ConnectionStringVariable);

            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }

    public static bool IsConfigured => AdminConnectionString is not null;
}
