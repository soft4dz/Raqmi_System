using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Common;
using RaqmiSystem.Domain.Organization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RaqmiSystem.Domain.Lodging;

/// <summary>
/// Un sejour, vendu sur un TYPE de chambre pour la periode [<see cref="ArrivalDate"/>,
/// <see cref="DepartureDate"/>). Les dates suivent la convention hoteliere : la nuit du jour de
/// depart ne fait PAS partie du sejour, de sorte qu'un depart et une arrivee le meme jour se
/// partagent la chambre sans conflit.
///
/// LA RESERVATION PORTE UN TYPE, PAS FORCEMENT UNE CHAMBRE. <see cref="RoomId"/> est nullable, et
/// c'est le fait central de ce modele : un client reserve "une double standard", pas la 214. La
/// chambre physique est affectee quand l'hotel le decide - a la prise, la veille, ou au comptoir.
/// Exiger un numero des la vente obligerait la reception a bloquer des chambres precises des mois
/// a l'avance, ce qui interdit ensuite tout regroupement de groupe et toute optimisation du plan.
///
/// PRIX FIGE A LA VENTE. Le tarif de chaque nuit est resolu par le module Tarifs AU MOMENT DE LA
/// VENTE puis fige dans <see cref="NightlyRateSnapshot"/> / <see cref="NightlyRatesSnapshotJson"/>,
/// meme discipline que l'identite de l'emetteur figee dans les factures emises : une evolution de
/// tarif ne doit jamais reecrire le prix auquel une reservation a ete prise. Il n'est reecrit que
/// par un geste explicite et trace (prolongation, changement de type, revision de tarif).
///
/// INVARIANT CENTRAL (anti-double-reservation) : deux reservations de la MEME CHAMBRE dont le
/// statut tient l'inventaire (<see cref="IsBlocking"/>) ne peuvent jamais se chevaucher
/// (chevauchement = arrivee &lt; autre.depart ET depart &gt; autre.arrivee). L'entite porte le
/// vocabulaire (<see cref="IsBlocking"/>, <see cref="PeriodsOverlap"/>) ; la garantie elle-meme
/// est tenue par le service dans une transaction Serializable, parce qu'aucun invariant portant
/// sur une seule ligne ne peut voir les autres reservations.
/// </summary>
public sealed class Reservation : AuditableEntity
{
    public const int NumberMaxLength = 24;
    public const int NotesMaxLength = 2000;
    public const int SpecialRequestsMaxLength = 2000;
    public const int GuaranteeReferenceMaxLength = 120;
    public const int CodeMaxLength = 40;

    /// <summary>Au-dela, ce n'est plus une chambre : la saisie est refusee.</summary>
    public const int MaxOccupants = 30;

    private Reservation()
    {
    }

    /// <summary>
    /// Cree un sejour vendu sur un type. <paramref name="roomId"/> est facultatif : une vente par
    /// type sans chambre affectee est un cas normal, pas une reservation incomplete.
    ///
    /// LE NUMERO DE DOSSIER EST OBLIGATOIRE ICI, et ce n'est pas une formalite : il est unique par
    /// unite, il est ce que le client cite au telephone, et une ligne sans numero ferait entrer en
    /// collision toutes les autres lignes sans numero sur l'index unique. L'exiger au constructeur
    /// rend l'oubli impossible plutot que rare.
    /// </summary>
    public Reservation(
        string hotelUnitCode,
        string number,
        string roomTypeCode,
        Guid? roomId,
        string customerCode,
        DateOnly arrivalDate,
        DateOnly departureDate,
        int adults,
        decimal nightlyRateSnapshot,
        string ratePlanCodeSnapshot,
        int children = 0,
        int infants = 0)
    {
        if (roomId is { } assigned && assigned == Guid.Empty)
        {
            throw new ArgumentException(
                "L'identifiant de la chambre doit etre valide ou absent.",
                nameof(roomId));
        }

        if (departureDate <= arrivalDate)
        {
            throw new ArgumentException(
                "La date de depart doit etre posterieure a la date d'arrivee (un sejour couvre au moins une nuit).",
                nameof(departureDate));
        }

        HotelUnitCode = HotelUnit.NormalizeCode(hotelUnitCode);
        Number = LodgingText.RequireCode(number, nameof(number), NumberMaxLength);
        RoomTypeCode = RoomType.NormalizeCode(roomTypeCode);
        OriginalRoomTypeCode = RoomTypeCode;
        RoomId = roomId;
        CustomerCode = Customer.NormalizeCode(customerCode);
        ArrivalDate = arrivalDate;
        DepartureDate = departureDate;
        NightlyRateSnapshot = LodgingText.Money(nightlyRateSnapshot, nameof(nightlyRateSnapshot));
        RatePlanCodeSnapshot = LodgingText.Require(ratePlanCodeSnapshot, nameof(ratePlanCodeSnapshot), 60);
        Status = ReservationStatus.Confirmed;

        ApplyGuestMix(adults, children, infants);
    }

