using RaqmiSystem.Application.Common;

namespace RaqmiSystem.Application.Channels;

/// <summary>
/// Le contrat d'un connecteur de distribution (Booking.com, Expedia, Airbnb, une autre OTA, ou un
/// channel manager qui les agrege).
///
/// POURQUOI CETTE INTERFACE EXISTE AVANT TOUT CONNECTEUR. Elle pose la frontiere : un fournisseur
/// PUBLIE ce que le PMS a calcule et RAPPORTE ce que le marche a vendu. Il ne calcule jamais de
/// disponibilite lui-meme. C'est la seule facon de tenir le principe fondamental - une source de
/// verite unique pour l'inventaire - car un connecteur qui aurait son propre stock finirait par ne
/// plus etre d'accord avec le PMS, et la difference se paierait en survente.
///
/// Le Domain ne connait pas cette interface et ne doit jamais la connaitre : elle vit ici, dans la
/// couche Application, et les implementations vivent en Infrastructure. Un fournisseur particulier
/// n'apparait nulle part ailleurs que dans sa propre implementation.
/// </summary>
public interface IChannelManagerProvider
{
    /// <summary>Code du fournisseur ("BOOKING", "EXPEDIA", "AIRBNB"), normalise en majuscules.</summary>
    string ProviderCode { get; }

    /// <summary>Nom lisible du fournisseur.</summary>
    string DisplayName { get; }

    /// <summary>Publie les disponibilites calculees par le PMS vers le canal.</summary>
    Task<ApplicationResult<ChannelSyncResult>> PublishAvailabilityAsync(
        ChannelAvailabilityPush push,
        CancellationToken cancellationToken);

    /// <summary>Publie les tarifs resolus par le module Tarifs vers le canal.</summary>
    Task<ApplicationResult<ChannelSyncResult>> PublishRatesAsync(
        ChannelRatePush push,
        CancellationToken cancellationToken);

    /// <summary>Publie les restrictions de vente (stop sell, CTA, CTD, durees) vers le canal.</summary>
    Task<ApplicationResult<ChannelSyncResult>> PublishRestrictionsAsync(
        ChannelRestrictionPush push,
        CancellationToken cancellationToken);

    /// <summary>
    /// Recupere les reservations, modifications et annulations que le canal a enregistrees depuis
    /// <paramref name="since"/>. Le PMS les rejoue ensuite par SON propre chemin de creation, avec
    /// ses propres controles : une reservation OTA ne contourne ni la disponibilite, ni les
    /// allotements, ni la surreservation.
    /// </summary>
    Task<ApplicationResult<IReadOnlyCollection<ChannelReservation>>> FetchReservationsAsync(
        string hotelUnitCode,
        DateTimeOffset since,
        CancellationToken cancellationToken);

    /// <summary>Confirme au canal qu'une reservation a bien ete integree au PMS.</summary>
    Task<ApplicationResult<ChannelSyncResult>> AcknowledgeAsync(
        string hotelUnitCode,
        string externalReservationId,
        CancellationToken cancellationToken);
}
