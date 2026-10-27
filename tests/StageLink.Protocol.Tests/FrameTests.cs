using StageLink.Protocol;

namespace StageLink.Protocol.Tests;

public class FrameTests
{
    [Fact]
    public void Encode_BuildsFullFrame()
    {
        // Arrange: SetGain, channel 1, gain 12.5 (04 E2)
        var frame = new Frame(Command.SetGain, [0x01, 0x04, 0xE2]);

        // Act
        byte[] bytes = frame.Encode();

        // Assert
        Assert.Equal([0xAA, 0x01, 0x03, 0x01, 0x04, 0xE2, 0xE5], bytes);
    }

    [Fact]
    public void Constructor_ThrowsWhenPayloadTooLong()
    {
        Assert.Throws<ArgumentException>(() => new Frame(Command.SetGain, new byte[256]));
    }

    [Fact]
    public void Decode_ReadsValidFrame()
    {
        // Act
        Frame frame = Frame.Decode([0xAA, 0x01, 0x03, 0x01, 0x04, 0xE2, 0xE5]);

        // Assert
        Assert.Equal(Command.SetGain, frame.Command);
        Assert.Equal([0x01, 0x04, 0xE2], frame.Payload);
    }

    [Fact]
    public void Decode_ThrowsOnWrongHeader()
    {
        Assert.Throws<InvalidFrameException>(
            () => Frame.Decode([0xBB, 0x01, 0x03, 0x01, 0x04, 0xE2, 0xE5]));
    }

    [Fact]
    public void Decode_ThrowsOnWrongChecksum()
    {
        Assert.Throws<InvalidFrameException>(
            () => Frame.Decode([0xAA, 0x01, 0x03, 0x01, 0x04, 0xE2, 0x00]));
    }

    [Fact]
    public void Decode_ThrowsOnUnknownCommand()
    {
        // Command 0x09 does not exist; checksum 09 ˆ 00 = 09 is correct
        Assert.Throws<InvalidFrameException>(
            () => Frame.Decode([0xAA, 0x09, 0x00, 0x09]));
    }
}