    // ------------------------------------- Identification -------------------------------------

    public string HotelUnitCode { get; private set; } = string.Empty;

    /// <summary>
    /// Numero de dossier, unique dans l'unite. Attribue par le service a la creation ; il est ce
    /// que le client cite au telephone, et c'est pourquoi il ne bouge jamais - meme apres un
    /// changement de chambre, de type ou de dates.
    /// </summary>
    public string Number { get; private set; } = string.Empty;

    /// <summary>
    /// Type de chambre VENDU aujourd'hui. Un surclassement le remplace ; le type vendu a
    /// l'origine reste dans <see cref="OriginalRoomTypeCode"/>.
    /// </summary>
    public string RoomTypeCode { get; private set; } = string.Empty;

    /// <summary>
    /// Type vendu a la creation, jamais modifie. Sans lui, un surclassement gratuit consenti au
    /// comptoir effacerait la trace de ce que le client avait reellement achete, et le controle de
    /// gestion ne verrait plus la difference entre une suite vendue et une suite offerte.
    /// </summary>
    public string OriginalRoomTypeCode { get; private set; } = string.Empty;

    /// <summary>Chambre physique affectee. Null tant que l'affectation n'a pas eu lieu.</summary>
    public Guid? RoomId { get; private set; }

    public string CustomerCode { get; private set; } = string.Empty;

    /// <summary>Vrai quand une chambre physique est affectee au sejour.</summary>
    public bool HasRoom => RoomId is not null;

    // --------------------------------------- Periode ---------------------------------------

    public DateOnly ArrivalDate { get; private set; }

    public DateOnly DepartureDate { get; private set; }

    /// <summary>Heure d'arrivee annoncee par le client. Sert au tri des arrivees et a l'ECI.</summary>
    public TimeOnly? EstimatedArrivalTime { get; private set; }

    /// <summary>Heure de depart annoncee. Sert au tri des departs et au depart tardif.</summary>
    public TimeOnly? EstimatedDepartureTime { get; private set; }

    /// <summary>Nombre de nuits du sejour (la nuit du depart n'en fait pas partie).</summary>
    public int Nights => DepartureDate.DayNumber - ArrivalDate.DayNumber;

    // -------------------------------------- Occupants --------------------------------------

    public int Adults { get; private set; }

    public int Children { get; private set; }

    /// <summary>Bebes en berceau. Comptes a part : un berceau n'est pas un couchage.</summary>
    public int Infants { get; private set; }

    /// <summary>
    /// Adultes + enfants : le nombre d'occupants que la capacite du type doit pouvoir coucher.
    /// Colonne conservee et maintenue en coherence avec le detail, parce que toute la lecture
    /// historique du produit (occupation, tableaux de bord, exports) s'appuie dessus.
    /// </summary>
    public int GuestCount { get; private set; }

    // ------------------------------------- Commercial -------------------------------------

    /// <summary>Segment de marche (LOISIR, AFFAIRES, GROUPE, EQUIPAGE...). Code libre normalise.</summary>
    public string? MarketSegmentCode { get; private set; }

    /// <summary>
    /// Canal de distribution (DIRECT, WEB, BOOKING, EXPEDIA, TELEPHONE...). C'est ce code que les
    /// restrictions ciblent et que le channel manager renseigne pour ses propres reservations.
    /// </summary>
    public string? ChannelCode { get; private set; }

    /// <summary>Source commerciale plus fine que le canal (campagne, partenaire, referent).</summary>
    public string? SourceCode { get; private set; }

    /// <summary>Societe cliente, quand le sejour releve d'un compte entreprise.</summary>
    public string? CompanyCode { get; private set; }

    /// <summary>Agence de voyage, quand elle intervient.</summary>
    public string? AgencyCode { get; private set; }

    /// <summary>Convention commerciale appliquee, figee a la vente pour la tracabilite du prix.</summary>
    public string? ConventionCode { get; private set; }

