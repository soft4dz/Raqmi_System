namespace RaqmiSystem.Application.Security;

/// <summary>
/// Lit le perimetre d'un utilisateur EN BASE, a l'instant demande : c'est la source d'autorite
/// que le jeton photographie. L'emission d'un jeton (connexion) et sa reemission (refresh)
/// passent toutes deux par cette lecture plutot que par une copie de l'ancien jeton, pour
/// qu'une affectation retiree disparaisse au refresh suivant sans attendre une reconnexion.
/// </summary>
public interface IUnitScopeProvider
{
    Task<IUnitScope> GetForUserAsync(Guid userId, DateTimeOffset utcNow, CancellationToken cancellationToken);
}
