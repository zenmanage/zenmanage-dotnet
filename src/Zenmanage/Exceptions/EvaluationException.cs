namespace Zenmanage.Exceptions;

/// <summary>
/// Raised when a requested flag cannot be evaluated.
/// </summary>
public sealed class EvaluationException : ZenmanageException
{
    public EvaluationException(string message)
        : base(message)
    {
    }
}