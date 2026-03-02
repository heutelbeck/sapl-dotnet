using System.Text;

namespace Sapl.Core.Client;

public sealed class SseParser
{
    internal const string ErrorBufferOverflow = "SSE buffer overflow: line exceeds maximum buffer size.";

    private const int MaxBufferSize = 1_048_576; // 1 MB

    private readonly StringBuilder _buffer = new();
    private readonly List<string> _dataLines = [];

    public IEnumerable<string> ProcessChunk(string chunk)
    {
        foreach (var ch in chunk)
        {
            if (ch == '\n' || ch == '\r')
            {
                var line = _buffer.ToString();
                _buffer.Clear();

                foreach (var data in ProcessLine(line))
                {
                    yield return data;
                }
            }
            else
            {
                if (_buffer.Length >= MaxBufferSize)
                {
                    throw new SseBufferOverflowException(ErrorBufferOverflow);
                }
                _buffer.Append(ch);
            }
        }
    }

    public string? Flush()
    {
        if (_dataLines.Count == 0)
            return null;
        var result = string.Join("\n", _dataLines);
        _dataLines.Clear();
        return result;
    }

    public void Reset()
    {
        _buffer.Clear();
        _dataLines.Clear();
    }

    private IEnumerable<string> ProcessLine(string line)
    {
        if (line.StartsWith(':'))
        {
            yield break;
        }

        if (line.Length == 0)
        {
            if (_dataLines.Count > 0)
            {
                var data = string.Join("\n", _dataLines);
                _dataLines.Clear();
                yield return data;
            }
            yield break;
        }

        if (line.StartsWith("data:", StringComparison.Ordinal))
        {
            var value = line.Length > 5 && line[5] == ' '
                ? line[6..]
                : line[5..];
            _dataLines.Add(value);
        }
    }
}

public sealed class SseBufferOverflowException : Exception
{
    public SseBufferOverflowException(string message) : base(message)
    {
    }

    public SseBufferOverflowException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public SseBufferOverflowException()
    {
    }
}
