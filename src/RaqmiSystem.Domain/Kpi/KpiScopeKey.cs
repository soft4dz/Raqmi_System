using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Domain.Kpi;

/// <summary>
/// La representation NON NULLE d'un perimetre de mesure : un code d'unite hoteliere, ou le
/// marqueur du groupe.
///
/// POURQUOI CE DETOUR. Le perimetre naturel est "une unite, ou aucune" - donc un code nullable.
/// Mais l'unicite d'un instantane (un indicateur, un perimetre, une periode) et celle d'une
/// regle de seuils (un indicateur, un perimetre) doivent etre des CONTRAINTES DE BASE, sans
/// quoi deux ecritures concurrentes poseraient deux valeurs groupe pour la meme periode et
/// personne ne saurait laquelle fait foi. Or PostgreSQL comme SQLite considerent deux NULL
/// comme distincts dans un index unique : un index portant directement le code nullable ne
/// protegerait que les lignes d'unite, jamais celles du groupe. La cle de perimetre porte donc
/// la contrainte, et le code d'unite nullable reste ce que lisent l'API et la cle etrangere.
///
/// LE MARQUEUR EST EN MINUSCULES, ce qui n'est pas cosmetique :
/// <see cref="HotelUnit.NormalizeCode"/> met tout code en majuscules, donc aucun code d'unite
/// reel ne pourra jamais valoir "(groupe)" - la collision est impossible par construction, et
/// non simplement improbable.
/// </summary>
public static class KpiScopeKey
{
    public const string Group = "(groupe)";

    public static string For(string? hotelUnitCode)
    {
        return string.IsNullOrWhiteSpace(hotelUnitCode)
            ? Group
            : HotelUnit.NormalizeCode(hotelUnitCode);
    }
}
