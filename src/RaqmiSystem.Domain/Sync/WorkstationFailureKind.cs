namespace RaqmiSystem.Domain.Sync;

/// <summary>
/// Nature of a client-side failure, as classified BY THE WORKSTATION that reports it. The server
/// stores the classification without re-deriving it: it never observed the failure, precisely
/// because most of these failures mean the call never reached the server.
/// </summary>
public enum WorkstationFailureKind
{
    /// <summary>The request never reached the server (cable, switch, wrong address, API down).</summary>
    Network = 0,

    /// <summary>The request left but no answer came back in time.</summary>
    Timeout = 1,

    /// <summary>The server answered with a non-success status code.</summary>
    HttpError = 2,

    /// <summary>Anything the client could not classify.</summary>
    Unexpected = 3
}