    /// <summary>
    /// Vrai quand la reservation est nee au comptoir avec un client deja present (walk-in). La
    /// distinction n'est pas decorative : un walk-in ne se compte pas dans les previsions, ne
    /// declenche pas de relance de pre-arrivee et raconte une autre histoire commerciale.
    /// </summary>
    public bool IsWalkIn { get; private set; }

    /// <summary>
    /// Vrai quand cette vente a franchi la capacite PHYSIQUE du type sur au moins une nuit. La
    /// reception doit pouvoir lister ces dossiers avant le jour J pour organiser le relogement.
    /// </summary>
    public bool IsOverbooking { get; private set; }

    public string? Notes { get; private set; }

    public string? SpecialRequests { get; private set; }

    // -------------------------------------- Groupes --------------------------------------

    /// <summary>
    /// Bloc de groupe sur lequel cette reservation a ete prise, quand elle vient d'un allotement.
    /// Null pour une reservation publique.
    ///
    /// Ce rattachement n'est pas decoratif : il dit si la nuitee CONSOMME le bloc ou si elle mange
    /// l'inventaire public. Sans lui, une chambre prise sur le bloc serait comptee deux fois -
    /// une fois comme tenue, une fois comme vendue - et l'hotel s'interdirait de vendre des
    /// chambres pourtant libres.
    /// </summary>
    public Guid? AllotmentId { get; private set; }

    /// <summary>
    /// Nom de l'occupant, tel qu'il figure sur la rooming list du groupe. Null tant que le groupe
    /// n'a pas transmis ses noms, ce qui est l'etat normal d'un bloc pose des mois a l'avance.
    /// </summary>
    public string? GuestName { get; private set; }

    // -------------------------------------- Garantie --------------------------------------

    public GuaranteeKind Guarantee { get; private set; } = GuaranteeKind.None;

    /// <summary>Empreinte carte, numero de voucher, reference de prise en charge.</summary>
    public string? GuaranteeReference { get; private set; }

    /// <summary>
    /// Politique d'annulation FIGEE a la confirmation, en JSON. Une evolution ulterieure de la
    /// politique ne doit jamais changer les conditions d'un dossier deja confirme : le client a
    /// accepte celles du jour de sa reservation, pas celles d'aujourd'hui.
    /// </summary>
    public string? CancellationPolicySnapshotJson { get; private set; }

    /// <summary>Code de la politique appliquee, pour la lecture au comptoir.</summary>
    public string? CancellationPolicyCode { get; private set; }

    /// <summary>Penalite retenue a l'annulation ou au no-show, calculee depuis la politique figee.</summary>
    public decimal CancellationFeeAmount { get; private set; }

    // --------------------------------------- Statut ---------------------------------------

    public ReservationStatus Status { get; private set; } = ReservationStatus.Confirmed;

    public string? CancelReason { get; private set; }

    public DateTimeOffset? CheckedInAt { get; private set; }

    public string? CheckedInBy { get; private set; }

    public DateTimeOffset? CheckedOutAt { get; private set; }

    public string? CheckedOutBy { get; private set; }

    public DateTimeOffset? CancelledAt { get; private set; }

    public string? CancelledBy { get; private set; }

    public DateTimeOffset? NoShowAt { get; private set; }

    public string? NoShowBy { get; private set; }

    /// <summary>
    /// Vrai quand cette reservation tient sa chambre sur sa periode. Delegue a
    /// <see cref="ReservationStatuses.Blocks"/> : une seule definition d'"occupe" pour tout le
    /// produit. Un sejour termine tient toujours - ces nuits ont bien ete consommees.
    /// </summary>
    public bool IsBlocking => Status.Blocks();

    // ---------------------------------------- Prix ----------------------------------------

    /// <summary>
    /// Prix de la nuit d'ARRIVEE, fige a la vente. Quand le sejour traverse plusieurs periodes
    /// tarifaires, le detail nuit par nuit fait foi (<see cref="NightlyRatesSnapshotJson"/>) ;
    /// cette valeur plate reste le tarif d'arrivee, pour l'affichage et pour les lignes anterieures
    /// a l'existence du detail.
    /// </summary>
    public decimal NightlyRateSnapshot { get; private set; }

    /// <summary>Plan tarifaire d'ou vient le prix fige de la nuit d'arrivee.</summary>
    public string RatePlanCodeSnapshot { get; private set; } = string.Empty;

