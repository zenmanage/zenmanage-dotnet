namespace Zenmanage.Exceptions;

/// <summary>
/// Raised when a cache backend fails to read or write cached data.
/// </summary>
public sealed class CacheException : ZenmanageException
{
    public CacheException(string message)
        : base(message)
    {
    }

    public CacheException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
