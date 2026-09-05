using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Security;

namespace RaqmiSystem.Application.Identity;

/// <summary>
/// Administration du perimetre utilisateur <-> unite (lot 2.2). Deux operations, sur le modele
/// de l'affectation des roles : lire, et REMPLACER l'ensemble. La regle « aucune affectation =
/// global » vit dans le Domain (UserUnitAssignment) ; ici on ne fait que la servir.
///
/// Un code inconnu est une erreur de validation, jamais ignore en silence : un administrateur
/// qui se trompe de code ne doit pas enregistrer un perimetre plus etroit que prevu sans le
/// savoir. Chaque remplacement est audite avec l'avant et l'apres, comme les roles.
/// </summary>
public interface IUserUnitAssignmentService
{
    Task<ApplicationResult<UserUnitScopeResponse>> GetAsync(Guid userId, CancellationToken cancellationToken);

    Task<ApplicationResult<UserUnitScopeResponse>> SetAsync(
        Guid userId,
        IReadOnlyCollection<string> hotelUnitCodes,
        OperationContext context,
        CancellationToken cancellationToken);
}
