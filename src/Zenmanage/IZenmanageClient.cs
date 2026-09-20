using Zenmanage.Flagging;

namespace Zenmanage;

/// <summary>
/// Public contract for the Zenmanage client.
/// Implement this interface to allow mocking in tests.
/// </summary>
public interface IZenmanageClient : IDisposable
{
    /// <summary>
    /// Returns the flag manager used for retrieving and evaluating flags.
    /// </summary>
    IFlagManager Flags();
}