    /// <summary>
    /// Tableau JSON des tarifs figes nuit par nuit ([{"night","amount","ratePlanCode"}], une
    /// entree par nuit, ordonnees). Null sur les lignes creees avant l'existence de ce detail :
    /// ces sejours facturaient - et facturent toujours - <see cref="NightlyRateSnapshot"/> a plat,
    /// ce sur quoi <see cref="GetNightlyRates"/> retombe.
    /// </summary>
    public string? NightlyRatesSnapshotJson { get; private set; }

    /// <summary>Total du sejour : somme des tarifs figes nuit par nuit.</summary>
    public decimal TotalStayAmount => GetNightlyRates().Sum(rate => rate.Amount);

    // ------------------------------------ Regles de date ------------------------------------

    /// <summary>
    /// La regle de chevauchement de l'invariant central, en un seul endroit : deux periodes
    /// [arrivee, depart) se chevauchent quand chacune commence avant que l'autre ne finisse.
    /// Demi-ouverte sur le jour de depart, de sorte qu'un depart et une arrivee le meme jour ne
    /// sont PAS un chevauchement.
    /// </summary>
    public static bool PeriodsOverlap(
        DateOnly firstArrival,
        DateOnly firstDeparture,
        DateOnly secondArrival,
        DateOnly secondDeparture)
    {
        return firstArrival < secondDeparture && firstDeparture > secondArrival;
    }

    /// <summary>Vrai quand le client dort dans la chambre la nuit donnee.</summary>
    public bool CoversNight(DateOnly night)
    {
        return ArrivalDate <= night && night < DepartureDate;
    }

    // ---------------------------------------- Gestes ----------------------------------------

    /// <summary>Rattache la reservation a un bloc de groupe, a la creation.</summary>
    public void AttachToAllotment(Guid allotmentId)
    {
        if (allotmentId == Guid.Empty)
        {
            throw new ArgumentException("L'identifiant de l'allotement est requis.", nameof(allotmentId));
        }

        AllotmentId = allotmentId;
    }

    /// <summary>Renseigne ou efface le nom de l'occupant (rooming list).</summary>
    public void SetGuestName(string? guestName)
    {
        if (string.IsNullOrWhiteSpace(guestName))
        {
            GuestName = null;
            return;
        }

        var trimmed = guestName.Trim();

        GuestName = trimmed.Length <= 160 ? trimmed : trimmed[..160];
    }

    /// <summary>Renseigne les heures annoncees d'arrivee et de depart.</summary>
    public void SetSchedule(TimeOnly? estimatedArrivalTime, TimeOnly? estimatedDepartureTime)
    {
        EstimatedArrivalTime = estimatedArrivalTime;
        EstimatedDepartureTime = estimatedDepartureTime;
    }

    /// <summary>Renseigne l'origine commerciale du dossier.</summary>
    public void SetCommercialContext(
        string? marketSegmentCode,
        string? channelCode,
        string? sourceCode,
        string? companyCode,
        string? agencyCode,
        string? conventionCode)
    {
        MarketSegmentCode = LodgingText.OptionalCode(marketSegmentCode, nameof(marketSegmentCode), CodeMaxLength);
        ChannelCode = LodgingText.OptionalCode(channelCode, nameof(channelCode), CodeMaxLength);
        SourceCode = LodgingText.OptionalCode(sourceCode, nameof(sourceCode), CodeMaxLength);
        CompanyCode = LodgingText.OptionalCode(companyCode, nameof(companyCode), CodeMaxLength);
        AgencyCode = LodgingText.OptionalCode(agencyCode, nameof(agencyCode), CodeMaxLength);
        ConventionCode = LodgingText.OptionalCode(conventionCode, nameof(conventionCode), CodeMaxLength);
    }

    public void SetNotes(string? notes, string? specialRequests)
    {
        Notes = LodgingText.Optional(notes, nameof(notes), NotesMaxLength);
        SpecialRequests = LodgingText.Optional(specialRequests, nameof(specialRequests), SpecialRequestsMaxLength);
    }

    /// <summary>Marque la vente comme prise au comptoir avec le client present.</summary>
    public void MarkAsWalkIn()
    {
        IsWalkIn = true;
    }

    /// <summary>Marque la vente comme ayant franchi la capacite physique.</summary>
    public void MarkAsOverbooking()
    {
        IsOverbooking = true;
    }

