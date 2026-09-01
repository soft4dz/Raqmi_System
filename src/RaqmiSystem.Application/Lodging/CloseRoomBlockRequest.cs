namespace RaqmiSystem.Application.Lodging;

/// <summary>
/// Remise en service. <paramref name="ReturnDate"/> est la date REELLE de retour, qui peut tomber
/// avant ou apres la fin prevue : une panne se repare rarement le jour annonce.
/// </summary>
public sealed record CloseRoomBlockRequest(DateOnly ReturnDate);
