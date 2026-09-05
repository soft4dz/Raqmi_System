using Microsoft.Extensions.DependencyInjection;
using RaqmiSystem.Application.Documents;
using RaqmiSystem.Infrastructure.Documents;

// Espace de noms RaqmiSystem.Infrastructure (et non .Documents) a dessein : Program.cs importe deja
// cet espace pour AddRaqmiInfrastructure, et le cablage du module doit y tenir en UNE ligne, sans
// using supplementaire - la contrainte d'integration de ce chantier.
namespace RaqmiSystem.Infrastructure;

/// <summary>
/// Enregistrement de la chaine documentaire (A5). Separe de AddRaqmiInfrastructure pour que ce
/// chantier n'edite pas DependencyInjection.cs, que d'autres lots modifient en parallele.
/// </summary>
public static class DocumentsDependencyInjection
{
    public static IServiceCollection AddRaqmiDocuments(this IServiceCollection services)
    {
        // La licence QuestPDF (Community) est declaree ici, au demarrage, et non au premier rendu :
        // un moteur sans licence echoue a la premiere impression, c'est-a-dire devant un client.
        QuestPdfInvoiceRenderer.ConfigureEngine();

        // Le gabarit est sans etat : un singleton evite de reconstruire la description QuestPDF
        // (polices, styles) a chaque requete.
        services.AddSingleton<IDocumentRenderer<InvoiceDocumentModel>, QuestPdfInvoiceRenderer>();
        services.AddScoped<IDocumentArchive, EfDocumentArchive>();
        services.AddScoped<IDocumentService, DocumentService>();

        return services;
    }
}
