namespace RaqmiSystem.Domain.Lodging;

public enum ChargeKind
{
    /// <summary>One accommodation night, generated automatically at check-in. Always positive.</summary>
    Night,

    /// <summary>Any extra consumed during the stay (minibar, restaurant, ...). Always positive.</summary>
    Extra,

    /// <summary>
    /// A payment applied to the folio, referencing the treasury receipt it mirrors. Recorded as a
    /// NEGATIVE amount so the folio balance converges to zero.
    /// </summary>
    Settlement,

    /// <summary>A commercial gesture or correction. The only other kind allowed to be negative.</summary>
    Adjustment
}
