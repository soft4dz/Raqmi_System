using RaqmiSystem.Domain.Common;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Domain.Lodging;

/// <summary>Etat d'un passage de night audit.</summary>
public enum NightAuditStatus
{
    /// <summary>Controles passes, rien n'a encore ete ecrit. C'est le mode "repetition".</summary>
    Inspected = 0,

    /// <summary>Passage execute : les nuitees ont ete posees et la journee peut etre cloturee.</summary>
    Completed = 1,

    /// <summary>Passage refuse : au moins un controle bloquant a echoue et rien n'a ete ecrit.</summary>
    Blocked = 2
}

/// <summary>
/// Un passage de night audit pour une unite et une journee d'exploitation.
///
/// CE QUE FAIT LE NIGHT AUDIT, ET DANS QUEL ORDRE : il controle (arrivees non traitees, departs
/// non cloturees, chambres incoherentes, folios ouverts), pose les nuitees et les prestations
/// automatiques de la journee, puis rend un rapport. La cloture comptable elle-meme reste au
/// module Cloture : deux modules qui cloturent la meme journee finiraient par ne plus etre
/// d'accord sur son etat.
///
/// L'IDEMPOTENCE EST L'EXIGENCE CENTRALE. Relancer le night audit d'une journee ne doit JAMAIS
/// doubler une ecriture. Elle est obtenue en deux endroits : une ligne unique par (unite, journee)
/// pour le passage lui-meme, et une reference de geste unique par folio pour chaque nuitee posee
/// (<c>FolioCharge.SourceReference</c>). Le second mecanisme est le vrai garde-fou - le premier ne
/// protegerait pas d'un passage relance apres une panne au milieu de l'ecriture.
/// </summary>
public sealed class NightAuditRun : AuditableEntity
{
    public const int ReportMaxLength = 8000;

    private NightAuditRun()
    {
    }

    public NightAuditRun(string hotelUnitCode, DateOnly businessDate, string userName, DateTimeOffset utcNow)
    {
        HotelUnitCode = HotelUnit.NormalizeCode(hotelUnitCode);
        BusinessDate = businessDate;
        StartedAt = utcNow;
        StartedBy = LodgingText.Actor(userName);
        Status = NightAuditStatus.Inspected;
    }

    public string HotelUnitCode { get; private set; } = string.Empty;

    /// <summary>Journee d'exploitation auditee.</summary>
    public DateOnly BusinessDate { get; private set; }

    public NightAuditStatus Status { get; private set; } = NightAuditStatus.Inspected;

    public DateTimeOffset StartedAt { get; private set; }

    public string StartedBy { get; private set; } = "system";

    public DateTimeOffset? CompletedAt { get; private set; }

    public string? CompletedBy { get; private set; }

    // ------------------------------- Ce que le passage a fait -------------------------------

    /// <summary>Nombre de nuitees posees par ce passage.</summary>
    public int PostedRoomNights { get; private set; }

    /// <summary>Nombre de lignes de prestations automatiques posees (pension, taxes).</summary>
    public int PostedExtras { get; private set; }

    /// <summary>Montant total pose par ce passage.</summary>
    public decimal PostedAmount { get; private set; }

    /// <summary>Nombre de dossiers passes en no-show par ce passage.</summary>
    public int NoShowsRecorded { get; private set; }

    /// <summary>
    /// Nombre de lignes qui existaient DEJA et qui n'ont donc pas ete reposees. Un chiffre non nul
    /// sur un second passage est le signe que l'idempotence a joue : c'est une information utile,
    /// pas une anomalie.
    /// </summary>
    public int SkippedAlreadyPosted { get; private set; }

    // ---------------------------------- Ce qu'il a trouve ----------------------------------

    /// <summary>Arrivees du jour non enregistrees au moment du passage.</summary>
    public int PendingArrivals { get; private set; }

    /// <summary>Departs du jour non enregistres au moment du passage.</summary>
    public int PendingDepartures { get; private set; }

    /// <summary>Folios encore ouverts sur des sejours termines.</summary>
    public int OpenFolios { get; private set; }

    /// <summary>Chambres occupees dont l'etat menage est incoherent avec l'occupation.</summary>
    public int RoomStateMismatches { get; private set; }

    /// <summary>Rapport lisible du passage, ligne a ligne.</summary>
    public string? Report { get; private set; }

    /// <summary>Enregistre les constats de controle du passage.</summary>
    public void RecordChecks(
        int pendingArrivals,
        int pendingDepartures,
        int openFolios,
        int roomStateMismatches)
    {
        PendingArrivals = Math.Max(0, pendingArrivals);
        PendingDepartures = Math.Max(0, pendingDepartures);
        OpenFolios = Math.Max(0, openFolios);
        RoomStateMismatches = Math.Max(0, roomStateMismatches);
    }

    /// <summary>Enregistre ce que le passage a reellement ecrit.</summary>
    public void RecordPostings(
        int postedRoomNights,
        int postedExtras,
        decimal postedAmount,
        int noShowsRecorded,
        int skippedAlreadyPosted)
    {
        PostedRoomNights = Math.Max(0, postedRoomNights);
        PostedExtras = Math.Max(0, postedExtras);
        PostedAmount = LodgingText.Money(postedAmount, nameof(postedAmount));
        NoShowsRecorded = Math.Max(0, noShowsRecorded);
        SkippedAlreadyPosted = Math.Max(0, skippedAlreadyPosted);
    }

    public void SetReport(string? report)
    {
        Report = LodgingText.Optional(report, nameof(report), ReportMaxLength);
    }

    /// <summary>Cloture le passage comme execute.</summary>
    public void Complete(string userName, DateTimeOffset utcNow)
    {
        Status = NightAuditStatus.Completed;
        CompletedAt = utcNow;
        CompletedBy = LodgingText.Actor(userName);
    }

    /// <summary>Cloture le passage comme refuse : rien n'a ete ecrit.</summary>
    public void Block(string userName, DateTimeOffset utcNow)
    {
        Status = NightAuditStatus.Blocked;
        CompletedAt = utcNow;
        CompletedBy = LodgingText.Actor(userName);
    }
}