    /// <summary>
    /// Change la composition des occupants. Le controle de capacite du type est fait par le
    /// service, qui seul connait le type ; l'entite ne garde que les bornes de saisie.
    /// </summary>
    public void ChangeGuestMix(int adults, int children, int infants)
    {
        if (Status.IsClosed())
        {
            throw new InvalidOperationException(
                "La composition d'un sejour termine, annule ou en no-show ne peut plus etre modifiee.");
        }

        ApplyGuestMix(adults, children, infants);
    }

    /// <summary>Pose ou remplace la garantie du dossier.</summary>
    public void SetGuarantee(GuaranteeKind guarantee, string? reference)
    {
        if (!Enum.IsDefined(guarantee))
        {
            throw new ArgumentOutOfRangeException(nameof(guarantee), guarantee, "Nature de garantie inconnue.");
        }

        Guarantee = guarantee;
        GuaranteeReference = guarantee == GuaranteeKind.None
            ? null
            : LodgingText.Optional(reference, nameof(reference), GuaranteeReferenceMaxLength);
    }

    /// <summary>
    /// Fige la politique d'annulation applicable au dossier. Ecrite UNE SEULE FOIS : la reecrire
    /// reviendrait a changer retroactivement les conditions acceptees par le client.
    /// </summary>
    public void FreezeCancellationPolicy(string policyCode, string policySnapshotJson)
    {
        if (CancellationPolicySnapshotJson is not null)
        {
            throw new InvalidOperationException(
                "La politique d'annulation de ce dossier est deja figee : elle ne peut plus etre remplacee.");
        }

        CancellationPolicyCode = LodgingText.RequireCode(policyCode, nameof(policyCode), CodeMaxLength);
        CancellationPolicySnapshotJson = LodgingText.Require(policySnapshotJson, nameof(policySnapshotJson), 4000);
    }

    /// <summary>Retient la penalite calculee depuis la politique figee.</summary>
    public void ApplyCancellationFee(decimal amount)
    {
        CancellationFeeAmount = LodgingText.Money(amount, nameof(amount));
    }

    // ------------------------------------- Affectation -------------------------------------

    /// <summary>
    /// Affecte une chambre physique au sejour. Le service a deja verifie que la chambre est libre
    /// sur toute la periode, active, du bon type et de la bonne unite ; l'entite ne garde que la
    /// regle de statut.
    /// </summary>
    public void AssignRoom(Guid roomId)
    {
        if (roomId == Guid.Empty)
        {
            throw new ArgumentException("L'identifiant de la chambre est requis.", nameof(roomId));
        }

        if (Status.IsClosed())
        {
            throw new InvalidOperationException(
                "Une chambre ne peut plus etre affectee a un sejour termine, annule ou en no-show.");
        }

        RoomId = roomId;
    }

    /// <summary>
    /// Retire la chambre du dossier, qui redevient une vente par type. Refuse une fois le client
    /// arrive : on ne retire pas la chambre de quelqu'un qui dort dedans, on le DEPLACE.
    /// </summary>
    public void ReleaseRoom()
    {
        if (!Status.IsPreArrival())
        {
            throw new InvalidOperationException(
                "La chambre d'un sejour deja commence ne peut pas etre retiree : utilisez un changement de chambre.");
        }

        RoomId = null;
    }

    /// <summary>
    /// Deplace le sejour vers une autre chambre. Autorise avant ET pendant le sejour - c'est
    /// justement pendant qu'on deplace un client - mais jamais apres.
    /// </summary>
    public void MoveToRoom(Guid roomId)
    {
        if (roomId == Guid.Empty)
        {
            throw new ArgumentException("L'identifiant de la chambre est requis.", nameof(roomId));
        }

        if (Status.IsClosed())
        {
            throw new InvalidOperationException(
                "Un sejour termine, annule ou en no-show ne peut plus changer de chambre.");
        }

        if (RoomId == roomId)
        {
            throw new InvalidOperationException("Le sejour occupe deja cette chambre.");
        }

        RoomId = roomId;
    }

    /// <summary>
    /// Change le TYPE vendu (surclassement ou declassement). Le type d'origine reste intact ; le
    /// tarif, lui, n'est pas touche ici : c'est un geste separe, parce qu'un surclassement peut
    /// tres bien etre offert.
    /// </summary>
    public void ChangeRoomType(string roomTypeCode)
    {
        if (Status.IsClosed())
        {
            throw new InvalidOperationException(
                "Le type d'un sejour termine, annule ou en no-show ne peut plus etre change.");
        }

        var normalized = RoomType.NormalizeCode(roomTypeCode);

        if (normalized == RoomTypeCode)
        {
            throw new InvalidOperationException("Le sejour est deja vendu sur ce type de chambre.");
        }

        RoomTypeCode = normalized;
    }

