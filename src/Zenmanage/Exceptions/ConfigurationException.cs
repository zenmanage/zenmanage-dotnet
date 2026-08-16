namespace Zenmanage.Exceptions;

/// <summary>
/// Raised when SDK configuration is invalid.
/// </summary>
public sealed class ConfigurationException : ZenmanageException
{
    public ConfigurationException(string message)
        : base(message)
    {
    }
}