using RaqmiSystem.Domain.Common;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Domain.Billing;

public sealed class Invoice : AuditableEntity
{
    private readonly List<InvoiceLine> _lines = new();

    private Invoice()
    {
    }

    public Invoice(string customerCode, string hotelUnitCode, DateOnly invoiceDate)
    {
        CustomerCode = Customer.NormalizeCode(customerCode);
        HotelUnitCode = HotelUnit.NormalizeCode(hotelUnitCode);
        InvoiceDate = invoiceDate;
        Status = InvoiceStatus.Draft;
    }

    /// <summary>
    /// Definitive invoice number ("FAC-{year}-{sequence:D6}"). Null while the invoice is a
    /// draft: the number is only allocated at issue time so that abandoned drafts never burn
    /// a slot in the legal numbering sequence.
    /// </summary>
    public string? Number { get; private set; }

    public int? IssuedYear { get; private set; }

    public int? IssuedSequence { get; private set; }

    public string CustomerCode { get; private set; } = string.Empty;

    public string HotelUnitCode { get; private set; } = string.Empty;

    public DateOnly InvoiceDate { get; private set; }

    public InvoiceStatus Status { get; private set; } = InvoiceStatus.Draft;

    public decimal TotalExclVat { get; private set; }

    public decimal TotalVat { get; private set; }

    public decimal TotalInclVat { get; private set; }

    public DateTimeOffset? IssuedAt { get; private set; }

    public string? IssuedBy { get; private set; }

    /// <summary>
    /// Legal immutability of issued invoices: the customer's identification as it stood at
    /// the moment of issuance is frozen into these snapshot columns (filled by
    /// <see cref="CaptureCustomerSnapshot"/> during the issue operation). They stay null
    /// while the invoice is a Draft; once Issued, readers must render the snapshot rather
    /// than the live customer record, so later edits to the customer never rewrite history.
    /// </summary>
    public string? CustomerNameSnapshot { get; private set; }

    public string? CustomerNifSnapshot { get; private set; }

    public string? CustomerRcSnapshot { get; private set; }

    public string? CustomerAiSnapshot { get; private set; }

    public string? CustomerNisSnapshot { get; private set; }

    public string? CustomerAddressSnapshot { get; private set; }

    public DateTimeOffset? PaidAt { get; private set; }

    public string? PaidBy { get; private set; }

    public DateTimeOffset? CancelledAt { get; private set; }

    public string? CancelledBy { get; private set; }

    public string? CancellationReason { get; private set; }

    public IReadOnlyCollection<InvoiceLine> Lines => _lines.AsReadOnly();

    public bool CanEdit => Status == InvoiceStatus.Draft;

    public void ReplaceLines(IReadOnlyCollection<InvoiceLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        if (Status != InvoiceStatus.Draft)
        {
            throw new InvalidOperationException("Only draft invoices can be modified.");
        }

        _lines.Clear();

        var lineNumber = 1;

        foreach (var line in lines)
        {
            ArgumentNullException.ThrowIfNull(line);
            line.SetLineNumber(lineNumber++);
            _lines.Add(line);
        }

        RecalculateTotals();
    }

    /// <summary>
    /// Freezes the customer's identification into the invoice at issue time. Must be called
    /// while the invoice is still a Draft (immediately before <see cref="Issue"/>).
    /// </summary>
    public void CaptureCustomerSnapshot(
        string customerName,
        string? nif,
        string? rc,
        string? ai,
        string? nis,
        string? address)
    {
        if (Status != InvoiceStatus.Draft)
        {
            throw new InvalidOperationException("The customer snapshot can only be captured on a draft invoice.");
        }

        CustomerNameSnapshot = RequireValue(customerName, nameof(customerName), 200);
        CustomerNifSnapshot = nif;
        CustomerRcSnapshot = rc;
        CustomerAiSnapshot = ai;
        CustomerNisSnapshot = nis;
        CustomerAddressSnapshot = address;
    }

    public void Issue(int year, int sequence, string userName, DateTimeOffset utcNow)
    {
        if (Status != InvoiceStatus.Draft)
        {
            throw new InvalidOperationException("Only draft invoices can be issued.");
        }

        if (_lines.Count == 0)
        {
            throw new InvalidOperationException("An invoice requires at least one line to be issued.");
        }

        SetNumber(year, sequence);
        Status = InvoiceStatus.Issued;
        IssuedAt = utcNow;
        IssuedBy = RequireActor(userName);
    }

    /// <summary>
    /// Re-allocates the issue number after a unique-index collision when two invoices were
    /// issued concurrently for the same year. Only valid on an invoice that has just been
    /// issued (in-memory) but whose number failed to persist.
    /// </summary>
    public void ReassignIssueNumber(int year, int sequence)
    {
        if (Status != InvoiceStatus.Issued)
        {
            throw new InvalidOperationException("Only issued invoices can be renumbered.");
        }

        SetNumber(year, sequence);
    }

    public void MarkPaid(string userName, DateTimeOffset utcNow)
    {
        if (Status != InvoiceStatus.Issued)
        {
            throw new InvalidOperationException("Only issued invoices can be marked as paid.");
        }

        Status = InvoiceStatus.Paid;
        PaidAt = utcNow;
        PaidBy = RequireActor(userName);
    }

    public void Cancel(string reason, string userName, DateTimeOffset utcNow)
    {
        if (Status is not (InvoiceStatus.Draft or InvoiceStatus.Issued))
        {
            throw new InvalidOperationException("Only draft or issued invoices can be cancelled.");
        }

        var normalizedReason = RequireValue(reason, nameof(reason), 500);

        Status = InvoiceStatus.Cancelled;
        CancelledAt = utcNow;
        CancelledBy = RequireActor(userName);
        CancellationReason = normalizedReason;
    }

    public static string FormatNumber(int year, int sequence)
    {
        return $"FAC-{year}-{sequence:D6}";
    }

    private void SetNumber(int year, int sequence)
    {
        if (year is < 2000 or > 2999)
        {
            throw new ArgumentOutOfRangeException(nameof(year), year, "Year is out of the supported range.");
        }

        if (sequence is < 1 or > 999999)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), sequence, "Sequence must be between 1 and 999999.");
        }

        IssuedYear = year;
        IssuedSequence = sequence;
        Number = FormatNumber(year, sequence);
    }

    private void RecalculateTotals()
    {
        TotalExclVat = _lines.Sum(line => line.LineTotalExclVat);
        TotalVat = _lines.Sum(line => line.VatAmount);
        TotalInclVat = TotalExclVat + TotalVat;
    }

    private static string RequireActor(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            return "system";
        }

        return userName.Trim();
    }

    private static string RequireValue(string value, string argumentName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", argumentName);
        }

        var trimmed = value.Trim();

        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"Value cannot exceed {maxLength} characters.", argumentName);
        }

        return trimmed;
    }
}