    // --------------------------------------- Periode ---------------------------------------

    /// <summary>
    /// Deplace ou prolonge le sejour. Le service a deja verifie la disponibilite, les allotements
    /// et les restrictions sur la NOUVELLE periode, et il repose ensuite les tarifs figes ; sans
    /// ce repricing la prolongation facturerait des nuits sans prix.
    ///
    /// La date d'arrivee d'un sejour DEJA COMMENCE ne bouge pas : le client est arrive, la nuit a
    /// eu lieu, la reecrire falsifierait l'occupation passee.
    /// </summary>
    public void Reschedule(DateOnly arrivalDate, DateOnly departureDate)
    {
        if (Status.IsClosed())
        {
            throw new InvalidOperationException(
                "Les dates d'un sejour termine, annule ou en no-show ne peuvent plus etre modifiees.");
        }

        if (departureDate <= arrivalDate)
        {
            throw new ArgumentException(
                "La date de depart doit etre posterieure a la date d'arrivee (un sejour couvre au moins une nuit).",
                nameof(departureDate));
        }

        if (Status == ReservationStatus.CheckedIn && arrivalDate != ArrivalDate)
        {
            throw new InvalidOperationException(
                "La date d'arrivee d'un sejour en cours ne peut plus etre modifiee : la nuit a eu lieu.");
        }

        ArrivalDate = arrivalDate;
        DepartureDate = departureDate;
    }

    // ---------------------------------------- Tarifs ----------------------------------------

    /// <summary>
    /// Fige le detail des tarifs nuit par nuit a la creation. Le detail doit couvrir EXACTEMENT
    /// les nuits de [arrivee, depart) et son montant de nuit d'arrivee doit egaler le tarif plat :
    /// les deux representations ne peuvent jamais diverger.
    /// </summary>
    public void FreezeNightlyRates(IReadOnlyCollection<ReservationNightRate> nightlyRates)
    {
        if (NightlyRatesSnapshotJson is not null)
        {
            throw new InvalidOperationException(
                "Les tarifs de ce sejour sont deja figes. Utilisez une revision de tarif pour les remplacer.");
        }

        ApplyNightlyRates(nightlyRates);
    }

    /// <summary>
    /// Repose les tarifs figes apres un geste qui change ce qui est vendu : prolongation, decalage
    /// de dates, changement de type, revision commerciale.
    ///
    /// Le remplacement est EXPLICITE et jamais implicite. L'appelant journalise l'ancien total, ce
    /// qui est la seule facon de repondre plus tard a "pourquoi ce sejour ne coute plus le meme
    /// prix qu'a la reservation".
    /// </summary>
    public void RepriceNightlyRates(IReadOnlyCollection<ReservationNightRate> nightlyRates)
    {
        if (Status.IsClosed())
        {
            throw new InvalidOperationException(
                "Les tarifs d'un sejour termine, annule ou en no-show ne peuvent plus etre revises.");
        }

        ApplyNightlyRates(nightlyRates);
    }

    /// <summary>
    /// Les tarifs nuit par nuit, ordonnes : le detail fige quand il existe, sinon
    /// <see cref="NightlyRateSnapshot"/> applique a chaque nuit (lignes anciennes, ou detail
    /// illisible - la facturation correspond alors exactement a ce que ces sejours ont toujours
    /// facture). Le folio facture ces montants, nuit par nuit.
    /// </summary>
    public IReadOnlyList<ReservationNightRate> GetNightlyRates()
    {
        if (!string.IsNullOrWhiteSpace(NightlyRatesSnapshotJson))
        {
            try
            {
                var documents = JsonSerializer.Deserialize<NightRateDocument[]>(NightlyRatesSnapshotJson);

                if (documents is not null && documents.Length == Nights)
                {
                    return documents
                        .Select(document => new ReservationNightRate(document.Night, document.Amount, document.RatePlanCode))
                        .OrderBy(rate => rate.Night)
                        .ToArray();
                }
            }
            catch (JsonException)
            {
                // On retombe sur le tarif plat ci-dessous.
            }
        }

        var flatRates = new ReservationNightRate[Nights];

        for (var index = 0; index < flatRates.Length; index++)
        {
            flatRates[index] = new ReservationNightRate(
                ArrivalDate.AddDays(index),
                NightlyRateSnapshot,
                RatePlanCodeSnapshot);
        }

        return flatRates;
    }

