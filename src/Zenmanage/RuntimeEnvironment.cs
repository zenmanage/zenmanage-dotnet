namespace Zenmanage;

/// <summary>
/// Describes whether the SDK is being used in a trusted server environment or an untrusted client environment.
/// </summary>
public enum RuntimeEnvironment
{
    /// <summary>
    /// Use server keys prefixed with <c>srv_</c>.
    /// </summary>
    Server,

    /// <summary>
    /// Use client keys prefixed with <c>cli_</c>.
    /// </summary>
    Client
}