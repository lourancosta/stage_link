using StageLink.Protocol;
using StageLink.Simulator;

namespace StageLink.Simulator.Tests;

public class SpeakerTests
{
    private readonly SpeakerState _state = new("Main L");
    private readonly Speaker _speaker;

    public SpeakerTests()
    {
        _speaker = new Speaker(_state);
    }

    private static byte[] FixedPointPayload(double value)
    {
        var writer = new ByteWriter();
        writer.WriteFixedPoint(value);
        return writer.ToArray();
    }

    private static void AssertAck(Frame reply, ushort messageNumber)
    {
        Assert.Equal(Command.Ack, reply.Command);
        Assert.Equal(messageNumber, reply.MessageNumber);
        Assert.Empty(reply.Payload);
    }

    private static void AssertNack(Frame reply, ushort messageNumber, NackCode code)
    {
        Assert.Equal(Command.Nack, reply.Command);
        Assert.Equal(messageNumber, reply.MessageNumber);
        Assert.Equal([(byte)code], reply.Payload);
    }

    [Fact]
    public void Handle_Ping_RepliesPongWithSameMessageNumber()
    {
        // Arrange
        var request = new Frame(Command.Ping, 42, []);

        // Act
        Frame reply = _speaker.Handle(request);

        // Assert
        Assert.Equal(Command.Pong, reply.Command);
        Assert.Equal(42, reply.MessageNumber);
        Assert.Empty(reply.Payload);
    }

    [Theory]
    [InlineData(Command.Announce)]
    [InlineData(Command.Pong)]
    [InlineData(Command.State)]
    [InlineData(Command.Telemetry)]
    [InlineData(Command.Ack)]
    [InlineData(Command.Nack)]
    public void Handle_UnsupportedCommand_RepliesNack(Command command)
    {
        // Arrange
        var request = new Frame(command, 7, []);

        // Act
        Frame reply = _speaker.Handle(request);

        // Assert
        Assert.Equal(Command.Nack, reply.Command);
        Assert.Equal(7, reply.MessageNumber);
        Assert.Equal([(byte)NackCode.UnsupportedCommand], reply.Payload);
    }

    // SetGain

    [Fact]
    public void Handle_SetGain_UpdatesStateAndAcks()
    {
        Frame reply = _speaker.Handle(new Frame(Command.SetGain, 10, FixedPointPayload(-6.5)));

        AssertAck(reply, 10);
        Assert.Equal(-6.5, _state.Gain);
    }

    [Fact]
    public void Handle_SetGain_OutOfRange_NacksAndKeepsState()
    {
        Frame reply = _speaker.Handle(new Frame(Command.SetGain, 11, FixedPointPayload(20.0)));

        AssertNack(reply, 11, NackCode.ValueOutOfRange);
        Assert.Equal(0.0, _state.Gain);
    }

    [Fact]
    public void Handle_SetGain_WrongPayloadLength_Nacks()
    {
        Frame reply = _speaker.Handle(new Frame(Command.SetGain, 12, [0x01]));

        AssertNack(reply, 12, NackCode.InvalidPayload);
    }

    // SetMute

    [Fact]
    public void Handle_SetMute_On_UpdatesStateAndAcks()
    {
        Frame reply = _speaker.Handle(new Frame(Command.SetMute, 20, [0x01]));

        AssertAck(reply, 20);
        Assert.True(_state.Muted);
    }

    [Fact]
    public void Handle_SetMute_Off_UpdatesStateAndAcks()
    {
        _state.SetMute(true);

        Frame reply = _speaker.Handle(new Frame(Command.SetMute, 21, [0x00]));

        AssertAck(reply, 21);
        Assert.False(_state.Muted);
    }

    [Theory]
    [InlineData(new byte[] { })]
    [InlineData(new byte[] { 0x02 })]
    [InlineData(new byte[] { 0x01, 0x00 })]
    public void Handle_SetMute_InvalidPayload_Nacks(byte[] payload)
    {
        Frame reply = _speaker.Handle(new Frame(Command.SetMute, 22, payload));

        AssertNack(reply, 22, NackCode.InvalidPayload);
        Assert.False(_state.Muted);
    }

    // SetTilt

    [Fact]
    public void Handle_SetTilt_UpdatesStateAndAcks()
    {
        Frame reply = _speaker.Handle(new Frame(Command.SetTilt, 30, FixedPointPayload(7.5)));

        AssertAck(reply, 30);
        Assert.Equal(7.5, _state.Tilt);
    }

    [Fact]
    public void Handle_SetTilt_OutOfRange_NacksAndKeepsState()
    {
        Frame reply = _speaker.Handle(new Frame(Command.SetTilt, 31, FixedPointPayload(-20.0)));

        AssertNack(reply, 31, NackCode.ValueOutOfRange);
        Assert.Equal(0.0, _state.Tilt);
    }

    [Fact]
    public void Handle_SetTilt_WrongPayloadLength_Nacks()
    {
        Frame reply = _speaker.Handle(new Frame(Command.SetTilt, 32, [0x00, 0x00, 0x00]));

        AssertNack(reply, 32, NackCode.InvalidPayload);
    }

    // GetState

    [Fact]
    public void Handle_GetState_RepliesWithCurrentState()
    {
        // Arrange
        _state.SetGain(-6.5);
        _state.SetMute(true);
        _state.SetTilt(7.5);

        // Act
        Frame reply = _speaker.Handle(new Frame(Command.GetState, 40, []));

        // Assert
        Assert.Equal(Command.State, reply.Command);
        Assert.Equal(40, reply.MessageNumber);
        Assert.Equal(
            [
                0x06, 0x4D, 0x61, 0x69, 0x6E, 0x20, 0x4C, // name: 6 characters, "Main L"
                0xFD, 0x76,                               // gain: -6.5 dB  (-650)
                0x01,                                     // mute: on
                0x02, 0xEE,                               // tilt: 7.5 degrees (750)
            ],
            reply.Payload);
    }

    [Fact]
    public void Handle_GetState_WithPayload_Nacks()
    {
        Frame reply = _speaker.Handle(new Frame(Command.GetState, 41, [0x00]));

        AssertNack(reply, 41, NackCode.InvalidPayload);
    }
}
