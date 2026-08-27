using RaqmiSystem.Domain.Common;

namespace RaqmiSystem.Domain.Organization;

public sealed class HotelUnit : AuditableEntity
{
    private HotelUnit()
    {
    }

    public HotelUnit(string code, string name)
    {
        Code = RequireValue(code, nameof(code));
        Name = RequireValue(name, nameof(name));
        IsActive = true;
    }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public void Rename(string name)
    {
        Name = RequireValue(name, nameof(name));
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    private static string RequireValue(string value, string argumentName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", argumentName);
        }

        return value.Trim();
    }
}
