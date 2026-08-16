namespace Zenmanage.Exceptions;

/// <summary>
/// Raised when flag rules cannot be retrieved from the API.
/// </summary>
public sealed class FetchRulesException : ZenmanageException
{
    public FetchRulesException(string message)
        : base(message)
    {
    }

    public FetchRulesException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}