    /// <summary>Le tarif fige de la nuit donnee, ou null quand elle n'appartient pas au sejour.</summary>
    public ReservationNightRate? GetNightlyRate(DateOnly night)
    {
        return GetNightlyRates().FirstOrDefault(rate => rate.Night == night);
    }

    /// <summary>Forme de stockage d'une entree de <see cref="NightlyRatesSnapshotJson"/>.</summary>
    private sealed record NightRateDocument(
        [property: JsonPropertyName("night")] DateOnly Night,
        [property: JsonPropertyName("amount")] decimal Amount,
        [property: JsonPropertyName("ratePlanCode")] string RatePlanCode);

    // ------------------------------------- Transitions -------------------------------------

    /// <summary>
    /// Fait passer le dossier d'un etat d'avant-arrivee a un autre : demande -> option ->
    /// confirmee -> garantie, dans n'importe quel ordre. Le service journalise le passage.
    /// </summary>
    public void MoveToPreArrivalStatus(ReservationStatus status)
    {
        if (!Enum.IsDefined(status) || !status.IsPreArrival())
        {
            throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "Seuls les statuts d'avant-arrivee (demande, option, confirmee, garantie) sont acceptes ici.");
        }

        if (!Status.IsPreArrival())
        {
            throw new InvalidOperationException(
                "Seul un dossier d'avant-arrivee peut changer de statut commercial.");
        }

        if (status == ReservationStatus.Guaranteed && Guarantee == GuaranteeKind.None)
        {
            throw new InvalidOperationException(
                "Un dossier ne peut pas etre declare garanti sans garantie : posez d'abord la garantie.");
        }

