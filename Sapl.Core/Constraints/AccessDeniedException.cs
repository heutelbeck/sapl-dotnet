namespace Sapl.Core.Constraints;

public sealed class AccessDeniedException : Exception
{
    internal const string ErrorAccessDenied = "Access denied.";

    public AccessDeniedException() : base(ErrorAccessDenied)
    {
    }

    public AccessDeniedException(string message) : base(message)
    {
    }

    public AccessDeniedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
