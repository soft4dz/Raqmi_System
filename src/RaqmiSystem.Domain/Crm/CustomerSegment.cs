using RaqmiSystem.Domain.Common;

namespace RaqmiSystem.Domain.Crm;

/// <summary>
/// A commercial family of customers (business travellers, tour operators, loyal leisure guests,
/// ...). Segments are the addressing unit of the module: a campaign targets a segment, never a
/// hand-picked list of guests, so that who receives what stays a property of the customer file
/// and not of a list someone typed once.
///
/// A segment is DEACTIVATED, never deleted, exactly like the customer file it qualifies: the
/// guests already carrying it, and the campaigns already run on it, must keep reading the way
/// they happened.
/// </summary>
public sealed class CustomerSegment : AuditableEntity
{
    private CustomerSegment()
    {
    }

    public CustomerSegment(string code, string label, string? description = null)
    {
        Code = NormalizeCode(code);
        ApplyDetails(label, description);
        IsActive = true;
    }

    public string Code { get; private set; } = string.Empty;

    public string Label { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public bool IsActive { get; private set; }

    public void UpdateDetails(string label, string? description)
    {
        ApplyDetails(label, description);
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public static string NormalizeCode(string value)
    {
        return CrmText.RequireCode(value, nameof(value));
    }

    private void ApplyDetails(string label, string? description)
    {
        Label = CrmText.Require(label, nameof(label), 160);
        Description = CrmText.Optional(description, nameof(description), 400);
    }
}
