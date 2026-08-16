namespace Zenmanage.Exceptions;

/// <summary>
/// Raised when the rules response shape is invalid.
/// </summary>
public sealed class InvalidRulesException : ZenmanageException
{
    public InvalidRulesException(string message)
        : base(message)
    {
    }
}