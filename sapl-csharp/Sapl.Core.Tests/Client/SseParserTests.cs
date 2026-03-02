using FluentAssertions;
using Sapl.Core.Client;

namespace Sapl.Core.Tests.Client;

public class SseParserTests
{
    [Fact]
    void WhenSingleDataEventThenYieldsData()
    {
        var parser = new SseParser();
        var results = parser.ProcessChunk("data: {\"decision\":\"PERMIT\"}\n\n").ToList();
        results.Should().ContainSingle()
            .Which.Should().Be("{\"decision\":\"PERMIT\"}");
    }

    [Fact]
    void WhenMultipleEventsThenYieldsAll()
    {
        var parser = new SseParser();
        var results = parser.ProcessChunk(
            "data: {\"decision\":\"PERMIT\"}\n\ndata: {\"decision\":\"DENY\"}\n\n").ToList();
        results.Should().HaveCount(2);
        results[0].Should().Be("{\"decision\":\"PERMIT\"}");
        results[1].Should().Be("{\"decision\":\"DENY\"}");
    }

    [Fact]
    void WhenCommentLineThenIgnored()
    {
        var parser = new SseParser();
        var results = parser.ProcessChunk(": keep-alive\ndata: hello\n\n").ToList();
        results.Should().ContainSingle()
            .Which.Should().Be("hello");
    }

    [Fact]
    void WhenEmptyDataLineThenIgnored()
    {
        var parser = new SseParser();
        var results = parser.ProcessChunk("data: \n\n").ToList();
        results.Should().ContainSingle()
            .Which.Should().Be("");
    }

    [Fact]
    void WhenMultipleDataLinesThenJoinedWithNewline()
    {
        var parser = new SseParser();
        var results = parser.ProcessChunk("data: line1\ndata: line2\n\n").ToList();
        results.Should().ContainSingle()
            .Which.Should().Be("line1\nline2");
    }

    [Fact]
    void WhenChunkSplitAcrossCallsThenBuffersCorrectly()
    {
        var parser = new SseParser();
        var results1 = parser.ProcessChunk("data: {\"dec").ToList();
        results1.Should().BeEmpty();

        var results2 = parser.ProcessChunk("ision\":\"PERMIT\"}\n\n").ToList();
        results2.Should().ContainSingle()
            .Which.Should().Be("{\"decision\":\"PERMIT\"}");
    }

    [Fact]
    void WhenDataWithoutSpaceAfterColonThenParsesCorrectly()
    {
        var parser = new SseParser();
        var results = parser.ProcessChunk("data:{\"x\":1}\n\n").ToList();
        results.Should().ContainSingle()
            .Which.Should().Be("{\"x\":1}");
    }

    [Fact]
    void WhenBufferOverflowThenThrows()
    {
        var parser = new SseParser();
        var longLine = new string('x', 1_048_577);
        var act = () => parser.ProcessChunk(longLine).ToList();
        act.Should().Throw<SseBufferOverflowException>();
    }

    [Fact]
    void WhenResetThenClearsState()
    {
        var parser = new SseParser();
        parser.ProcessChunk("data: partial").ToList();
        parser.Reset();
        var results = parser.ProcessChunk("data: fresh\n\n").ToList();
        results.Should().ContainSingle()
            .Which.Should().Be("fresh");
    }

    [Fact]
    void WhenOnlyNewlinesThenNoOutput()
    {
        var parser = new SseParser();
        var results = parser.ProcessChunk("\n\n\n").ToList();
        results.Should().BeEmpty();
    }

    [Fact]
    void WhenCarriageReturnThenTreatedAsLineEnding()
    {
        var parser = new SseParser();
        var results = parser.ProcessChunk("data: test\r\r").ToList();
        results.Should().ContainSingle()
            .Which.Should().Be("test");
    }

    [Fact]
    void WhenFlushWithPendingDataThenReturnsIt()
    {
        var parser = new SseParser();
        parser.ProcessChunk("data: pending\n").ToList();
        var flushed = parser.Flush();
        flushed.Should().Be("pending");
    }

    [Fact]
    void WhenFlushWithNoDataThenReturnsNull()
    {
        var parser = new SseParser();
        parser.Flush().Should().BeNull();
    }
}
