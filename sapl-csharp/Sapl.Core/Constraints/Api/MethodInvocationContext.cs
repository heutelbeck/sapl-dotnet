namespace Sapl.Core.Constraints.Api;

public sealed class MethodInvocationContext
{
    public MethodInvocationContext(
        object?[] args,
        string methodName,
        string? className,
        object? request)
    {
        Args = args;
        MethodName = methodName;
        ClassName = className;
        Request = request;
    }

    public object?[] Args { get; }

    public string MethodName { get; }

    public string? ClassName { get; }

    public object? Request { get; }
}
