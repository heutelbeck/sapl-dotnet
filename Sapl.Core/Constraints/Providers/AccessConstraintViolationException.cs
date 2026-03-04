namespace Sapl.Core.Constraints.Providers;

public sealed class AccessConstraintViolationException : Exception
{
    public AccessConstraintViolationException(string message) : base(message)
    {
    }

    public AccessConstraintViolationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
