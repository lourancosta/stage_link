using StageLink.Protocol;

namespace StageLink.Protocol.Tests;

public class FrameTests
{
    // SetGain -6.5 dB, message number 1 (the example in ROADMAP.md)
    private static readonly byte[] ValidFrame =
        [0x53, 0x4C, 0x01, 0x20, 0x00, 0x01, 0x00, 0x02, 0xFD, 0x76, 0xCA, 0x6B];

    [Fact]
    public void Encode_BuildsFullFrame()
    {
        // Arrange
        var payloadWriter = new ByteWriter();
        payloadWriter.WriteFixedPoint(-6.5);
        var frame = new Frame(Command.SetGain, 1, payloadWriter.ToArray());

        // Act
        byte[] bytes = frame.Encode();

        // Assert
        Assert.Equal(ValidFrame, bytes);
    }

    [Fact]
    public void Encode_WritesMessageNumberAndEmptyPayload()
    {
        var frame = new Frame(Command.Ping, 300, []);

        Assert.Equal([0x53, 0x4C, 0x01, 0x02, 0x01, 0x2C, 0x00, 0x00, 0x39, 0x98], frame.Encode());
    }

    [Fact]
    public void Constructor_ThrowsWhenPayloadTooLong()
    {
        Assert.Throws<ArgumentException>(() => new Frame(Command.SetGain, 1, new byte[65536]));
    }

    [Fact]
    public void Decode_ReadsValidFrame()
    {
        Frame frame = Frame.Decode(ValidFrame);

        Assert.Equal(Command.SetGain, frame.Command);
        Assert.Equal(1, frame.MessageNumber);
        Assert.Equal([0xFD, 0x76], frame.Payload);
    }

    [Fact]
    public void Decode_ThrowsOnWrongStartMarker()
    {
        // "SM" instead of "SL"; CRC recomputed so only the marker is wrong
        Assert.Throws<InvalidFrameException>(() => Frame.Decode(
            [0x53, 0x4D, 0x01, 0x20, 0x00, 0x01, 0x00, 0x02, 0xFD, 0x76, 0x21, 0x48]));
    }

    [Fact]
    public void Decode_ThrowsOnUnsupportedVersion()
    {
        // Version 2; CRC recomputed
        Assert.Throws<InvalidFrameException>(() => Frame.Decode(
            [0x53, 0x4C, 0x02, 0x20, 0x00, 0x01, 0x00, 0x02, 0xFD, 0x76, 0x02, 0x1E]));
    }

    [Fact]
    public void Decode_ThrowsOnUnknownCommand()
    {
        // Command 0x09 does not exist; CRC recomputed
        Assert.Throws<InvalidFrameException>(() => Frame.Decode(
            [0x53, 0x4C, 0x01, 0x09, 0x00, 0x01, 0x00, 0x00, 0x67, 0x44]));
    }

    [Fact]
    public void Decode_ThrowsOnWrongCrc()
    {
        // Last byte changed from 6B to 6C
        Assert.Throws<InvalidFrameException>(() => Frame.Decode(
            [0x53, 0x4C, 0x01, 0x20, 0x00, 0x01, 0x00, 0x02, 0xFD, 0x76, 0xCA, 0x6C]));
    }

    [Fact]
    public void Decode_ThrowsOnTruncatedFrame()
    {
        Assert.Throws<InvalidFrameException>(() => Frame.Decode(ValidFrame[..^1]));
    }

    [Fact]
    public void Payload_CannotBeChangedFromOutside()
    {
        var frame = new Frame(Command.SetGain, 1, [0xFD, 0x76]);

        frame.Payload[0] = 0xFF;

        Assert.Equal([0xFD, 0x76], frame.Payload);
    }

    [Fact]
    public void Constructor_CopiesPayload()
    {
        byte[] original = [0xFD, 0x76];
        var frame = new Frame(Command.SetGain, 1, original);

        original[0] = 0xFF;

        Assert.Equal([0xFD, 0x76], frame.Payload);
    }
}