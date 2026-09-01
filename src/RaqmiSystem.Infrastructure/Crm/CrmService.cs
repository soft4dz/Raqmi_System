using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Billing;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Crm;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Crm;
using RaqmiSystem.Domain.Lodging;
using RaqmiSystem.Domain.Organization;
using RaqmiSystem.Infrastructure.Persistence;
using System.Data;
using System.Text.Json;

namespace RaqmiSystem.Infrastructure.Crm;

/// <summary>
/// CRM and guest experience service (module 10.4).
///
/// TWO RULES SHAPE THE WHOLE FILE.
///
/// First, the module stores only what it owns - the qualification of a guest, the loyalty ledger,
/// the satisfaction answers and the contact log. The identity, the stays and the invoices shown
/// by the 360 view are READ from the modules that own them at query time. That is why nothing
/// here caches a stay count or an invoiced total: a CRM that keeps its own copy of the front
/// desk's figures ends up arguing with the front desk.
///
/// Second, everything that can be derived is derived. The point balance is the sum of the ledger,
/// the loyalty tier is the highest active tier that balance reaches, and the NPS families come
/// from <see cref="SatisfactionEntry.Classify"/> - never from a column. A stored balance or a
/// stored tier would be a second truth to keep in step with the movements that justify it.
///
/// Redeeming points is the one place where a read decides a write (the balance must cover the
/// redemption), so it uses the same atomic guard as the rest of the ERP: a Serializable
/// transaction, the balance re-read INSIDE it, and serialization failures surfaced as retryable
/// 409s rather than 500s.
/// </summary>
public sealed class CrmService(
    RaqmiDbContext dbContext,
    IAuditLogWriter auditLogWriter,
    IBillingService billingService) : ICrmService
{
    private const string SegmentsEntity = "crm.customer_segments";

    private const string ProfilesEntity = "crm.guest_profiles";

    private const string TiersEntity = "crm.loyalty_tiers";

    private const string LedgerEntity = "crm.loyalty_transactions";

    private const string CampaignsEntity = "crm.campaigns";

    private const string SatisfactionEntity = "crm.satisfaction_entries";

    private const string InteractionsEntity = "crm.guest_interactions";

    /// <summary>
    /// How many movements, contacts and answers the 360 view carries. The screen is a summary,
    /// not an archive: the dedicated statement and journals show everything.
    /// </summary>
    private const int RecentRowLimit = 10;

    /// <summary>
    /// Answer given when the balance guard finds the ledger no longer says what the request read,
    /// or when the database refused to serialize concurrent transactions. Nothing was written
    /// either way, so the caller may simply try again.
    /// </summary>
    private const string LedgerContention =
        "The point ledger was modified concurrently. Please try again.";

    // ====================================== Segments ======================================

    public async Task<IReadOnlyCollection<CustomerSegmentResponse>> ListSegmentsAsync(
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<CustomerSegment>().AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(segment => segment.IsActive);
        }

        var segments = await query
            .OrderBy(segment => segment.Code)
            .ToArrayAsync(cancellationToken);

        var guestCounts = await dbContext.Set<GuestProfile>()
            .AsNoTracking()
            .Where(profile => profile.SegmentCode != null)
            .GroupBy(profile => profile.SegmentCode!)
            .Select(group => new { Code = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.Code, row => row.Count, cancellationToken);

        return segments
            .Select(segment => Map(segment, guestCounts.GetValueOrDefault(segment.Code)))
            .ToArray();
    }

    public async Task<ApplicationResult<CustomerSegmentResponse>> CreateSegmentAsync(
        CreateCustomerSegmentRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeCodeOrEmpty(request.Code);

        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            return ApplicationResult<CustomerSegmentResponse>.Validation("Segment code is required.");
        }

        var exists = await dbContext.Set<CustomerSegment>()
            .AnyAsync(current => current.Code == normalizedCode, cancellationToken);

        if (exists)
        {
            return ApplicationResult<CustomerSegmentResponse>.Conflict("A segment with this code already exists.");
        }

        CustomerSegment segment;

        try
        {
            segment = new CustomerSegment(normalizedCode, request.Label, request.Description);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<CustomerSegmentResponse>.Validation(ex.Message);
        }

        segment.MarkCreated(context.UserName, DateTimeOffset.UtcNow);
        dbContext.Set<CustomerSegment>().Add(segment);

        try
        {
            await WriteAuditAsync(
                "crm.segment.created",
                SegmentsEntity,
                segment.Id,
                context,
                new { segment.Code, segment.Label },
                cancellationToken);

            await SaveAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            // The exists-check above and this insert are not atomic: a concurrent create with the
            // same code loses the race against the unique constraint on customer_segments.code.
            return ApplicationResult<CustomerSegmentResponse>.Conflict("A segment with this code already exists.");
        }

        return ApplicationResult<CustomerSegmentResponse>.Success(Map(segment, guestCount: 0));
    }

    public async Task<ApplicationResult<CustomerSegmentResponse>> UpdateSegmentAsync(
        string code,
        UpdateCustomerSegmentRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var segment = await FindSegmentAsync(code, cancellationToken);

        if (segment is null)
        {
            return ApplicationResult<CustomerSegmentResponse>.NotFound("Segment was not found.");
        }

        try
        {
            segment.UpdateDetails(request.Label, request.Description);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<CustomerSegmentResponse>.Validation(ex.Message);
        }

        segment.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            "crm.segment.updated",
            SegmentsEntity,
            segment.Id,
            context,
            new { segment.Code, segment.Label },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<CustomerSegmentResponse>.Success(
            Map(segment, await CountGuestsAsync(segment.Code, cancellationToken)));
    }

    public async Task<ApplicationResult<CustomerSegmentResponse>> SetSegmentActiveAsync(
        string code,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var segment = await FindSegmentAsync(code, cancellationToken);

        if (segment is null)
        {
            return ApplicationResult<CustomerSegmentResponse>.NotFound("Segment was not found.");
        }

        if (isActive)
        {
            segment.Activate();
        }
        else
        {
            segment.Deactivate();
        }

        segment.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            isActive ? "crm.segment.activated" : "crm.segment.deactivated",
            SegmentsEntity,
            segment.Id,
            context,
            new { segment.Code, segment.IsActive },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<CustomerSegmentResponse>.Success(
            Map(segment, await CountGuestsAsync(segment.Code, cancellationToken)));
    }

    // ================================ Profiles and 360 view ================================

    public async Task<IReadOnlyCollection<GuestProfileResponse>> ListGuestProfilesAsync(
        string? search,
        string? segmentCode,
        bool vipOnly,
        CancellationToken cancellationToken)
    {
        var profiles = dbContext.Set<GuestProfile>().AsNoTracking();

        var normalizedSegment = NormalizeNullableCode(segmentCode);

        if (normalizedSegment is not null)
        {
            profiles = profiles.Where(profile => profile.SegmentCode == normalizedSegment);
        }

        if (vipOnly)
        {
            profiles = profiles.Where(profile => profile.IsVip);
        }

        var normalizedSearch = string.IsNullOrWhiteSpace(search)
            ? null
            : search.Trim().ToUpperInvariant();

        // The search spans both sides of the join (the customer code on the profile, the name on
        // the customer), so it is expressed INSIDE the join rather than on its projection: a
        // Where applied after the Select would have to see through the projected record, which
        // EF cannot translate.
        var rows = await (
                from profile in profiles
                join customer in dbContext.Set<Customer>().AsNoTracking()
                    on profile.CustomerCode equals customer.Code
                where normalizedSearch == null
                    || profile.CustomerCode.Contains(normalizedSearch)
                    || customer.Name.ToUpper().Contains(normalizedSearch)
                orderby customer.Name
                select new ProfileRow(profile, customer.Name))
            .ToArrayAsync(cancellationToken);

        var balances = await LoadBalancesAsync(
            rows.Select(row => row.Profile.CustomerCode).ToArray(),
            cancellationToken);

        var segmentLabels = await LoadSegmentLabelsAsync(cancellationToken);
        var tiers = await LoadActiveTiersAsync(cancellationToken);

        return rows
            .Select(row =>
            {
                var balance = balances.GetValueOrDefault(row.Profile.CustomerCode);

                return Map(
                    row.Profile,
                    row.CustomerName,
                    segmentLabels.GetValueOrDefault(row.Profile.SegmentCode ?? string.Empty),
                    balance,
                    ResolveTier(tiers, balance).Current);
            })
            .ToArray();
    }

    public async Task<ApplicationResult<GuestProfileResponse>> GetGuestProfileAsync(
        string customerCode,
        CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeCodeOrEmpty(customerCode);

        var profile = await dbContext.Set<GuestProfile>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.CustomerCode == normalizedCode, cancellationToken);

        if (profile is null)
        {
            return ApplicationResult<GuestProfileResponse>.NotFound("Guest profile was not found.");
        }

        return ApplicationResult<GuestProfileResponse>.Success(
            await BuildProfileResponseAsync(profile, cancellationToken));
    }

    public async Task<ApplicationResult<GuestProfileResponse>> SaveGuestProfileAsync(
        string customerCode,
        SaveGuestProfileRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeCodeOrEmpty(customerCode);
        var customerName = await FindCustomerNameAsync(normalizedCode, cancellationToken);

        if (customerName is null)
        {
            // The CRM qualifies customers of the customer file; it never invents one.
            return ApplicationResult<GuestProfileResponse>.NotFound("Customer was not found.");
        }

        var segmentFailure = await ValidateSegmentAsync<GuestProfileResponse>(
            request.SegmentCode,
            cancellationToken);

        if (segmentFailure is not null)
        {
            return segmentFailure;
        }

        var profile = await dbContext.Set<GuestProfile>()
            .SingleOrDefaultAsync(current => current.CustomerCode == normalizedCode, cancellationToken);

        var isCreation = profile is null;
        var utcNow = DateTimeOffset.UtcNow;

        try
        {
            if (profile is null)
            {
                profile = new GuestProfile(
                    normalizedCode,
                    request.SegmentCode,
                    request.PreferredLanguage,
                    request.BirthDate,
                    request.Preferences,
                    request.Notes,
                    request.IsVip);

                profile.MarkCreated(context.UserName, utcNow);
                dbContext.Set<GuestProfile>().Add(profile);
            }
            else
            {
                profile.UpdateDetails(
                    request.SegmentCode,
                    request.PreferredLanguage,
                    request.BirthDate,
                    request.Preferences,
                    request.Notes,
                    request.IsVip);

                profile.MarkUpdated(context.UserName, utcNow);
            }
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<GuestProfileResponse>.Validation(ex.Message);
        }

        try
        {
            await WriteAuditAsync(
                isCreation ? "crm.profile.created" : "crm.profile.updated",
                ProfilesEntity,
                profile.Id,
                context,
                new { profile.CustomerCode, profile.SegmentCode, profile.IsVip },
                cancellationToken);

            await SaveAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            // Two concurrent first-saves both read "no profile yet"; the loser collides with
            // ux_guest_profiles_customer_code rather than creating a second profile.
            return ApplicationResult<GuestProfileResponse>.Conflict(
                "A profile was created for this customer concurrently. Please try again.");
        }

        return ApplicationResult<GuestProfileResponse>.Success(
            await BuildProfileResponseAsync(profile, cancellationToken));
    }

    public async Task<ApplicationResult<GuestProfileResponse>> SetMarketingConsentAsync(
        string customerCode,
        SetMarketingConsentRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeCodeOrEmpty(customerCode);
        var customerName = await FindCustomerNameAsync(normalizedCode, cancellationToken);

        if (customerName is null)
        {
            return ApplicationResult<GuestProfileResponse>.NotFound("Customer was not found.");
        }

        var profile = await dbContext.Set<GuestProfile>()
            .SingleOrDefaultAsync(current => current.CustomerCode == normalizedCode, cancellationToken);

        var utcNow = DateTimeOffset.UtcNow;
        var isCreation = profile is null;

        if (profile is null)
        {
            // Recording the answer is often the FIRST thing known about a guest - the opt-in
            // collected at check-in. Refusing it because nobody had qualified the guest yet would
            // lose the one piece of the relationship that has to be provable.
            profile = new GuestProfile(normalizedCode);
            profile.MarkCreated(context.UserName, utcNow);
            dbContext.Set<GuestProfile>().Add(profile);
        }
        else
        {
            profile.MarkUpdated(context.UserName, utcNow);
        }

        profile.SetMarketingConsent(request.Consent, utcNow);

        try
        {
            await WriteAuditAsync(
                request.Consent ? "crm.consent.granted" : "crm.consent.withdrawn",
                ProfilesEntity,
                profile.Id,
                context,
                new { profile.CustomerCode, profile.MarketingConsent, profile.MarketingConsentUpdatedAt },
                cancellationToken);

            await SaveAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (isCreation && ex.IsUniqueViolation())
        {
            return ApplicationResult<GuestProfileResponse>.Conflict(
                "A profile was created for this customer concurrently. Please try again.");
        }

        return ApplicationResult<GuestProfileResponse>.Success(
            await BuildProfileResponseAsync(profile, cancellationToken));
    }

    public async Task<ApplicationResult<Customer360Response>> GetCustomer360Async(
        string customerCode,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeCodeOrEmpty(customerCode);

        // The identity comes from the module that owns it, through its own service: the 360 view
        // must show exactly what the customer file shows, not a second projection of it.
        var customerResult = await billingService.GetCustomerAsync(normalizedCode, cancellationToken);

        if (!customerResult.Succeeded || customerResult.Value is null)
        {
            return ApplicationResult<Customer360Response>.NotFound("Customer was not found.");
        }

        var customer = customerResult.Value;

        var profile = await dbContext.Set<GuestProfile>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.CustomerCode == normalizedCode, cancellationToken);

        var loyalty = await BuildLoyaltyStatementAsync(
            normalizedCode,
            customer.Name,
            RecentRowLimit,
            cancellationToken);

        var profileResponse = profile is null
            ? null
            : Map(
                profile,
                customer.Name,
                (await LoadSegmentLabelsAsync(cancellationToken))
                    .GetValueOrDefault(profile.SegmentCode ?? string.Empty),
                loyalty.Balance,
                (await LoadActiveTiersAsync(cancellationToken)) is var tiers
                    ? ResolveTier(tiers, loyalty.Balance).Current
                    : null);

        var stays = await BuildStayStatisticsAsync(normalizedCode, today, cancellationToken);
        var billing = await BuildBillingStatisticsAsync(normalizedCode, cancellationToken);

        // Both listings are filtered in the database and ordered in memory: the SQLite provider of
        // the test harness refuses ORDER BY on a DateTimeOffset column. Each set is what ONE guest
        // has answered and what has been logged about them, so materializing it before ordering
        // stays proportionate.
        var surveys = (await dbContext.Set<SatisfactionEntry>()
                .AsNoTracking()
                .Where(entry => entry.CustomerCode == normalizedCode)
                .ToArrayAsync(cancellationToken))
            .OrderByDescending(entry => entry.SurveyDate)
            .ThenByDescending(entry => entry.CreatedAt)
            .ToArray();

        var interactions = (await dbContext.Set<GuestInteraction>()
                .AsNoTracking()
                .Where(interaction => interaction.CustomerCode == normalizedCode)
                .ToArrayAsync(cancellationToken))
            .OrderByDescending(interaction => interaction.OccurredAt)
            .Take(RecentRowLimit)
            .ToArray();

        var liveCampaigns = await LoadLiveCampaignsAsync(profile, today, cancellationToken);

        return ApplicationResult<Customer360Response>.Success(new Customer360Response(
            customer,
            profileResponse,
            loyalty,
            stays,
            billing,
            BuildSatisfactionStatistics(surveys),
            interactions.Select(interaction => Map(interaction, customer.Name)).ToArray(),
            surveys.Take(RecentRowLimit).Select(entry => Map(entry, customer.Name)).ToArray(),
            liveCampaigns));
    }

    // ======================================= Loyalty =======================================

    public async Task<IReadOnlyCollection<LoyaltyTierResponse>> ListLoyaltyTiersAsync(
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<LoyaltyTier>().AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(tier => tier.IsActive);
        }

        var tiers = await query
            .OrderBy(tier => tier.PointsThreshold)
            .ToArrayAsync(cancellationToken);

        return tiers.Select(Map).ToArray();
    }

    public async Task<ApplicationResult<LoyaltyTierResponse>> CreateLoyaltyTierAsync(
        CreateLoyaltyTierRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeCodeOrEmpty(request.Code);

        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            return ApplicationResult<LoyaltyTierResponse>.Validation("Tier code is required.");
        }

        var exists = await dbContext.Set<LoyaltyTier>()
            .AnyAsync(current => current.Code == normalizedCode, cancellationToken);

        if (exists)
        {
            return ApplicationResult<LoyaltyTierResponse>.Conflict("A tier with this code already exists.");
        }

        var thresholdTaken = await IsThresholdTakenAsync(request.PointsThreshold, null, cancellationToken);

        if (thresholdTaken)
        {
            return ApplicationResult<LoyaltyTierResponse>.Conflict(ThresholdTaken);
        }

        LoyaltyTier tier;

        try
        {
            tier = new LoyaltyTier(normalizedCode, request.Label, request.PointsThreshold, request.Benefits);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<LoyaltyTierResponse>.Validation(ex.Message);
        }

        tier.MarkCreated(context.UserName, DateTimeOffset.UtcNow);
        dbContext.Set<LoyaltyTier>().Add(tier);

        try
        {
            await WriteAuditAsync(
                "crm.loyalty.tier.created",
                TiersEntity,
                tier.Id,
                context,
                new { tier.Code, tier.Label, tier.PointsThreshold },
                cancellationToken);

            await SaveAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            return ApplicationResult<LoyaltyTierResponse>.Conflict(
                "A tier with this code or this threshold already exists.");
        }

        return ApplicationResult<LoyaltyTierResponse>.Success(Map(tier));
    }

    public async Task<ApplicationResult<LoyaltyTierResponse>> UpdateLoyaltyTierAsync(
        string code,
        UpdateLoyaltyTierRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var tier = await FindTierAsync(code, cancellationToken);

        if (tier is null)
        {
            return ApplicationResult<LoyaltyTierResponse>.NotFound("Tier was not found.");
        }

        if (await IsThresholdTakenAsync(request.PointsThreshold, tier.Id, cancellationToken))
        {
            return ApplicationResult<LoyaltyTierResponse>.Conflict(ThresholdTaken);
        }

        try
        {
            tier.UpdateDetails(request.Label, request.PointsThreshold, request.Benefits);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<LoyaltyTierResponse>.Validation(ex.Message);
        }

        tier.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        try
        {
            await WriteAuditAsync(
                "crm.loyalty.tier.updated",
                TiersEntity,
                tier.Id,
                context,
                new { tier.Code, tier.Label, tier.PointsThreshold },
                cancellationToken);

            await SaveAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            return ApplicationResult<LoyaltyTierResponse>.Conflict(ThresholdTaken);
        }

        return ApplicationResult<LoyaltyTierResponse>.Success(Map(tier));
    }

    public async Task<ApplicationResult<LoyaltyTierResponse>> SetLoyaltyTierActiveAsync(
        string code,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var tier = await FindTierAsync(code, cancellationToken);

        if (tier is null)
        {
            return ApplicationResult<LoyaltyTierResponse>.NotFound("Tier was not found.");
        }

        // Reactivating a tier can collide with the successor that took its threshold while it was
        // retired, which is exactly what the filtered unique index guards.
        if (isActive && await IsThresholdTakenAsync(tier.PointsThreshold, tier.Id, cancellationToken))
        {
            return ApplicationResult<LoyaltyTierResponse>.Conflict(ThresholdTaken);
        }

        if (isActive)
        {
            tier.Activate();
        }
        else
        {
            tier.Deactivate();
        }

        tier.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        try
        {
            await WriteAuditAsync(
                isActive ? "crm.loyalty.tier.activated" : "crm.loyalty.tier.deactivated",
                TiersEntity,
                tier.Id,
                context,
                new { tier.Code, tier.IsActive },
                cancellationToken);

            await SaveAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            return ApplicationResult<LoyaltyTierResponse>.Conflict(ThresholdTaken);
        }

        return ApplicationResult<LoyaltyTierResponse>.Success(Map(tier));
    }

    public async Task<ApplicationResult<LoyaltyStatementResponse>> GetLoyaltyStatementAsync(
        string customerCode,
        CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeCodeOrEmpty(customerCode);
        var customerName = await FindCustomerNameAsync(normalizedCode, cancellationToken);

        if (customerName is null)
        {
            return ApplicationResult<LoyaltyStatementResponse>.NotFound("Customer was not found.");
        }

        return ApplicationResult<LoyaltyStatementResponse>.Success(
            await BuildLoyaltyStatementAsync(normalizedCode, customerName, movementLimit: null, cancellationToken));
    }

    public async Task<ApplicationResult<LoyaltyStatementResponse>> RecordLoyaltyMovementAsync(
        string customerCode,
        LoyaltyTransactionKind kind,
        LoyaltyMovementRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeCodeOrEmpty(customerCode);
        var customerName = await FindCustomerNameAsync(normalizedCode, cancellationToken);

        if (customerName is null)
        {
            return ApplicationResult<LoyaltyStatementResponse>.NotFound("Customer was not found.");
        }

        if (kind is LoyaltyTransactionKind.Adjustment)
        {
            if (request.Points == 0)
            {
                return ApplicationResult<LoyaltyStatementResponse>.Validation(
                    "An adjustment of zero point would move nothing.");
            }
        }
        else if (request.Points <= 0)
        {
            // Earning, redeeming and expiring are all expressed as a QUANTITY of points; the
            // direction is the operation being called, so a caller never has to type a sign.
            return ApplicationResult<LoyaltyStatementResponse>.Validation(
                "The number of points must be strictly positive.");
        }

        var signedPoints = kind switch
        {
            LoyaltyTransactionKind.Redeem or LoyaltyTransactionKind.Expiry => -request.Points,
            _ => request.Points
        };

        // BALANCE GUARD. The balance must be re-read INSIDE the transaction that writes the
        // movement: read outside of one, two concurrent redemptions both see enough points and
        // both commit, leaving a guest with a negative balance. Under PostgreSQL the loser's
        // commit is refused with a serialization failure; under the SQLite test provider the
        // loser's write is turned away with "database is locked". Both become a retryable 409.
        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            var balance = await SumBalanceAsync(normalizedCode, cancellationToken);

            if (balance + signedPoints < 0)
            {
                return ApplicationResult<LoyaltyStatementResponse>.Validation(
                    $"The guest holds {balance} points, which does not cover this movement.");
            }

            LoyaltyTransaction movement;

            try
            {
                movement = new LoyaltyTransaction(
                    normalizedCode,
                    kind,
                    signedPoints,
                    request.OccurredOn,
                    request.Reason,
                    request.Reference);
            }
            catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
            {
                return ApplicationResult<LoyaltyStatementResponse>.Validation(ex.Message);
            }

            movement.MarkCreated(context.UserName, DateTimeOffset.UtcNow);
            dbContext.Set<LoyaltyTransaction>().Add(movement);

            await WriteAuditAsync(
                $"crm.loyalty.{kind.ToString().ToLowerInvariant()}",
                LedgerEntity,
                movement.Id,
                context,
                new
                {
                    movement.CustomerCode,
                    Kind = movement.Kind.ToString(),
                    movement.Points,
                    movement.Reason,
                    movement.Reference,
                    BalanceAfter = balance + signedPoints
                },
                cancellationToken);

            await SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception exception) when (exception.IsSerializationFailure())
        {
            return ApplicationResult<LoyaltyStatementResponse>.Conflict(LedgerContention);
        }

        return ApplicationResult<LoyaltyStatementResponse>.Success(
            await BuildLoyaltyStatementAsync(normalizedCode, customerName, movementLimit: null, cancellationToken));
    }

    // ====================================== Campaigns ======================================

    public async Task<IReadOnlyCollection<CampaignResponse>> ListCampaignsAsync(
        CampaignStatus? status,
        string? segmentCode,
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<Campaign>().AsNoTracking();

        if (status.HasValue)
        {
            query = query.Where(campaign => campaign.Status == status.Value);
        }

        var normalizedSegment = NormalizeNullableCode(segmentCode);

        if (normalizedSegment is not null)
        {
            query = query.Where(campaign => campaign.TargetSegmentCode == normalizedSegment);
        }

        // A campaign belongs to the period as soon as it OVERLAPS it: a campaign started last
        // month and still running is part of this month's activity.
        if (from.HasValue)
        {
            query = query.Where(campaign => campaign.EndDate >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(campaign => campaign.StartDate <= to.Value);
        }

        var campaigns = await query
            .OrderByDescending(campaign => campaign.StartDate)
            .ThenBy(campaign => campaign.Code)
            .ToArrayAsync(cancellationToken);

        var segmentLabels = await LoadSegmentLabelsAsync(cancellationToken);

        return campaigns
            .Select(campaign => Map(
                campaign,
                segmentLabels.GetValueOrDefault(campaign.TargetSegmentCode ?? string.Empty)))
            .ToArray();
    }

    public async Task<ApplicationResult<CampaignResponse>> GetCampaignAsync(
        string code,
        CancellationToken cancellationToken)
    {
        var campaign = await FindCampaignAsync(code, tracked: false, cancellationToken);

        if (campaign is null)
        {
            return ApplicationResult<CampaignResponse>.NotFound("Campaign was not found.");
        }

        return ApplicationResult<CampaignResponse>.Success(
            await MapWithSegmentAsync(campaign, cancellationToken));
    }

    public async Task<ApplicationResult<CampaignResponse>> CreateCampaignAsync(
        CreateCampaignRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeCodeOrEmpty(request.Code);

        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            return ApplicationResult<CampaignResponse>.Validation("Campaign code is required.");
        }

        var exists = await dbContext.Set<Campaign>()
            .AnyAsync(current => current.Code == normalizedCode, cancellationToken);

        if (exists)
        {
            return ApplicationResult<CampaignResponse>.Conflict("A campaign with this code already exists.");
        }

        var segmentFailure = await ValidateSegmentAsync<CampaignResponse>(
            request.TargetSegmentCode,
            cancellationToken);

        if (segmentFailure is not null)
        {
            return segmentFailure;
        }

        Campaign campaign;

        try
        {
            campaign = new Campaign(
                normalizedCode,
                request.Label,
                request.Channel,
                request.StartDate,
                request.EndDate,
                request.TargetSegmentCode,
                request.Objective,
                request.Message);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<CampaignResponse>.Validation(ex.Message);
        }

        campaign.MarkCreated(context.UserName, DateTimeOffset.UtcNow);
        dbContext.Set<Campaign>().Add(campaign);

        try
        {
            await WriteAuditAsync(
                "crm.campaign.created",
                CampaignsEntity,
                campaign.Id,
                context,
                new
                {
                    campaign.Code,
                    campaign.Label,
                    Channel = campaign.Channel.ToString(),
                    campaign.TargetSegmentCode,
                    campaign.StartDate,
                    campaign.EndDate
                },
                cancellationToken);

            await SaveAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            return ApplicationResult<CampaignResponse>.Conflict("A campaign with this code already exists.");
        }

        return ApplicationResult<CampaignResponse>.Success(
            await MapWithSegmentAsync(campaign, cancellationToken));
    }

    public async Task<ApplicationResult<CampaignResponse>> UpdateCampaignAsync(
        string code,
        UpdateCampaignRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var campaign = await FindCampaignAsync(code, tracked: true, cancellationToken);

        if (campaign is null)
        {
            return ApplicationResult<CampaignResponse>.NotFound("Campaign was not found.");
        }

        var segmentFailure = await ValidateSegmentAsync<CampaignResponse>(
            request.TargetSegmentCode,
            cancellationToken);

        if (segmentFailure is not null)
        {
            return segmentFailure;
        }

        try
        {
            campaign.UpdateDetails(
                request.Label,
                request.Channel,
                request.StartDate,
                request.EndDate,
                request.TargetSegmentCode,
                request.Objective,
                request.Message);
        }
        catch (InvalidOperationException ex)
        {
            return ApplicationResult<CampaignResponse>.Validation(ex.Message);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<CampaignResponse>.Validation(ex.Message);
        }

        campaign.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            "crm.campaign.updated",
            CampaignsEntity,
            campaign.Id,
            context,
            new { campaign.Code, campaign.Label, Channel = campaign.Channel.ToString(), campaign.TargetSegmentCode },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<CampaignResponse>.Success(
            await MapWithSegmentAsync(campaign, cancellationToken));
    }

    public Task<ApplicationResult<CampaignResponse>> ScheduleCampaignAsync(
        string code,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        return TransitionCampaignAsync(
            code,
            "crm.campaign.scheduled",
            (campaign, userName, utcNow) => campaign.Schedule(userName, utcNow),
            context,
            cancellationToken);
    }

    public Task<ApplicationResult<CampaignResponse>> LaunchCampaignAsync(
        string code,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        return TransitionCampaignAsync(
            code,
            "crm.campaign.launched",
            (campaign, userName, utcNow) => campaign.Launch(userName, utcNow),
            context,
            cancellationToken);
    }

    public Task<ApplicationResult<CampaignResponse>> CompleteCampaignAsync(
        string code,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        return TransitionCampaignAsync(
            code,
            "crm.campaign.completed",
            (campaign, userName, utcNow) => campaign.Complete(userName, utcNow),
            context,
            cancellationToken);
    }

    public Task<ApplicationResult<CampaignResponse>> CancelCampaignAsync(
        string code,
        CancelCampaignRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        return TransitionCampaignAsync(
            code,
            "crm.campaign.cancelled",
            (campaign, userName, utcNow) => campaign.Cancel(request.Reason, userName, utcNow),
            context,
            cancellationToken);
    }

    public async Task<ApplicationResult<CampaignAudienceResponse>> GetCampaignAudienceAsync(
        string code,
        CancellationToken cancellationToken)
    {
        var campaign = await FindCampaignAsync(code, tracked: false, cancellationToken);

        if (campaign is null)
        {
            return ApplicationResult<CampaignAudienceResponse>.NotFound("Campaign was not found.");
        }

        var profiles = dbContext.Set<GuestProfile>().AsNoTracking();

        // Narrowed BEFORE the join, on the profile itself: a filter applied to the projected row
        // would have to see through the record EF built, which it cannot translate.
        if (campaign.TargetSegmentCode is { } targetSegment)
        {
            profiles = profiles.Where(profile => profile.SegmentCode == targetSegment);
        }

        var rows = await (
                from profile in profiles
                join customer in dbContext.Set<Customer>().AsNoTracking()
                    on profile.CustomerCode equals customer.Code
                where customer.IsActive
                orderby customer.Name
                select new AudienceRow(profile, customer.Name, customer.Email, customer.Phone))
            .ToArrayAsync(cancellationToken);

        var balances = await LoadBalancesAsync(
            rows.Select(row => row.Profile.CustomerCode).ToArray(),
            cancellationToken);

        var excludedForConsent = 0;
        var excludedForMissingContact = 0;
        var members = new List<CampaignAudienceMember>(rows.Length);

        foreach (var row in rows)
        {
            // Consent first: a guest the establishment may not address on this channel is excluded
            // whether or not it holds their address.
            if (campaign.RequiresMarketingConsent && !row.Profile.MarketingConsent)
            {
                excludedForConsent++;
                continue;
            }

            if (!HasContactFor(campaign.Channel, row.Email, row.Phone))
            {
                excludedForMissingContact++;
                continue;
            }

            members.Add(new CampaignAudienceMember(
                row.Profile.CustomerCode,
                row.CustomerName,
                row.Profile.SegmentCode,
                row.Email,
                row.Phone,
                row.Profile.MarketingConsent,
                row.Profile.IsVip,
                balances.GetValueOrDefault(row.Profile.CustomerCode)));
        }

        return ApplicationResult<CampaignAudienceResponse>.Success(new CampaignAudienceResponse(
            campaign.Code,
            campaign.Channel,
            campaign.RequiresMarketingConsent,
            campaign.TargetSegmentCode,
            members.Count,
            excludedForConsent,
            excludedForMissingContact,
            members));
    }

    // ===================================== Satisfaction =====================================

    public async Task<IReadOnlyCollection<SatisfactionEntryResponse>> ListSatisfactionEntriesAsync(
        DateOnly? from,
        DateOnly? to,
        string? hotelUnitCode,
        string? customerCode,
        NpsCategory? category,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<SatisfactionEntry>().AsNoTracking();

        if (from.HasValue)
        {
            query = query.Where(entry => entry.SurveyDate >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(entry => entry.SurveyDate <= to.Value);
        }

        var normalizedUnit = NormalizeNullableCode(hotelUnitCode);

        if (normalizedUnit is not null)
        {
            query = query.Where(entry => entry.HotelUnitCode == normalizedUnit);
        }

        var normalizedCustomer = NormalizeNullableCode(customerCode);

        if (normalizedCustomer is not null)
        {
            query = query.Where(entry => entry.CustomerCode == normalizedCustomer);
        }

        // Filtered in the database, ordered in memory: the SQLite provider of the test harness
        // refuses ORDER BY on a DateTimeOffset column, and this listing has no LIMIT, so sorting
        // the materialized set is exactly equivalent on both providers.
        var entries = (await query.ToArrayAsync(cancellationToken))
            .OrderByDescending(entry => entry.SurveyDate)
            .ThenByDescending(entry => entry.CreatedAt)
            .ToArray();

        // The NPS families are read from SatisfactionEntry.Classify rather than re-expressed as a
        // score range in the query: the cut-offs of the method have exactly one definition.
        var filtered = category.HasValue
            ? entries.Where(entry => entry.Category == category.Value).ToArray()
            : entries;

        var customerNames = await LoadCustomerNamesAsync(
            filtered.Select(entry => entry.CustomerCode).Distinct().ToArray(),
            cancellationToken);

        return filtered
            .Select(entry => Map(entry, customerNames.GetValueOrDefault(entry.CustomerCode)))
            .ToArray();
    }

    public async Task<ApplicationResult<SatisfactionEntryResponse>> RecordSatisfactionAsync(
        RecordSatisfactionRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var normalizedCustomer = NormalizeCodeOrEmpty(request.CustomerCode);
        var customerName = await FindCustomerNameAsync(normalizedCustomer, cancellationToken);

        if (customerName is null)
        {
            return ApplicationResult<SatisfactionEntryResponse>.NotFound("Customer was not found.");
        }

        var normalizedUnit = NormalizeCodeOrEmpty(request.HotelUnitCode);

        var unitExists = await dbContext.Set<HotelUnit>()
            .AnyAsync(unit => unit.Code == normalizedUnit, cancellationToken);

        if (!unitExists)
        {
            return ApplicationResult<SatisfactionEntryResponse>.NotFound("Hotel unit was not found.");
        }

        if (request.ReservationId is { } reservationId && reservationId != Guid.Empty)
        {
            var reservationExists = await dbContext.Set<Reservation>()
                .AnyAsync(reservation => reservation.Id == reservationId, cancellationToken);

            if (!reservationExists)
            {
                return ApplicationResult<SatisfactionEntryResponse>.NotFound("Reservation was not found.");
            }
        }

        SatisfactionEntry entry;

        try
        {
            entry = new SatisfactionEntry(
                normalizedCustomer,
                normalizedUnit,
                request.SurveyDate,
                request.Score,
                request.Source,
                request.ReservationId,
                request.Comment);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<SatisfactionEntryResponse>.Validation(ex.Message);
        }

        entry.MarkCreated(context.UserName, DateTimeOffset.UtcNow);
        dbContext.Set<SatisfactionEntry>().Add(entry);

        await WriteAuditAsync(
            "crm.satisfaction.recorded",
            SatisfactionEntity,
            entry.Id,
            context,
            new
            {
                entry.CustomerCode,
                entry.HotelUnitCode,
                entry.SurveyDate,
                entry.Score,
                Category = entry.Category.ToString(),
                Source = entry.Source.ToString()
            },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<SatisfactionEntryResponse>.Success(Map(entry, customerName));
    }

    public async Task<ApplicationResult<NpsSummaryResponse>> GetNpsSummaryAsync(
        DateOnly from,
        DateOnly to,
        string? hotelUnitCode,
        CancellationToken cancellationToken)
    {
        if (to < from)
        {
            return ApplicationResult<NpsSummaryResponse>.Validation("The from date cannot be after the to date.");
        }

        var normalizedUnit = NormalizeNullableCode(hotelUnitCode);

        var query = dbContext.Set<SatisfactionEntry>()
            .AsNoTracking()
            .Where(entry => entry.SurveyDate >= from && entry.SurveyDate <= to);

        if (normalizedUnit is not null)
        {
            var unitExists = await dbContext.Set<HotelUnit>()
                .AnyAsync(unit => unit.Code == normalizedUnit, cancellationToken);

            if (!unitExists)
            {
                return ApplicationResult<NpsSummaryResponse>.NotFound("Hotel unit was not found.");
            }

            query = query.Where(entry => entry.HotelUnitCode == normalizedUnit);
        }

        // Only the unit and the score are read back, and the classification happens in memory
        // through SatisfactionEntry.Classify: expressing the 0-6 / 7-8 / 9-10 cut-offs as a SQL
        // range here would be a second copy of the method's own definition, free to drift from
        // the domain's. A period of answers is a small set - one row per guest who replied.
        var answers = await query
            .Select(entry => new ScoreRow(entry.HotelUnitCode, entry.Score))
            .ToArrayAsync(cancellationToken);

        var unitNames = await dbContext.Set<HotelUnit>()
            .AsNoTracking()
            .ToDictionaryAsync(unit => unit.Code, unit => unit.Name, cancellationToken);

        var units = answers
            .GroupBy(answer => answer.HotelUnitCode)
            .Select(group => BuildUnitBreakdown(
                group.Key,
                unitNames.GetValueOrDefault(group.Key, group.Key),
                group.Select(answer => answer.Score).ToArray()))
            .OrderBy(unit => unit.HotelUnitCode)
            .ToArray();

        var allScores = answers.Select(answer => answer.Score).ToArray();
        var counts = CountCategories(allScores);

        return ApplicationResult<NpsSummaryResponse>.Success(new NpsSummaryResponse(
            from,
            to,
            normalizedUnit,
            allScores.Length,
            counts.Promoters,
            counts.Passives,
            counts.Detractors,
            SatisfactionEntry.ComputeNps(counts.Promoters, counts.Passives, counts.Detractors),
            Average(allScores),
            units));
    }

    // ===================================== Interactions =====================================

    public async Task<IReadOnlyCollection<GuestInteractionResponse>> ListInteractionsAsync(
        DateOnly? from,
        DateOnly? to,
        string? customerCode,
        string? hotelUnitCode,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<GuestInteraction>().AsNoTracking();

        if (from.HasValue)
        {
            var lowerBound = new DateTimeOffset(from.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            query = query.Where(interaction => interaction.OccurredAt >= lowerBound);
        }

        if (to.HasValue)
        {
            // Inclusive on the to date: an interaction logged at 18:00 on the last day of the
            // period belongs to it.
            var upperBound = new DateTimeOffset(to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            query = query.Where(interaction => interaction.OccurredAt < upperBound);
        }

        var normalizedCustomer = NormalizeNullableCode(customerCode);

        if (normalizedCustomer is not null)
        {
            query = query.Where(interaction => interaction.CustomerCode == normalizedCustomer);
        }

        var normalizedUnit = NormalizeNullableCode(hotelUnitCode);

        if (normalizedUnit is not null)
        {
            query = query.Where(interaction => interaction.HotelUnitCode == normalizedUnit);
        }

        // Filtered in the database, ordered in memory, for the same reason as the satisfaction
        // journal above: no LIMIT here, so the two are strictly equivalent.
        var interactions = (await query.ToArrayAsync(cancellationToken))
            .OrderByDescending(interaction => interaction.OccurredAt)
            .ToArray();

        var customerNames = await LoadCustomerNamesAsync(
            interactions.Select(interaction => interaction.CustomerCode).Distinct().ToArray(),
            cancellationToken);

        return interactions
            .Select(interaction => Map(interaction, customerNames.GetValueOrDefault(interaction.CustomerCode)))
            .ToArray();
    }

    public async Task<ApplicationResult<GuestInteractionResponse>> LogInteractionAsync(
        LogGuestInteractionRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var normalizedCustomer = NormalizeCodeOrEmpty(request.CustomerCode);
        var customerName = await FindCustomerNameAsync(normalizedCustomer, cancellationToken);

        if (customerName is null)
        {
            return ApplicationResult<GuestInteractionResponse>.NotFound("Customer was not found.");
        }

        var normalizedUnit = NormalizeNullableCode(request.HotelUnitCode);

        if (normalizedUnit is not null)
        {
            var unitExists = await dbContext.Set<HotelUnit>()
                .AnyAsync(unit => unit.Code == normalizedUnit, cancellationToken);

            if (!unitExists)
            {
                return ApplicationResult<GuestInteractionResponse>.NotFound("Hotel unit was not found.");
            }
        }

        GuestInteraction interaction;

        try
        {
            interaction = new GuestInteraction(
                normalizedCustomer,
                request.OccurredAt,
                request.Channel,
                request.Direction,
                request.Subject,
                request.HandledBy,
                normalizedUnit,
                request.Notes);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<GuestInteractionResponse>.Validation(ex.Message);
        }

        interaction.MarkCreated(context.UserName, DateTimeOffset.UtcNow);
        dbContext.Set<GuestInteraction>().Add(interaction);

        await WriteAuditAsync(
            "crm.interaction.logged",
            InteractionsEntity,
            interaction.Id,
            context,
            new
            {
                interaction.CustomerCode,
                interaction.HotelUnitCode,
                interaction.OccurredAt,
                Channel = interaction.Channel.ToString(),
                Direction = interaction.Direction.ToString(),
                interaction.Subject,
                interaction.HandledBy
            },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<GuestInteractionResponse>.Success(Map(interaction, customerName));
    }

    // ======================================= Internals =======================================

    private const string ThresholdTaken =
        "Another active tier already opens at this number of points.";

    private async Task<ApplicationResult<CampaignResponse>> TransitionCampaignAsync(
        string code,
        string auditAction,
        Action<Campaign, string, DateTimeOffset> transition,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var campaign = await FindCampaignAsync(code, tracked: true, cancellationToken);

        if (campaign is null)
        {
            return ApplicationResult<CampaignResponse>.NotFound("Campaign was not found.");
        }

        var utcNow = DateTimeOffset.UtcNow;

        try
        {
            transition(campaign, context.UserName, utcNow);
        }
        catch (InvalidOperationException ex)
        {
            // The lifecycle rules live in the entity; a refused transition is a business answer,
            // not a server failure.
            return ApplicationResult<CampaignResponse>.Validation(ex.Message);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<CampaignResponse>.Validation(ex.Message);
        }

        campaign.MarkUpdated(context.UserName, utcNow);

        await WriteAuditAsync(
            auditAction,
            CampaignsEntity,
            campaign.Id,
            context,
            new { campaign.Code, Status = campaign.Status.ToString(), campaign.CancelReason },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<CampaignResponse>.Success(
            await MapWithSegmentAsync(campaign, cancellationToken));
    }

    /// <summary>
    /// A segment carried by a profile or targeted by a campaign must exist AND be active: pointing
    /// new work at a retired segment is how an audience silently becomes empty. Returns null when
    /// the segment is acceptable (including when none was given).
    /// </summary>
    private async Task<ApplicationResult<T>?> ValidateSegmentAsync<T>(
        string? segmentCode,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeNullableCode(segmentCode);

        if (normalized is null)
        {
            return null;
        }

        var segment = await dbContext.Set<CustomerSegment>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Code == normalized, cancellationToken);

        if (segment is null)
        {
            return ApplicationResult<T>.NotFound("Segment was not found.");
        }

        return segment.IsActive
            ? null
            : ApplicationResult<T>.Validation("This segment is deactivated and can no longer be assigned.");
    }

    private Task<CustomerSegment?> FindSegmentAsync(string code, CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeCodeOrEmpty(code);

        return dbContext.Set<CustomerSegment>()
            .SingleOrDefaultAsync(current => current.Code == normalizedCode, cancellationToken);
    }

    private Task<LoyaltyTier?> FindTierAsync(string code, CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeCodeOrEmpty(code);

        return dbContext.Set<LoyaltyTier>()
            .SingleOrDefaultAsync(current => current.Code == normalizedCode, cancellationToken);
    }

    private Task<Campaign?> FindCampaignAsync(string code, bool tracked, CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeCodeOrEmpty(code);

        var query = tracked
            ? dbContext.Set<Campaign>()
            : dbContext.Set<Campaign>().AsNoTracking();

        return query.SingleOrDefaultAsync(current => current.Code == normalizedCode, cancellationToken);
    }

    private Task<int> CountGuestsAsync(string segmentCode, CancellationToken cancellationToken)
    {
        return dbContext.Set<GuestProfile>()
            .AsNoTracking()
            .CountAsync(profile => profile.SegmentCode == segmentCode, cancellationToken);
    }

    private Task<bool> IsThresholdTakenAsync(
        int pointsThreshold,
        Guid? exceptTierId,
        CancellationToken cancellationToken)
    {
        return dbContext.Set<LoyaltyTier>()
            .AsNoTracking()
            .AnyAsync(
                tier => tier.IsActive
                    && tier.PointsThreshold == pointsThreshold
                    && (exceptTierId == null || tier.Id != exceptTierId),
                cancellationToken);
    }

    private async Task<string?> FindCustomerNameAsync(string customerCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(customerCode))
        {
            return null;
        }

        return await dbContext.Set<Customer>()
            .AsNoTracking()
            .Where(customer => customer.Code == customerCode)
            .Select(customer => customer.Name)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<Dictionary<string, string>> LoadCustomerNamesAsync(
        string[] customerCodes,
        CancellationToken cancellationToken)
    {
        if (customerCodes.Length == 0)
        {
            return new Dictionary<string, string>();
        }

        return await dbContext.Set<Customer>()
            .AsNoTracking()
            .Where(customer => customerCodes.Contains(customer.Code))
            .ToDictionaryAsync(customer => customer.Code, customer => customer.Name, cancellationToken);
    }

    private async Task<Dictionary<string, string>> LoadSegmentLabelsAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Set<CustomerSegment>()
            .AsNoTracking()
            .ToDictionaryAsync(segment => segment.Code, segment => segment.Label, cancellationToken);
    }

    private async Task<LoyaltyTier[]> LoadActiveTiersAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Set<LoyaltyTier>()
            .AsNoTracking()
            .Where(tier => tier.IsActive)
            .OrderBy(tier => tier.PointsThreshold)
            .ToArrayAsync(cancellationToken);
    }

    private Task<int> SumBalanceAsync(string customerCode, CancellationToken cancellationToken)
    {
        return dbContext.Set<LoyaltyTransaction>()
            .Where(movement => movement.CustomerCode == customerCode)
            .SumAsync(movement => movement.Points, cancellationToken);
    }

    private async Task<Dictionary<string, int>> LoadBalancesAsync(
        string[] customerCodes,
        CancellationToken cancellationToken)
    {
        if (customerCodes.Length == 0)
        {
            return new Dictionary<string, int>();
        }

        return await dbContext.Set<LoyaltyTransaction>()
            .AsNoTracking()
            .Where(movement => customerCodes.Contains(movement.CustomerCode))
            .GroupBy(movement => movement.CustomerCode)
            .Select(group => new { Code = group.Key, Balance = group.Sum(movement => movement.Points) })
            .ToDictionaryAsync(row => row.Code, row => row.Balance, cancellationToken);
    }

    /// <summary>
    /// The tier rule of the programme, in one place: the current tier is the highest ACTIVE tier
    /// the balance reaches, and the next one is the cheapest tier above it. Both are null when the
    /// programme has no active tier - an installation that has not set one up yet.
    /// </summary>
    private static (LoyaltyTier? Current, LoyaltyTier? Next) ResolveTier(
        IReadOnlyList<LoyaltyTier> tiersByThreshold,
        int balance)
    {
        LoyaltyTier? current = null;
        LoyaltyTier? next = null;

        foreach (var tier in tiersByThreshold)
        {
            if (tier.PointsThreshold <= balance)
            {
                current = tier;
                continue;
            }

            next = tier;
            break;
        }

        return (current, next);
    }

    private async Task<GuestProfileResponse> BuildProfileResponseAsync(
        GuestProfile profile,
        CancellationToken cancellationToken)
    {
        var customerName = await FindCustomerNameAsync(profile.CustomerCode, cancellationToken) ?? profile.CustomerCode;
        var balance = await SumBalanceAsync(profile.CustomerCode, cancellationToken);
        var tiers = await LoadActiveTiersAsync(cancellationToken);

        var segmentLabel = profile.SegmentCode is null
            ? null
            : (await LoadSegmentLabelsAsync(cancellationToken)).GetValueOrDefault(profile.SegmentCode);

        return Map(profile, customerName, segmentLabel, balance, ResolveTier(tiers, balance).Current);
    }

    private async Task<LoyaltyStatementResponse> BuildLoyaltyStatementAsync(
        string customerCode,
        string customerName,
        int? movementLimit,
        CancellationToken cancellationToken)
    {
        var balance = await SumBalanceAsync(customerCode, cancellationToken);
        var tiers = await LoadActiveTiersAsync(cancellationToken);
        var (current, next) = ResolveTier(tiers, balance);

        // Filtered in the database, ordered in memory: the SQLite provider of the test harness
        // refuses ORDER BY on a DateTimeOffset column, and the tie-breaker between two movements
        // of the same day is precisely the moment they were posted. The set is one guest's
        // ledger, so materializing it before ordering stays proportionate - and the limit, when
        // there is one, must be applied AFTER the ordering anyway.
        var ledger = await dbContext.Set<LoyaltyTransaction>()
            .AsNoTracking()
            .Where(movement => movement.CustomerCode == customerCode)
            .ToArrayAsync(cancellationToken);

        var ordered = ledger
            .OrderByDescending(movement => movement.OccurredOn)
            .ThenByDescending(movement => movement.CreatedAt);

        var movements = movementLimit.HasValue
            ? ordered.Take(movementLimit.Value).ToArray()
            : ordered.ToArray();

        return new LoyaltyStatementResponse(
            customerCode,
            customerName,
            balance,
            current?.Code,
            current?.Label,
            current?.Benefits,
            next?.Code,
            next?.Label,
            next is null ? null : next.PointsThreshold - balance,
            movements.Select(Map).ToArray());
    }

    private async Task<GuestStayStatistics> BuildStayStatisticsAsync(
        string customerCode,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        // The stay total is read from the per-night snapshot frozen on each reservation
        // (Reservation.TotalStayAmount), which EF does not map, so the rows are materialized. One
        // guest has a handful of stays - this is not a report over the whole hotel.
        var reservations = await dbContext.Set<Reservation>()
            .AsNoTracking()
            .Where(reservation => reservation.CustomerCode == customerCode)
            .ToArrayAsync(cancellationToken);

        // What the guest actually slept: a booking still to come is not a stay, and a cancelled
        // one never was.
        var stayed = reservations
            .Where(reservation => reservation.Status is ReservationStatus.CheckedIn or ReservationStatus.CheckedOut)
            .ToArray();

        return new GuestStayStatistics(
            stayed.Length,
            stayed.Sum(reservation => reservation.Nights),
            stayed.Length == 0 ? null : stayed.Min(reservation => reservation.ArrivalDate),
            stayed.Length == 0 ? null : stayed.Max(reservation => reservation.DepartureDate),
            stayed.Sum(reservation => reservation.TotalStayAmount),
            reservations.Count(reservation =>
                reservation.Status.IsPreArrival() && reservation.ArrivalDate >= today),
            reservations.Count(reservation => reservation.Status == ReservationStatus.Cancelled),
            reservations.Count(reservation => reservation.Status == ReservationStatus.NoShow));
    }

    private async Task<GuestBillingStatistics> BuildBillingStatisticsAsync(
        string customerCode,
        CancellationToken cancellationToken)
    {
        var invoices = await dbContext.Set<Invoice>()
            .AsNoTracking()
            .Where(invoice => invoice.CustomerCode == customerCode)
            .Where(invoice => invoice.Status != InvoiceStatus.Cancelled)
            .Select(invoice => new InvoiceRow(invoice.Status, invoice.TotalInclVat))
            .ToArrayAsync(cancellationToken);

        return new GuestBillingStatistics(
            invoices.Length,
            invoices.Sum(invoice => invoice.TotalInclVat),
            invoices
                .Where(invoice => invoice.Status == InvoiceStatus.Issued)
                .Sum(invoice => invoice.TotalInclVat));
    }

    private static GuestSatisfactionStatistics BuildSatisfactionStatistics(
        IReadOnlyList<SatisfactionEntry> surveys)
    {
        var scores = surveys.Select(entry => entry.Score).ToArray();
        var counts = CountCategories(scores);
        var last = surveys.Count == 0 ? null : surveys[0];

        return new GuestSatisfactionStatistics(
            scores.Length,
            Average(scores),
            SatisfactionEntry.ComputeNps(counts.Promoters, counts.Passives, counts.Detractors),
            last?.SurveyDate,
            last?.Score);
    }

    private async Task<IReadOnlyCollection<CampaignResponse>> LoadLiveCampaignsAsync(
        GuestProfile? profile,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var running = await dbContext.Set<Campaign>()
            .AsNoTracking()
            .Where(campaign => campaign.Status == CampaignStatus.Running)
            .Where(campaign => campaign.StartDate <= today && campaign.EndDate >= today)
            .OrderBy(campaign => campaign.StartDate)
            .ToArrayAsync(cancellationToken);

        var segmentLabels = await LoadSegmentLabelsAsync(cancellationToken);

        return running
            // An untargeted campaign addresses the whole file; a targeted one only reaches a guest
            // sitting in its segment - and a direct channel only reaches one who consented, which
            // is the same rule the audience applies.
            .Where(campaign => campaign.TargetSegmentCode is null
                || campaign.TargetSegmentCode == profile?.SegmentCode)
            .Where(campaign => !campaign.RequiresMarketingConsent || profile?.MarketingConsent == true)
            .Select(campaign => Map(
                campaign,
                segmentLabels.GetValueOrDefault(campaign.TargetSegmentCode ?? string.Empty)))
            .ToArray();
    }

    /// <summary>
    /// Can this channel physically reach the guest? An email campaign needs an address, a text or
    /// a call needs a number, and an on-site offer needs neither - it is served to a guest already
    /// standing there.
    /// </summary>
    private static bool HasContactFor(CampaignChannel channel, string? email, string? phone)
    {
        return channel switch
        {
            CampaignChannel.Email => !string.IsNullOrWhiteSpace(email),
            CampaignChannel.Sms or CampaignChannel.Phone => !string.IsNullOrWhiteSpace(phone),
            _ => true
        };
    }

    private static NpsUnitBreakdown BuildUnitBreakdown(string unitCode, string unitName, int[] scores)
    {
        var counts = CountCategories(scores);

        return new NpsUnitBreakdown(
            unitCode,
            unitName,
            scores.Length,
            counts.Promoters,
            counts.Passives,
            counts.Detractors,
            SatisfactionEntry.ComputeNps(counts.Promoters, counts.Passives, counts.Detractors),
            Average(scores));
    }

    private static (int Promoters, int Passives, int Detractors) CountCategories(IReadOnlyCollection<int> scores)
    {
        var promoters = 0;
        var passives = 0;
        var detractors = 0;

        foreach (var score in scores)
        {
            switch (SatisfactionEntry.Classify(score))
            {
                case NpsCategory.Promoter:
                    promoters++;
                    break;

                case NpsCategory.Passive:
                    passives++;
                    break;

                default:
                    detractors++;
                    break;
            }
        }

        return (promoters, passives, detractors);
    }

    /// <summary>Average score to one decimal, or null when nobody answered.</summary>
    private static decimal? Average(IReadOnlyCollection<int> scores)
    {
        if (scores.Count == 0)
        {
            return null;
        }

        return Math.Round((decimal)scores.Sum() / scores.Count, 1, MidpointRounding.AwayFromZero);
    }

    private async Task<CampaignResponse> MapWithSegmentAsync(Campaign campaign, CancellationToken cancellationToken)
    {
        var label = campaign.TargetSegmentCode is null
            ? null
            : (await LoadSegmentLabelsAsync(cancellationToken)).GetValueOrDefault(campaign.TargetSegmentCode);

        return Map(campaign, label);
    }

    private static CustomerSegmentResponse Map(CustomerSegment segment, int guestCount)
    {
        return new CustomerSegmentResponse(
            segment.Id,
            segment.Code,
            segment.Label,
            segment.Description,
            segment.IsActive,
            guestCount,
            segment.CreatedAt,
            segment.CreatedBy,
            segment.UpdatedAt,
            segment.UpdatedBy);
    }

    private static GuestProfileResponse Map(
        GuestProfile profile,
        string customerName,
        string? segmentLabel,
        int loyaltyPoints,
        LoyaltyTier? tier)
    {
        return new GuestProfileResponse(
            profile.Id,
            profile.CustomerCode,
            customerName,
            profile.SegmentCode,
            segmentLabel,
            profile.PreferredLanguage,
            profile.BirthDate,
            profile.Preferences,
            profile.Notes,
            profile.IsVip,
            profile.MarketingConsent,
            profile.MarketingConsentUpdatedAt,
            loyaltyPoints,
            tier?.Code,
            tier?.Label,
            profile.CreatedAt,
            profile.CreatedBy,
            profile.UpdatedAt,
            profile.UpdatedBy);
    }

    private static LoyaltyTierResponse Map(LoyaltyTier tier)
    {
        return new LoyaltyTierResponse(
            tier.Id,
            tier.Code,
            tier.Label,
            tier.PointsThreshold,
            tier.Benefits,
            tier.IsActive,
            tier.CreatedAt,
            tier.CreatedBy,
            tier.UpdatedAt,
            tier.UpdatedBy);
    }

    private static LoyaltyTransactionResponse Map(LoyaltyTransaction movement)
    {
        return new LoyaltyTransactionResponse(
            movement.Id,
            movement.CustomerCode,
            movement.Kind,
            movement.Points,
            movement.OccurredOn,
            movement.Reason,
            movement.Reference,
            movement.CreatedAt,
            movement.CreatedBy);
    }

    private static CampaignResponse Map(Campaign campaign, string? targetSegmentLabel)
    {
        return new CampaignResponse(
            campaign.Id,
            campaign.Code,
            campaign.Label,
            campaign.Channel,
            campaign.TargetSegmentCode,
            targetSegmentLabel,
            campaign.StartDate,
            campaign.EndDate,
            campaign.Status,
            campaign.Objective,
            campaign.Message,
            campaign.CanEdit,
            campaign.RequiresMarketingConsent,
            campaign.ScheduledAt,
            campaign.ScheduledBy,
            campaign.LaunchedAt,
            campaign.LaunchedBy,
            campaign.CompletedAt,
            campaign.CompletedBy,
            campaign.CancelledAt,
            campaign.CancelledBy,
            campaign.CancelReason,
            campaign.CreatedAt,
            campaign.CreatedBy,
            campaign.UpdatedAt,
            campaign.UpdatedBy);
    }

    private static SatisfactionEntryResponse Map(SatisfactionEntry entry, string? customerName)
    {
        return new SatisfactionEntryResponse(
            entry.Id,
            entry.CustomerCode,
            customerName ?? entry.CustomerCode,
            entry.HotelUnitCode,
            entry.SurveyDate,
            entry.Score,
            entry.Category,
            entry.Source,
            entry.ReservationId,
            entry.Comment,
            entry.CreatedAt,
            entry.CreatedBy);
    }

    private static GuestInteractionResponse Map(GuestInteraction interaction, string? customerName)
    {
        return new GuestInteractionResponse(
            interaction.Id,
            interaction.CustomerCode,
            customerName ?? interaction.CustomerCode,
            interaction.HotelUnitCode,
            interaction.OccurredAt,
            interaction.Channel,
            interaction.Direction,
            interaction.Subject,
            interaction.HandledBy,
            interaction.Notes,
            interaction.CreatedAt,
            interaction.CreatedBy);
    }

    private static string NormalizeCodeOrEmpty(string code)
    {
        return string.IsNullOrWhiteSpace(code) ? string.Empty : code.Trim().ToUpperInvariant();
    }

    private static string? NormalizeNullableCode(string? code)
    {
        return string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpperInvariant();
    }

    /// <summary>
    /// Explicit flush after the audit write. AuditLogWriter.WriteAsync already calls
    /// SaveChangesAsync internally (persisting the pending entity changes together with the audit
    /// row), so this call is usually a no-op - it exists so persistence never silently depends on
    /// the audit writer's internals.
    /// </summary>
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task WriteAuditAsync(
        string action,
        string entityName,
        Guid entityId,
        OperationContext context,
        object details,
        CancellationToken cancellationToken)
    {
        await auditLogWriter.WriteAsync(
            new AuditLogEntry(
                context.UserId,
                context.UserName,
                action,
                entityName,
                entityId.ToString(),
                context.IpAddress,
                JsonSerializer.Serialize(details)),
            cancellationToken);
    }

    /// <summary>Projection shapes of the joins above. Named types so EF can translate them.</summary>
    private sealed record ProfileRow(GuestProfile Profile, string CustomerName);

    private sealed record AudienceRow(GuestProfile Profile, string CustomerName, string? Email, string? Phone);

    private sealed record ScoreRow(string HotelUnitCode, int Score);

    private sealed record InvoiceRow(InvoiceStatus Status, decimal TotalInclVat);
}
