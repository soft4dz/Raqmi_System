using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Application.Sync;
using RaqmiSystem.Domain.Sync;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Infrastructure.Sync;

/// <summary>
/// Supervision des postes deployes. Ce service NE SYNCHRONISE RIEN et ne doit jamais evoluer vers
/// cela : tous les postes ecrivent dans la MEME base PostgreSQL a travers la MEME API
/// (docs/architecture.md : "One central PostgreSQL database per deployment"), il n'existe donc
/// aucun etat divergent a reconcilier. Le nom historique du module vient de l'ancien produit
/// Electron, ou chaque poste portait sa propre base SQLite locale ; cette premisse a disparu.
///
/// Deux refus assumes, qui sont la raison d'etre de ce perimetre :
///
///   * AUCUNE FILE DE REJEU. Aujourd'hui une coupure reseau fait perdre une action BRUYAMMENT :
///     bandeau rouge, l'operateur refait sa saisie. Une file transformerait ce mode d'echec en
///     DOUBLON SILENCIEUX sur des routes qui creent de l'argent (encaissements, ordres de
///     paiement, ecritures comptables) et qui ne portent aucune protection d'unicite en base. Un
///     encaissement en double est un incident comptable ; une ressaisie n'en est pas un.
///
///   * AUCUNE ECRITURE DANS LE JOURNAL D'AUDIT. Un battement toutes les cinq minutes et par poste
///     noierait la piste d'audit sous du bruit technique, au point de la rendre inutilisable pour
///     ce a quoi elle sert : retrouver un acte de gestion. Les tables de ce module SONT le
///     journal de leur propre activite.
/// </summary>
public sealed class SyncSupervisionService(RaqmiDbContext dbContext) : ISyncSupervisionService
{
    /// <summary>Au-dela, un poste est dit "sans contact recent".</summary>
    public const int StaleAfterMinutes = 15;

    /// <summary>Au-dela, un poste est dit "silencieux".</summary>
    public const int OfflineAfterMinutes = 60;

    /// <summary>Fenetre par defaut du registre : un poste plus ancien est masque sauf demande.</summary>
    public const int DefaultWindowDays = 30;

    /// <summary>Taille maximale d'un lot d'erreurs remontees en une fois.</summary>
    public const int MaxBatchSize = 50;

    /// <summary>Plafond dur de lecture du journal, quoi que demande l'appelant.</summary>
    public const int MaxFailurePageSize = 200;

    public async Task<ApplicationResult<WorkstationResponse>> HeartbeatAsync(
        WorkstationHeartbeatRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        if (request.StationId == Guid.Empty)
        {
            return ApplicationResult<WorkstationResponse>.Validation("L'identifiant du poste est requis.");
        }

        if (string.IsNullOrWhiteSpace(request.Label))
        {
            return ApplicationResult<WorkstationResponse>.Validation("Le nom du poste est requis.");
        }

        if (string.IsNullOrWhiteSpace(request.AppVersion))
        {
            return ApplicationResult<WorkstationResponse>.Validation("La version applicative est requise.");
        }

        // Le nom d'utilisateur vient du CONTEXTE (donc du jeton), jamais du corps de la requete :
        // un poste ne doit pas pouvoir attribuer son activite a quelqu'un d'autre.
        var userName = string.IsNullOrWhiteSpace(context.UserName) ? "inconnu" : context.UserName;
        var nowUtc = DateTime.UtcNow;

        var workstation = await dbContext.Workstations
            .FirstOrDefaultAsync(item => item.Id == request.StationId, cancellationToken);

        if (workstation is null)
        {
            workstation = Workstation.Register(
                request.StationId,
                request.Label,
                userName,
                request.AppVersion,
                request.HotelUnitCode,
                nowUtc);

            workstation.MarkCreated(userName, nowUtc);
            dbContext.Workstations.Add(workstation);

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // COURSE REELLE : deux fenetres du meme poste, ou un battement declenche pendant
                // qu'un autre est encore en vol, arrivent tous deux avec le poste absent puis
                // tentent l'insertion. Le perdant recoit une violation de cle primaire. On ne
                // remonte pas cette erreur a l'operateur - un battement rate ne doit jamais faire
                // echouer son travail - on relit et on met simplement a jour la ligne gagnante.
                dbContext.ChangeTracker.Clear();

                var existing = await dbContext.Workstations
                    .FirstOrDefaultAsync(item => item.Id == request.StationId, cancellationToken);

                if (existing is null)
                {
                    return ApplicationResult<WorkstationResponse>.Conflict(
                        "Le poste n'a pas pu etre enregistre. Reessayez.");
                }

                existing.Touch(request.Label, userName, request.AppVersion, request.HotelUnitCode, nowUtc);
                existing.MarkUpdated(userName, nowUtc);
                await dbContext.SaveChangesAsync(cancellationToken);
                workstation = existing;
            }
        }
        else
        {
            workstation.Touch(request.Label, userName, request.AppVersion, request.HotelUnitCode, nowUtc);
            workstation.MarkUpdated(userName, nowUtc);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return ApplicationResult<WorkstationResponse>.Success(Map(workstation, nowUtc));
    }

