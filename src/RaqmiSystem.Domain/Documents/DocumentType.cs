namespace RaqmiSystem.Domain.Documents;

/// <summary>
/// Nature d'un document rendu. Une seule valeur aujourd'hui (A5 version minimale : le gabarit de
/// la facture) ; la note de sejour, le recu, le bon de commande et le bulletin de paie s'ajouteront
/// ici, chacun avec sa reference metier propre.
/// </summary>
public enum DocumentType
{
    Invoice
}
