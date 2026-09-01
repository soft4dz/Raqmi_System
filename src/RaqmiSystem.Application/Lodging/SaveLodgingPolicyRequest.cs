namespace RaqmiSystem.Application.Lodging;

public sealed record SaveLodgingPolicyRequest(
    TimeOnly CheckInTime,
    TimeOnly CheckOutTime,
    TimeOnly? EarlyCheckInFromTime,
    bool EarlyCheckInIsFree,
    decimal EarlyCheckInFlatCharge,
    decimal EarlyCheckInPercentOfNight,
    TimeOnly? LateCheckOutUntilTime,
    bool LateCheckOutIsFree,
    decimal LateCheckOutFlatCharge,
    decimal LateCheckOutPercentOfNight,
    bool OutOfServiceReducesInventory,
    bool OverbookingEnabled);