        Status = status;
    }

    /// <summary>
    /// Enregistre l'arrivee. Autorisee depuis la veille UTC de la date d'arrivee (voir le
    /// commentaire de borne basse) jusqu'a la date de depart incluse ; refusee une fois la date de
    /// depart passee, parce qu'un dossier oublie enregistre des mois plus tard ouvrirait un folio
    /// facturant toutes les nuits d'origine.
    ///
    /// Une simple DEMANDE ne s'enregistre pas : elle ne tient pas de chambre, donc elle n'en a pas
    /// forcement une de libre. Il faut la confirmer d'abord.
    /// </summary>
    public void CheckIn(DateOnly today, string userName, DateTimeOffset utcNow)
    {
        if (!Status.IsPreArrival() || Status == ReservationStatus.Inquiry)
        {
            throw new InvalidOperationException(
                "Seule une reservation en option, confirmee ou garantie peut etre enregistree a l'arrivee.");
        }

        if (!HasRoom)
        {
            throw new InvalidOperationException(
                "Aucune chambre n'est affectee a ce dossier : affectez-en une avant l'arrivee.");
        }

        // "today" est le jour metier (convention du depot pour toute decision basee sur UtcNow),
        // mais le client vit a l'heure locale que le serveur ne connait pas. En Algerie (UTC+1),
        // un client qui arrive a 00h30 locale le jour de son arrivee est encore sur la date UTC
        // PRECEDENTE : une borne stricte "today < ArrivalDate" refuserait une arrivee parfaitement
        // legitime. La regle la plus sure sans fuseau client est de relacher la borne d'un jour
        // exactement : l'arrivee est acceptee des la veille UTC de la date d'arrivee.
        if (today < ArrivalDate.AddDays(-1))
        {
            throw new InvalidOperationException("Un sejour ne peut pas commencer avant sa date d'arrivee.");
        }

        if (today > DepartureDate)
        {
            throw new InvalidOperationException(
                "Un dossier dont la date de depart est passee ne peut plus etre enregistre a l'arrivee. "
                + "Annulez-le ou constatez le no-show.");
        }

        Status = ReservationStatus.CheckedIn;
        CheckedInAt = utcNow;
        CheckedInBy = LodgingText.Actor(userName);
    }

    /// <summary>
    /// Enregistre le depart. La regle du solde a zero vit dans le service (elle a besoin du
    /// folio) ; l'entite ne garde que la transition.
    /// </summary>
    public void CheckOut(string userName, DateTimeOffset utcNow)
    {
        if (Status != ReservationStatus.CheckedIn)
        {
            throw new InvalidOperationException("Seul un sejour en cours peut etre enregistre au depart.");
        }

        Status = ReservationStatus.CheckedOut;
        CheckedOutAt = utcNow;
        CheckedOutBy = LodgingText.Actor(userName);
    }

    /// <summary>Annule un dossier d'avant-arrivee, motif obligatoire. Libere la chambre immediatement.</summary>
    public void Cancel(string reason, string userName, DateTimeOffset utcNow)
    {
        if (!Status.IsPreArrival())
        {
            throw new InvalidOperationException("Seule une reservation d'avant-arrivee peut etre annulee.");
        }

        CancelReason = LodgingText.Require(reason, nameof(reason), 500);
        Status = ReservationStatus.Cancelled;
        CancelledAt = utcNow;
        CancelledBy = LodgingText.Actor(userName);
    }

    /// <summary>
    /// Constate le no-show, uniquement une fois la date d'arrivee PASSEE (strictement) : tant que
    /// le jour d'arrivee court, le client peut encore se presenter et l'arrivee doit rester
    /// possible.
    /// </summary>
    public void MarkNoShow(DateOnly today, string userName, DateTimeOffset utcNow)
    {
        if (!Status.IsPreArrival())
        {
            throw new InvalidOperationException("Seule une reservation d'avant-arrivee peut passer en no-show.");
        }

        if (today <= ArrivalDate)
        {
            throw new InvalidOperationException(
                "Un no-show ne peut etre constate qu'une fois la date d'arrivee passee.");
        }

        Status = ReservationStatus.NoShow;
        NoShowAt = utcNow;
        NoShowBy = LodgingText.Actor(userName);
    }

    // --------------------------------------- Internes ---------------------------------------

    private void ApplyGuestMix(int adults, int children, int infants)
    {
        if (adults <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(adults),
                adults,
                "Un sejour compte au moins un adulte.");
        }

        var checkedAdults = LodgingText.Count(adults, nameof(adults), MaxOccupants);
        var checkedChildren = LodgingText.Count(children, nameof(children), MaxOccupants);
        var checkedInfants = LodgingText.Count(infants, nameof(infants), MaxOccupants);

        if (checkedAdults + checkedChildren > MaxOccupants)
        {
            throw new ArgumentOutOfRangeException(
                nameof(adults),
                checkedAdults + checkedChildren,
                $"Un sejour ne peut pas depasser {MaxOccupants} occupants.");
        }

        Adults = checkedAdults;
        Children = checkedChildren;
        Infants = checkedInfants;
        GuestCount = checkedAdults + checkedChildren;
    }

    private void ApplyNightlyRates(IReadOnlyCollection<ReservationNightRate> nightlyRates)
    {
        ArgumentNullException.ThrowIfNull(nightlyRates);

        if (nightlyRates.Count != Nights)
        {
            throw new ArgumentException(
                $"Le detail des tarifs doit porter exactement une entree par nuit ({Nights}), "
                + $"{nightlyRates.Count} recue(s).",
                nameof(nightlyRates));
        }

        var ordered = nightlyRates.OrderBy(rate => rate.Night).ToArray();
        var expectedNight = ArrivalDate;

        foreach (var rate in ordered)
        {
            if (rate.Night != expectedNight)
            {
                throw new ArgumentException(
                    "Le detail des tarifs doit couvrir chaque nuit du sejour exactement une fois ; "
                    + $"attendu {expectedNight:yyyy-MM-dd}, recu {rate.Night:yyyy-MM-dd}.",
                    nameof(nightlyRates));
            }

            LodgingText.Money(rate.Amount, nameof(nightlyRates));
            LodgingText.Require(rate.RatePlanCode, nameof(nightlyRates), 60);
            expectedNight = expectedNight.AddDays(1);
        }

        // Le tarif plat suit toujours la nuit d'arrivee du detail : les deux representations ne
        // peuvent pas diverger, sous peine d'afficher un prix et d'en facturer un autre.
        NightlyRateSnapshot = ordered[0].Amount;
        RatePlanCodeSnapshot = ordered[0].RatePlanCode;

        NightlyRatesSnapshotJson = JsonSerializer.Serialize(
            ordered.Select(rate => new NightRateDocument(rate.Night, rate.Amount, rate.RatePlanCode)).ToArray());
    }
}
