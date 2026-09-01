namespace RaqmiSystem.Tests.Postgres;

/// <summary>
/// <c>[Fact]</c> qui s'ignore de lui-meme quand aucune base PostgreSQL n'est configuree.
///
/// Le choix de Skip plutot que d'un echec ou d'un simple <c>return</c> est deliberee : un test
/// qui rend vert sans rien executer serait un mensonge dans le rapport, et un test qui echoue
/// sans base rendrait la suite rouge sur chaque poste de developpement. Skipped dit la verite :
/// ce test existe, il n'a pas ete joue ici, et le motif explique comment le jouer.
///
/// La decision se prend a la construction de l'attribut, c'est-a-dire a la DECOUVERTE des tests :
/// la variable d'environnement doit donc etre definie avant le lancement de <c>dotnet test</c>,
/// pas depuis un test.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (!PostgresTestEnvironment.IsConfigured)
        {
            Skip = PostgresTestEnvironment.SkipReason;
        }
    }
}
