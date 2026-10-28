using StageLink.Protocol;

namespace StageLink.Protocol.Tests;

public class FrameReaderTests
{
    // SetGain -6.5 dB, message number 1 (the example in ROADMAP.md)
    private static readonly byte[] ValidFrame =
        [0x53, 0x4C, 0x01, 0x20, 0x00, 0x01, 0x00, 0x02, 0xFD, 0x76, 0xCA, 0x6B];

    private static FrameReader ReaderFor(byte[] bytes)
    {
        return new FrameReader(new MemoryStream(bytes));
    }

    [Fact]
    public async Task ReadFrameAsync_ReadsOneFrame()
    {
        FrameReader reader = ReaderFor(ValidFrame);

        Frame? frame = await reader.ReadFrameAsync();

        Assert.NotNull(frame);
        Assert.Equal(Command.SetGain, frame.Command);
        Assert.Equal(1, frame.MessageNumber);
    }

    [Fact]
    public async Task ReadFrameAsync_ReadsFramesSentBackToBack()
    {
        byte[] second = new Frame(Command.Ping, 2, []).Encode();
        FrameReader reader = ReaderFor([.. ValidFrame, .. second]);

        Frame? first = await reader.ReadFrameAsync();
        Frame? next = await reader.ReadFrameAsync();
        Frame? end = await reader.ReadFrameAsync();

        Assert.Equal(Command.SetGain, first?.Command);
        Assert.Equal(Command.Ping, next?.Command);
        Assert.Null(end);
    }

    [Fact]
    public async Task ReadFrameAsync_ReturnsNullWhenStreamIsEmpty()
    {
        FrameReader reader = ReaderFor([]);

        Assert.Null(await reader.ReadFrameAsync());
    }

    [Fact]
    public async Task ReadFrameAsync_SkipsGarbageBeforeStartMarker()
    {
        // Includes a lone 0x53 that is NOT followed by 0x4C
        FrameReader reader = ReaderFor([0x00, 0xFF, 0x53, 0x00, .. ValidFrame]);

        Frame? frame = await reader.ReadFrameAsync();

        Assert.Equal(Command.SetGain, frame?.Command);
    }

    [Fact]
    public async Task ReadFrameAsync_FindsMarkerRightAfterFalseStart()
    {
        // 53 53 4C ...: the first 53 is garbage, the second starts the frame
        FrameReader reader = ReaderFor([0x53, .. ValidFrame]);

        Frame? frame = await reader.ReadFrameAsync();

        Assert.Equal(Command.SetGain, frame?.Command);
    }

    [Fact]
    public async Task ReadFrameAsync_WorksWhenBytesArriveOneAtATime()
    {
        var reader = new FrameReader(new OneByteAtATimeStream(ValidFrame));

        Frame? frame = await reader.ReadFrameAsync();

        Assert.Equal(Command.SetGain, frame?.Command);
    }

    [Fact]
    public async Task ReadFrameAsync_ThrowsOnWrongCrc()
    {
        FrameReader reader = ReaderFor([.. ValidFrame[..^1], 0x00]);

        await Assert.ThrowsAsync<InvalidFrameException>(() => reader.ReadFrameAsync());
    }

    [Fact]
    public async Task ReadFrameAsync_ThrowsWhenStreamEndsMidFrame()
    {
        FrameReader reader = ReaderFor(ValidFrame[..^3]);

        await Assert.ThrowsAsync<EndOfStreamException>(() => reader.ReadFrameAsync());
    }

    // Simulates a slow network: every read returns at most 1 byte.
    private sealed class OneByteAtATimeStream(byte[] data) : MemoryStream(data)
    {
        public override int Read(byte[] buffer, int offset, int count)
        {
            return base.Read(buffer, offset, Math.Min(count, 1));
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return base.ReadAsync(buffer[..Math.Min(buffer.Length, 1)], cancellationToken);
        }
    }
}
