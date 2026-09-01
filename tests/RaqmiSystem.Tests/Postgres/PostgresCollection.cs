namespace RaqmiSystem.Tests.Postgres;

/// <summary>
/// La collection xUnit « Postgres » : toutes les classes de test qui s'y rattachent partagent une
/// seule <see cref="PostgresDatabaseFixture"/> (donc une seule base migree par execution) et
/// s'executent EN SERIE. C'est voulu : les tests de concurrence ouvrent deux transactions
/// Serializable sur la meme base, et un troisieme test qui ecrirait au meme moment fausserait
/// le resultat. Les autres collections (SQLite, InMemory) continuent de tourner en parallele.
///
/// Le trait <c>Category=Postgres</c> est porte par chaque classe de la collection : c'est le
/// filtre que la CI et <c>tests/run-postgres-tests.ps1</c> passent a <c>dotnet test</c>.
/// </summary>
[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresDatabaseFixture>
{
    public const string Name = "Postgres";

    public const string CategoryTraitName = "Category";

    public const string CategoryTraitValue = "Postgres";
}
