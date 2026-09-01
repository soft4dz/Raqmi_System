namespace RaqmiSystem.Application.Channels;

/// <summary>
/// L'annuaire des connecteurs disponibles. Le PMS s'adresse a lui, jamais a un fournisseur nomme :
/// c'est ce qui empeche "Booking.com" d'apparaitre dans le code metier, et ce qui permet d'ajouter
/// une OTA sans toucher au moteur de reservation.
/// </summary>
public interface IChannelManagerRegistry
{
    /// <summary>Les connecteurs enregistres.</summary>
    IReadOnlyCollection<IChannelManagerProvider> Providers { get; }

    /// <summary>Le connecteur portant ce code, ou null quand aucun n'est enregistre.</summary>
    IChannelManagerProvider? Find(string providerCode);
}
