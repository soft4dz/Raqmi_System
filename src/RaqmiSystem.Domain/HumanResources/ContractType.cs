namespace RaqmiSystem.Domain.HumanResources;

/// <summary>Employment contract nature, as reported on CNAS and ANEM declarations.</summary>
public enum ContractType
{
    /// <summary>CDI - open-ended contract, no end date.</summary>
    Permanent = 0,

    /// <summary>CDD - fixed-term contract, an end date is mandatory.</summary>
    FixedTerm = 1,

    /// <summary>Seasonal contract, an end date is mandatory.</summary>
    Seasonal = 2,

    /// <summary>Internship or apprenticeship, an end date is mandatory.</summary>
    Internship = 3
}
