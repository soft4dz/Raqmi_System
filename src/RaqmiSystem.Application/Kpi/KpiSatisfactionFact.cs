namespace RaqmiSystem.Application.Kpi;

/// <summary>
/// Une reponse a une enquete de satisfaction, note brute de 0 a 10. Le classement en promoteur,
/// passif ou detracteur n'est PAS refait ici : il appartient au module CRM, qui porte les bornes
/// de la methode NPS, et le moteur KPI l'appelle plutot que de les redefinir - deux definitions
/// du NPS dans un meme produit finiraient toujours par diverger.
/// </summary>
public sealed record KpiSatisfactionFact(string HotelUnitCode, DateOnly SurveyDate, int Score);