    public async Task<ApplicationResult<int>> ReportFailuresAsync(
        ReportWorkstationFailuresRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        if (request.StationId == Guid.Empty)
        {
            return ApplicationResult<int>.Validation("L'identifiant du poste est requis.");
        }

        if (request.Items is null || request.Items.Count == 0)
        {
            return ApplicationResult<int>.Success(0);
        }

        if (request.Items.Count > MaxBatchSize)
        {
            // Cette route est ouverte a tout utilisateur authentifie : sans plafond elle
            // deviendrait un moyen d'ecrire des lignes sans limite.
            return ApplicationResult<int>.Validation(
                $"Un lot ne peut pas depasser {MaxBatchSize} entrees.");
        }

        var stationExists = await dbContext.Workstations
            .AnyAsync(item => item.Id == request.StationId, cancellationToken);

        if (!stationExists)
        {
            return ApplicationResult<int>.Validation(
                "Poste inconnu : envoyez d'abord un battement.");
        }

        var nowUtc = DateTime.UtcNow;
        var userName = string.IsNullOrWhiteSpace(context.UserName) ? "inconnu" : context.UserName;

        // Deduplication sur l'identifiant d'evenement genere par le poste : un client incertain
        // (reponse perdue apres ecriture) peut renvoyer son lot sans creer de doublon.
        var incomingIds = request.Items.Select(item => item.EventId).Distinct().ToList();

        var knownIds = await dbContext.WorkstationFailures
            .Where(failure => incomingIds.Contains(failure.Id))
            .Select(failure => failure.Id)
            .ToListAsync(cancellationToken);

        var known = knownIds.ToHashSet();
        var added = 0;

        foreach (var item in request.Items)
        {
            if (item.EventId == Guid.Empty || !known.Add(item.EventId))
            {
                continue;
            }

            // L'assainissement est REFAIT ici, alors que le poste l'a deja applique. Ce n'est pas
            // une redondance inutile : cette route accepte n'importe quel client authentifie, et
            // la base ne doit pas dependre de la bonne conduite de l'appelant pour rester exempte
            // de jetons et de mots de passe. La chaine de requete est retiree de la route pour la
            // meme raison - elle peut porter des identifiants.
            var failure = WorkstationFailure.Record(
                item.EventId,
                request.StationId,
                item.Method,
                FailureMessageSanitizer.StripQuery(item.Path, WorkstationFailure.PathMaxLength),
                item.StatusCode,
                ParseKind(item.Kind),
                FailureMessageSanitizer.Sanitize(item.Message),
                item.ClaimedAtUtc.UtcDateTime,
                nowUtc);

            failure.MarkCreated(userName, nowUtc);
            dbContext.WorkstationFailures.Add(failure);
            added++;
        }

        if (added > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return ApplicationResult<int>.Success(added);
    }

    public async Task<ApplicationResult<WorkstationRegistryResponse>> GetRegistryAsync(
        bool includeAllKnown,
        CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;
        var query = dbContext.Workstations.AsNoTracking();

        if (!includeAllKnown)
        {
            var cutoff = nowUtc.AddDays(-DefaultWindowDays);

            // Comparaison directe possible parce que ce module stocke des DateTime UTC et non des
            // DateTimeOffset : le fournisseur SQLite du harnais de test ne sait traduire ni la
            // comparaison ni l'ORDER BY d'un DateTimeOffset. C'est la raison du choix de type.
            query = query.Where(item => item.LastSeenUtc >= cutoff);
        }

        var workstations = await query
            .OrderByDescending(item => item.LastSeenUtc)
            .ToListAsync(cancellationToken);

        var mapped = workstations.Select(item => Map(item, nowUtc)).ToList();

        // Le nombre de versions DISTINCTES est le chiffre reellement utile de ce module : plus
        // d'une version en service signifie que des clients de builds differents parlent a la
        // meme API, ce qui est un danger d'exploitation, pas un detail cosmetique.
        var distinctVersions = mapped
            .Select(item => item.AppVersion)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        return ApplicationResult<WorkstationRegistryResponse>.Success(new WorkstationRegistryResponse(
            mapped,
            StaleAfterMinutes,
            OfflineAfterMinutes,
            nowUtc,
            distinctVersions));
    }

    public async Task<ApplicationResult<IReadOnlyCollection<WorkstationFailureResponse>>> GetFailuresAsync(
        int maxItems,
        CancellationToken cancellationToken)
    {
        var take = maxItems <= 0 ? MaxFailurePageSize : Math.Min(maxItems, MaxFailurePageSize);

        var rows = await dbContext.WorkstationFailures
            .AsNoTracking()
            .OrderByDescending(failure => failure.RecordedAtUtc)
            .Take(take)
            .Join(
                dbContext.Workstations.AsNoTracking(),
                failure => failure.WorkstationId,
                station => station.Id,
                (failure, station) => new { failure, station.Label })
            .ToListAsync(cancellationToken);

        var mapped = rows
            .Select(row => new WorkstationFailureResponse(
                row.failure.Id,
                row.failure.WorkstationId,
                row.Label,
                row.failure.Method,
                row.failure.Path,
                row.failure.StatusCode,
                row.failure.Kind.ToString(),
                row.failure.Message,
                new DateTimeOffset(row.failure.ClaimedAtUtc, TimeSpan.Zero),
                new DateTimeOffset(row.failure.RecordedAtUtc, TimeSpan.Zero),
                row.failure.ClockDriftSeconds))
            .ToList();

        return ApplicationResult<IReadOnlyCollection<WorkstationFailureResponse>>.Success(mapped);
    }

    /// <summary>
    /// La fraicheur est calculee ICI, cote serveur, et voyage avec la donnee : les ecrans ne
    /// redefinissent aucun seuil, exactement comme BackupStatusResponse renvoie IsOverdue plutot
    /// qu'un age brut. Aucun de ces etats ne veut dire "en ligne" : le serveur n'apprend jamais
    /// qu'un poste a ete eteint, un client lourd n'ayant aucun moyen fiable d'annoncer sa mort.
    /// </summary>
    private static WorkstationResponse Map(Workstation workstation, DateTime nowUtc)
    {
        var minutes = (nowUtc - workstation.LastSeenUtc).TotalMinutes;
        var rounded = minutes <= 0 ? 0 : (int)Math.Min(minutes, int.MaxValue);

        var freshness = rounded >= OfflineAfterMinutes
            ? "Silent"
            : rounded >= StaleAfterMinutes
                ? "Stale"
                : "Recent";

        return new WorkstationResponse(
            workstation.Id,
            workstation.Label,
            workstation.LastUserName,
            workstation.AppVersion,
            workstation.LastHotelUnitCode,
            workstation.CreatedAt,
            new DateTimeOffset(workstation.LastSeenUtc, TimeSpan.Zero),
            rounded,
            freshness);
    }

    // Une nature inconnue n'est pas un motif de refus : le journal doit accepter ce qu'un client
    // d'une version differente lui envoie plutot que de perdre l'information.
    private static WorkstationFailureKind ParseKind(string kind)
    {
        return Enum.TryParse<WorkstationFailureKind>(kind, ignoreCase: true, out var parsed)
            ? parsed
            : WorkstationFailureKind.Unexpected;
    }
}
