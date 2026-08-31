using RaqmiSystem.Domain.Common;

namespace RaqmiSystem.Domain.Inventory;

/// <summary>
/// Physical inventory count: one warehouse, one date, the quantities actually found on the
/// shelves. This is the real-world mechanics of inventories: counting produces a DOCUMENT,
/// and validating that document generates the ADJUSTMENT movements (counted minus theoretical
/// stock, in either direction) in the registry. After validation the count is immutable: it
/// is the proof behind the adjustments it generated, so rewriting it would orphan them.
/// The theoretical stock is read - and the adjustments are written - by the service inside an
/// atomic guard (same pattern as posting a journal entry); the entity enforces the
/// draft-only edits and the one-way Draft -> Validated transition.
/// </summary>
public sealed class InventoryCount : AuditableEntity
{
    private readonly List<InventoryCountLine> _lines = new();

    private InventoryCount()
    {
    }

    public InventoryCount(string warehouseCode, DateOnly countDate)
    {
        WarehouseCode = Warehouse.NormalizeCode(warehouseCode);
        CountDate = countDate;
        Status = InventoryCountStatus.Draft;
    }

    public string WarehouseCode { get; private set; } = string.Empty;

    public DateOnly CountDate { get; private set; }

    public InventoryCountStatus Status { get; private set; } = InventoryCountStatus.Draft;

    public DateTimeOffset? ValidatedAt { get; private set; }

    public string? ValidatedBy { get; private set; }

    public IReadOnlyCollection<InventoryCountLine> Lines => _lines.AsReadOnly();

    public bool CanEdit => Status == InventoryCountStatus.Draft;

    public void ReplaceLines(IReadOnlyCollection<InventoryCountLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        if (Status != InventoryCountStatus.Draft)
        {
            throw new InvalidOperationException("A validated inventory count is immutable.");
        }

        // One line per item: two counts of the same item in one inventory would make the
        // adjustment ambiguous (which of the two is the shelf truth?).
        var duplicated = lines
            .GroupBy(line => line.ItemCode)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicated is not null)
        {
            throw new ArgumentException(
                $"Item '{duplicated.Key}' is counted more than once in this inventory.",
                nameof(lines));
        }

        _lines.Clear();

        var lineNumber = 1;

        foreach (var line in lines)
        {
            ArgumentNullException.ThrowIfNull(line);
            line.SetLineNumber(lineNumber++);
            _lines.Add(line);
        }
    }

    public void Validate(string userName, DateTimeOffset utcNow)
    {
        if (Status != InventoryCountStatus.Draft)
        {
            throw new InvalidOperationException("This inventory count has already been validated.");
        }

        if (_lines.Count == 0)
        {
            throw new InvalidOperationException("An inventory count requires at least one counted line to be validated.");
        }

        Status = InventoryCountStatus.Validated;
        ValidatedAt = utcNow;
        ValidatedBy = RequireActor(userName);
    }

    private static string RequireActor(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            return "system";
        }

        return userName.Trim();
    }
}
