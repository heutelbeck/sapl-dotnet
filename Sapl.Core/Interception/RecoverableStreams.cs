using System.Runtime.CompilerServices;

namespace Sapl.Core.Interception;

public static class RecoverableStreams
{
    public static IAsyncEnumerable<T> Recover<T>(
        this IAsyncEnumerable<T> source,
        Action<AccessSignal>? onSignal = null)
    {
        return FilterSignals(source, onSignal, null, null);
    }

    public static IAsyncEnumerable<T> RecoverWith<T>(
        this IAsyncEnumerable<T> source,
        Func<T>? onDenyItem = null,
        Func<T>? onRecoverItem = null)
    {
        return FilterSignals(source, null, onDenyItem, onRecoverItem);
    }

    private static async IAsyncEnumerable<T> FilterSignals<T>(
        IAsyncEnumerable<T> source,
        Action<AccessSignal>? onSignal,
        Func<T>? onDenyItem,
        Func<T>? onRecoverItem,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            if (item is AccessSignal signal)
            {
                onSignal?.Invoke(signal);

                var replacement = signal.Kind switch
                {
                    AccessSignalKind.Denied => onDenyItem,
                    AccessSignalKind.Recovered => onRecoverItem,
                    _ => null,
                };

                if (replacement is not null)
                {
                    yield return replacement();
                }

                continue;
            }

            yield return item;
        }
    }
}
