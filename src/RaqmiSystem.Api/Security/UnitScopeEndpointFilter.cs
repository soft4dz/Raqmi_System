using System.Collections.Concurrent;
using System.Reflection;
using RaqmiSystem.Api.Endpoints;

namespace RaqmiSystem.Api.Security;

/// <summary>
/// Autorite du perimetre utilisateur <-> unite cote API (lot 2.2). Pose sur le groupe
/// <c>/api/v1</c>, il refuse en 403 toute requete authentifiee qui designe une unite hors du
/// perimetre porte par le jeton (<see cref="SecurityContextExtensions.GetUnitScope"/>).
///
/// Ce qu'il lit, dans cet ordre : la valeur de route <c>hotelUnitCode</c>, le parametre de
/// requete <c>hotelUnitCode</c> (les deux sans casse) et, dans chaque argument lie a la route,
/// une propriete publique <c>HotelUnitCode</c> de type chaine - la forme que prennent tous les
/// corps de creation (recette, reservation, type de chambre, evenement...). Chaque code
/// trouve doit etre autorise ; il suffit d'un refus.
///
/// Ce qu'il ne couvre PAS, et qui est documente dans docs/security.md : les listes sans
/// parametre d'unite (elles rendent aujourd'hui toutes les unites), et les routes qui
/// n'identifient l'unite qu'apres chargement (par identifiant de reservation, de facture, de
/// commande...). Ces deux limites relevent du controle dans les services proprietaires, a la
/// phase suivante - le filtre est la premiere ligne, pas la derniere.
///
/// Il s'execute APRES l'autorisation : une politique de permission qui refuse repond 403 avant
/// lui, et une requete non authentifiee ne l'atteint jamais sur une route protegee. Les
/// routes de session, de socle et d'administration sont exemptees explicitement : ce filtre
/// ne doit jamais empecher de se connecter, de lire son propre contexte, d'administrer les
/// comptes ou les unites elles-memes.
/// </summary>
public sealed class UnitScopeEndpointFilter : IEndpointFilter
{
    private const string UnitParameterName = "hotelUnitCode";

    private static readonly PathString[] ExemptPrefixes =
    [
        new("/api/v1/auth"),
        new("/api/v1/me"),
        new("/api/v1/health"),
        new("/api/v1/security"),
        new("/api/v1/organization"),
        new("/api/v1/settings")
    ];

    // La reflexion sur le type d'un argument est faite une fois par type, jamais par requete.
    private static readonly ConcurrentDictionary<Type, PropertyInfo?> UnitCodeProperties = new();

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;

        if (httpContext.User.Identity?.IsAuthenticated != true || IsExempt(httpContext.Request.Path))
        {
            return await next(context);
        }

        var scope = httpContext.User.GetUnitScope();

        if (scope.IsGlobal)
        {
            return await next(context);
        }

        foreach (var code in FindUnitCodes(context))
        {
            if (!scope.Allows(code))
            {
                var allowed = scope.AllowedUnitCodes.Count == 0
                    ? "aucune"
                    : string.Join(", ", scope.AllowedUnitCodes);

                return Results.Json(
                    new ErrorResponse(
                        $"L'unite hoteliere '{code.Trim()}' n'est pas dans votre perimetre (unites autorisees : {allowed}). " +
                        "Demandez a un administrateur d'ajuster vos affectations."),
                    statusCode: StatusCodes.Status403Forbidden);
            }
        }

        return await next(context);
    }

    private static bool IsExempt(PathString path)
    {
        return ExemptPrefixes.Any(prefix => path.StartsWithSegments(prefix));
    }

    private static IEnumerable<string> FindUnitCodes(EndpointFilterInvocationContext context)
    {
        var request = context.HttpContext.Request;

        if (request.RouteValues.TryGetValue(UnitParameterName, out var routeValue)
            && routeValue is string routeCode
            && !string.IsNullOrWhiteSpace(routeCode))
        {
            yield return routeCode;
        }

        if (request.Query.TryGetValue(UnitParameterName, out var queryValues))
        {
            foreach (var value in queryValues)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    yield return value!;
                }
            }
        }

        foreach (var argument in context.Arguments)
        {
            if (argument is null)
            {
                continue;
            }

            var property = UnitCodeProperties.GetOrAdd(argument.GetType(), FindUnitCodeProperty);

            if (property?.GetValue(argument) is string bodyCode && !string.IsNullOrWhiteSpace(bodyCode))
            {
                yield return bodyCode;
            }
        }
    }

    /// <summary>
    /// La propriete <c>HotelUnitCode</c> (chaine, publique, sans casse) d'un objet de requete.
    /// Les types du framework et les services injectes (dont le type concret vit dans
    /// Infrastructure) ne sont pas des objets de requete : ils sont ecartes sans reflexion.
    /// </summary>
    private static PropertyInfo? FindUnitCodeProperty(Type type)
    {
        if (type.IsPrimitive
            || type == typeof(string)
            || type.Namespace is null
            || type.Namespace.StartsWith("System", StringComparison.Ordinal)
            || type.Namespace.StartsWith("Microsoft", StringComparison.Ordinal)
            || type.Namespace.StartsWith("RaqmiSystem.Infrastructure", StringComparison.Ordinal))
        {
            return null;
        }

        var property = type.GetProperty(
            "HotelUnitCode",
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

        return property is not null
            && property.PropertyType == typeof(string)
            && property.GetIndexParameters().Length == 0
            ? property
            : null;
    }
}
