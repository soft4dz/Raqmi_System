using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Documents;
using RaqmiSystem.Domain.Documents;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Infrastructure.Documents;

/// <summary>
/// L'archive des documents rendus, dans la table documents.rendered_documents du meme contexte que
/// le reste de l'ERP. Lecture sans suivi (les octets ne se modifient jamais) ; ecriture protegee
/// par l'index unique (type, reference).
/// </summary>
public sealed class EfDocumentArchive(RaqmiDbContext dbContext) : IDocumentArchive
{
    public Task<RenderedDocument?> FindAsync(DocumentType type, string reference, CancellationToken cancellationToken)
    {
        var normalizedReference = RenderedDocument.NormalizeReference(reference);

        return dbContext.Set<RenderedDocument>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                document => document.Type == type && document.Reference == normalizedReference,
                cancellationToken);
    }

    public async Task<RenderedDocument> StoreAsync(RenderedDocument document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        dbContext.Set<RenderedDocument>().Add(document);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return document;
        }
        catch (DbUpdateException exception)
            when (exception.IsUniqueViolation(RenderedDocumentConfiguration.UniqueTypeReferenceIndexName))
        {
            // Premier ecrit gagne. Un rendu concurrent a archive la meme reference entre notre
            // lecture et notre ecriture : on abandonne le notre et on sert le sien, pour que les
            // deux demandeurs recoivent la meme piece octet pour octet. L'entite en echec est
            // detachee, sinon le contexte tenterait de la reinserer au prochain SaveChanges (celui
            // de l'ecriture d'audit, par exemple).
            dbContext.Entry(document).State = EntityState.Detached;

            var winner = await FindAsync(document.Type, document.Reference, cancellationToken);

            if (winner is null)
            {
                throw;
            }

            return winner;
        }
    }
}
