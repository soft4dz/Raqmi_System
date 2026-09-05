using RaqmiSystem.Domain.Documents;

namespace RaqmiSystem.Application.Documents;

/// <summary>
/// L'archive des documents rendus. Deux operations, aucune mise a jour, aucune suppression : une
/// piece legale remise ne s'efface pas et ne se remplace pas.
/// </summary>
public interface IDocumentArchive
{
    /// <summary>Le document archive sous cette reference, ou null s'il n'a jamais ete rendu.</summary>
    Task<RenderedDocument?> FindAsync(DocumentType type, string reference, CancellationToken cancellationToken);

    /// <summary>
    /// Archive le document. PREMIER ECRIT GAGNE : si un rendu concurrent a archive la meme
    /// reference entre-temps, c'est LE SIEN qui est renvoye et le notre est abandonne, pour que
    /// deux appelants simultanes remettent la meme piece octet pour octet. Le retour est donc le
    /// document a servir, pas necessairement celui passe en parametre.
    /// </summary>
    Task<RenderedDocument> StoreAsync(RenderedDocument document, CancellationToken cancellationToken);
}
