using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Identity;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Domain.Organization;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Infrastructure.Identity;

/// <summary>
/// Cote serveur de l'administration du perimetre utilisateur <-> unite. Il passe par
/// <c>dbContext.Set&lt;UserUnitAssignment&gt;()</c> et non par un DbSet nomme : le contexte n'est
/// pas touche par ce lot (voir UserUnitAssignmentConfiguration).
/// </summary>
public sealed class UserUnitAssignmentService(
    RaqmiDbContext dbContext,
    IAuditLogWriter auditLogWriter) : IUserUnitAssignmentService
{
    private const string AuditEntityName = "security.users";

    public async Task<ApplicationResult<UserUnitScopeResponse>> GetAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Id == userId, cancellationToken);

        if (user is null)
        {
            return ApplicationResult<UserUnitScopeResponse>.NotFound("User was not found.");
        }

        return ApplicationResult<UserUnitScopeResponse>.Success(await LoadAsync(user, cancellationToken));
    }

    public async Task<ApplicationResult<UserUnitScopeResponse>> SetAsync(
        Guid userId,
        IReadOnlyCollection<string> hotelUnitCodes,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .SingleOrDefaultAsync(current => current.Id == userId, cancellationToken);

        if (user is null)
        {
            return ApplicationResult<UserUnitScopeResponse>.NotFound("User was not found.");
        }

        var requested = NormalizeCodes(hotelUnitCodes);

        if (!requested.Succeeded)
        {
            return ApplicationResult<UserUnitScopeResponse>.Validation(requested.Error!);
        }

        var codes = requested.Value!;

        if (codes.Length > 0)
        {
            // Un code inconnu refuse TOUT le remplacement : l'administrateur ne doit pas se
            // retrouver avec un perimetre plus etroit que celui qu'il croyait enregistrer.
            var known = await dbContext.HotelUnits
                .Where(unit => codes.Contains(unit.Code))
                .Select(unit => unit.Code)
                .ToArrayAsync(cancellationToken);

            var unknown = codes.Except(known, StringComparer.Ordinal).ToArray();

            if (unknown.Length > 0)
            {
                return ApplicationResult<UserUnitScopeResponse>.Validation(
                    $"Unknown hotel unit code(s): {string.Join(", ", unknown)}.");
            }
        }

        var existing = await dbContext.Set<UserUnitAssignment>()
            .Where(assignment => assignment.UserId == userId)
            .ToArrayAsync(cancellationToken);

        var before = existing.Select(assignment => assignment.HotelUnitCode).Order(StringComparer.Ordinal).ToArray();
        var now = DateTimeOffset.UtcNow;

        // Remplacement en ensemble, comme User.SetRoles : ce qui n'est plus demande est retire,
        // ce qui est nouveau est ajoute, ce qui etait deja la garde sa date d'affectation.
        foreach (var assignment in existing.Where(assignment => !codes.Contains(assignment.HotelUnitCode, StringComparer.Ordinal)))
        {
            dbContext.Remove(assignment);
        }

        foreach (var code in codes.Where(code => !existing.Any(assignment => assignment.HotelUnitCode == code)))
        {
            dbContext.Add(new UserUnitAssignment(userId, code, context.UserName, now));
        }

        user.MarkUpdated(context.UserName, now);

        await auditLogWriter.WriteAsync(
            new AuditLogEntry(
                context.UserId,
                context.UserName,
                "security.user.units_changed",
                AuditEntityName,
                user.Id.ToString(),
                context.IpAddress,
                JsonSerializer.Serialize(new
                {
                    user.UserName,
                    Before = before,
                    After = codes,
                    IsGlobal = codes.Length == 0
                })),
            cancellationToken);

        // Flush explicite apres l'audit, comme UserAdministrationService : l'audit sauvegarde
        // deja, mais la persistance ne doit pas dependre en silence de ce detail.
        await dbContext.SaveChangesAsync(cancellationToken);

        return ApplicationResult<UserUnitScopeResponse>.Success(await LoadAsync(user, cancellationToken));
    }

    private async Task<UserUnitScopeResponse> LoadAsync(User user, CancellationToken cancellationToken)
    {
        var assignments = await dbContext.Set<UserUnitAssignment>()
            .AsNoTracking()
            .Where(assignment => assignment.UserId == user.Id)
            .OrderBy(assignment => assignment.HotelUnitCode)
            .ToArrayAsync(cancellationToken);

        return new UserUnitScopeResponse(
            user.Id,
            user.UserName,
            IsGlobal: assignments.Length == 0,
            assignments
                .Select(assignment => new UserUnitAssignmentResponse(
                    assignment.HotelUnitCode,
                    assignment.AssignedAt,
                    assignment.AssignedBy,
                    assignment.ValidFrom,
                    assignment.ValidTo))
                .ToArray());
    }

    /// <summary>
    /// Codes normalises par la regle du Domain (HotelUnit.NormalizeCode : majuscules, 40
    /// caracteres au plus), vides ignores, doublons fondus, tries. Un code trop long est une
    /// erreur de validation et non une exception qui traverse la route.
    /// </summary>
    private static ApplicationResult<string[]> NormalizeCodes(IReadOnlyCollection<string> hotelUnitCodes)
    {
        try
        {
            var codes = hotelUnitCodes
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Select(HotelUnit.NormalizeCode)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();

            return ApplicationResult<string[]>.Success(codes);
        }
        catch (ArgumentException ex)
        {
            return ApplicationResult<string[]>.Validation(ex.Message);
        }
    }
}
