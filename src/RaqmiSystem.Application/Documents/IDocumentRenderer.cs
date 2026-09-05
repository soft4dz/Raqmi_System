namespace RaqmiSystem.Application.Documents;

/// <summary>
/// Port de rendu : un modele de document en entree, un PDF en sortie. L'implementation (QuestPDF,
/// dans Infrastructure) est la seule a connaitre le moteur ; Application et Desktop n'en dependent
/// jamais. Une implementation par gabarit, typee par son modele.
/// </summary>
/// <typeparam name="TModel">Le modele que le gabarit sait imprimer.</typeparam>
public interface IDocumentRenderer<in TModel>
    where TModel : class
{
    /// <summary>
    /// Version du gabarit, archivee avec chaque document rendu. A incrementer a chaque changement
    /// de mise en page : elle dit avec quel gabarit une piece ancienne a ete remise.
    /// </summary>
    int TemplateVersion { get; }

    /// <summary>Rend le modele en PDF (A4, francais). Synchrone : c'est du calcul, pas de l'E/S.</summary>
    byte[] Render(TModel model);
}
