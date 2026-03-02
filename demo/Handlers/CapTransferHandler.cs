using System.Text.Json;
using Sapl.Core.Constraints.Api;

namespace Sapl.Demo.Handlers;

public sealed class CapTransferHandler : IMethodInvocationConstraintHandlerProvider
{
    private readonly ILogger<CapTransferHandler> _logger;

    public CapTransferHandler(ILogger<CapTransferHandler> logger)
    {
        _logger = logger;
    }

    public bool IsResponsible(JsonElement constraint)
    {
        return constraint.TryGetProperty("type", out var t) && t.GetString() == "capTransferAmount";
    }

    public Action<MethodInvocationContext> GetHandler(JsonElement constraint)
    {
        var maxAmount = constraint.TryGetProperty("maxAmount", out var m) ? m.GetDouble() : 5000;

        return context =>
        {
            if (context.Args.Length > 0 && context.Args[0] is double requested && requested > maxAmount)
            {
                _logger.LogInformation("[CAP] {Class}.{Method} args[0]: {Requested} -> {Max} (limit: {Max})",
                    context.ClassName, context.MethodName, requested, maxAmount, maxAmount);
                context.Args[0] = maxAmount;
            }
        };
    }
}
