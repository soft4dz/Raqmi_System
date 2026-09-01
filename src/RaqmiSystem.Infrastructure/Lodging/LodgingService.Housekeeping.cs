using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Housekeeping;
using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Infrastructure.Lodging;

/// <summary>
/// Le pont PMS -> housekeeping : les evenements du sejour qui changent l'etat d'une chambre.
///
/// QUI POSSEDE QUOI. Le module Housekeeping possede le WORKFLOW - qui nettoie, quand, avec quelle
/// inspection. Le PMS possede les EVENEMENTS qui declenchent un changement d'etat : un depart rend
/// la chambre sale, un changement de chambre rend l'ancienne sale, une mise hors service la retire
/// du plan de nettoyage, une remise en service la ramene en sale et non en propre. Sans ce pont,
/// la gouvernante decouvrirait les departs en faisant le tour des couloirs.
///
/// L'ETAT MENAGE N'EST PAS LA SOURCE DE VERITE DE L'INVENTAIRE. Une chambre marquee hors service
/// ici n'est qu'un AFFICHAGE pour la gouvernante ; ce qui retire reellement la chambre de la vente
/// est le <see cref="RoomBlock"/>, avec ses dates. Les deux sont tenus en phase par ce pont, et
/// c'est le blocage qui fait foi le jour ou ils divergeraient.
/// </summary>
public sealed partial class LodgingService
{
    /// <summary>La chambre vient d'etre liberee : elle est a nettoyer.</summary>
    private Task MarkRoomDirtyAsync(
        string hotelUnitCode,
        Guid? roomId,
        OperationContext context,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        return ApplyRoomConditionAsync(
            hotelUnitCode,
            roomId,
            RoomConditionStatus.Dirty,
            context,
            now,
            outOfOrderReason: null,
            outOfOrderUntil: null,
            cancellationToken);
    }

    /// <summary>Une chambre part en travaux : la gouvernante cesse d'y planifier des taches.</summary>
    private Task MarkRoomOutOfOrderAsync(
        RoomBlock block,
        Room room,
        OperationContext context,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        return ApplyRoomConditionAsync(
            block.HotelUnitCode,
            room.Id,
            RoomConditionStatus.OutOfOrder,
            context,
            now,
            block.Reason,
            block.EndDate,
            cancellationToken);
    }

    /// <summary>
    /// Applique un etat menage a une chambre, en creant la ligne si elle n'existe pas encore : une
    /// chambre dont personne n'a jamais rien declare n'a pas de ligne, et c'est normal.
    /// </summary>
    private async Task ApplyRoomConditionAsync(
        string hotelUnitCode,
        Guid? roomId,
        RoomConditionStatus status,
        OperationContext context,
        DateTimeOffset now,
        string? outOfOrderReason,
        DateOnly? outOfOrderUntil,
        CancellationToken cancellationToken)
    {
        if (roomId is not { } id)
        {
            return;
        }

        var condition = await dbContext.Set<RoomCondition>()
            .SingleOrDefaultAsync(current => current.RoomId == id, cancellationToken);

        if (condition is null)
        {
            condition = new RoomCondition(hotelUnitCode, id);
            condition.MarkCreated(context.UserName, now);
            dbContext.Set<RoomCondition>().Add(condition);
        }
        else
        {
            condition.MarkUpdated(context.UserName, now);
        }

        condition.Apply(status, context.UserName, now, outOfOrderReason, outOfOrderUntil);
    }

    /// <summary>
    /// L'etat menage des chambres d'une unite, pour les ecrans qui doivent dire si une chambre est
    /// PRETE. Une chambre sans ligne est presumee propre - une chambre neuve est vendable tant que
    /// personne n'a dit le contraire.
    /// </summary>
    private async Task<Dictionary<Guid, RoomConditionStatus>> LoadRoomConditionsAsync(
        string hotelUnitCode,
        CancellationToken cancellationToken)
    {
        return await dbContext.Set<RoomCondition>()
            .AsNoTracking()
            .Where(condition => condition.HotelUnitCode == hotelUnitCode)
            .ToDictionaryAsync(condition => condition.RoomId, condition => condition.Status, cancellationToken);
    }
}
