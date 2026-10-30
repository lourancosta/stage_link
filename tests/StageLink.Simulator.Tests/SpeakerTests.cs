using StageLink.Protocol;
using StageLink.Simulator;

namespace StageLink.Simulator.Tests;

public class SpeakerTests
{
    private readonly SpeakerState _state = new("Main L");
    private readonly Session _session = new();
    private readonly Speaker _speaker;

    public SpeakerTests()
    {
        _speaker = new Speaker(_state);
    }

    private Frame Send(Frame request)
    {
        return _speaker.Handle(request, _session);
    }

    private static byte[] IntervalPayload(ushort milliseconds)
    {
        var writer = new ByteWriter();
        writer.WriteUInt16(milliseconds);
        return writer.ToArray();
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
        Frame reply = Send(request);

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
        Frame reply = Send(request);

        // Assert
        Assert.Equal(Command.Nack, reply.Command);
        Assert.Equal(7, reply.MessageNumber);
        Assert.Equal([(byte)NackCode.UnsupportedCommand], reply.Payload);
    }

    // SetGain

    [Fact]
    public void Handle_SetGain_UpdatesStateAndAcks()
    {
        Frame reply = Send(new Frame(Command.SetGain, 10, FixedPointPayload(-6.5)));

        AssertAck(reply, 10);
        Assert.Equal(-6.5, _state.Gain);
    }

    [Fact]
    public void Handle_SetGain_OutOfRange_NacksAndKeepsState()
    {
        Frame reply = Send(new Frame(Command.SetGain, 11, FixedPointPayload(20.0)));

        AssertNack(reply, 11, NackCode.ValueOutOfRange);
        Assert.Equal(0.0, _state.Gain);
    }

    [Fact]
    public void Handle_SetGain_WrongPayloadLength_Nacks()
    {
        Frame reply = Send(new Frame(Command.SetGain, 12, [0x01]));

        AssertNack(reply, 12, NackCode.InvalidPayload);
    }

    // SetMute

    [Fact]
    public void Handle_SetMute_On_UpdatesStateAndAcks()
    {
        Frame reply = Send(new Frame(Command.SetMute, 20, [0x01]));

        AssertAck(reply, 20);
        Assert.True(_state.Muted);
    }

    [Fact]
    public void Handle_SetMute_Off_UpdatesStateAndAcks()
    {
        _state.SetMute(true);

        Frame reply = Send(new Frame(Command.SetMute, 21, [0x00]));

        AssertAck(reply, 21);
        Assert.False(_state.Muted);
    }

    [Theory]
    [InlineData(new byte[] { })]
    [InlineData(new byte[] { 0x02 })]
    [InlineData(new byte[] { 0x01, 0x00 })]
    public void Handle_SetMute_InvalidPayload_Nacks(byte[] payload)
    {
        Frame reply = Send(new Frame(Command.SetMute, 22, payload));

        AssertNack(reply, 22, NackCode.InvalidPayload);
        Assert.False(_state.Muted);
    }

    // SetTilt

    [Fact]
    public void Handle_SetTilt_UpdatesStateAndAcks()
    {
        Frame reply = Send(new Frame(Command.SetTilt, 30, FixedPointPayload(7.5)));

        AssertAck(reply, 30);
        Assert.Equal(7.5, _state.Tilt);
    }

    [Fact]
    public void Handle_SetTilt_OutOfRange_NacksAndKeepsState()
    {
        Frame reply = Send(new Frame(Command.SetTilt, 31, FixedPointPayload(-20.0)));

        AssertNack(reply, 31, NackCode.ValueOutOfRange);
        Assert.Equal(0.0, _state.Tilt);
    }

    [Fact]
    public void Handle_SetTilt_WrongPayloadLength_Nacks()
    {
        Frame reply = Send(new Frame(Command.SetTilt, 32, [0x00, 0x00, 0x00]));

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
        Frame reply = Send(new Frame(Command.GetState, 40, []));

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
        Frame reply = Send(new Frame(Command.GetState, 41, [0x00]));

        AssertNack(reply, 41, NackCode.InvalidPayload);
    }

    // Subscribe / Unsubscribe

    [Fact]
    public void Handle_Subscribe_AcksAndStartsSubscription()
    {
        Frame reply = Send(new Frame(Command.Subscribe, 50, IntervalPayload(500)));

        AssertAck(reply, 50);
        Assert.Equal(TimeSpan.FromMilliseconds(500), _session.TelemetryInterval);
        Assert.Equal(50, _session.TelemetryMessageNumber);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(10000)]
    public void Handle_Subscribe_AcceptsLimits(ushort milliseconds)
    {
        Frame reply = Send(new Frame(Command.Subscribe, 51, IntervalPayload(milliseconds)));

        AssertAck(reply, 51);
        Assert.Equal(TimeSpan.FromMilliseconds(milliseconds), _session.TelemetryInterval);
    }

    [Theory]
    [InlineData(99)]
    [InlineData(10001)]
    public void Handle_Subscribe_OutOfRange_Nacks(ushort milliseconds)
    {
        Frame reply = Send(new Frame(Command.Subscribe, 52, IntervalPayload(milliseconds)));

        AssertNack(reply, 52, NackCode.ValueOutOfRange);
        Assert.Null(_session.TelemetryInterval);
    }

    [Fact]
    public void Handle_Subscribe_WrongPayloadLength_Nacks()
    {
        Frame reply = Send(new Frame(Command.Subscribe, 53, [0x01]));

        AssertNack(reply, 53, NackCode.InvalidPayload);
    }

    [Fact]
    public void Handle_Unsubscribe_AcksAndStopsSubscription()
    {
        Send(new Frame(Command.Subscribe, 54, IntervalPayload(500)));

        Frame reply = Send(new Frame(Command.Unsubscribe, 55, []));

        AssertAck(reply, 55);
        Assert.Null(_session.TelemetryInterval);
    }

    [Fact]
    public void Handle_Unsubscribe_WithPayload_Nacks()
    {
        Frame reply = Send(new Frame(Command.Unsubscribe, 56, [0x00]));

        AssertNack(reply, 56, NackCode.InvalidPayload);
    }

    // Telemetry

    [Fact]
    public void BuildTelemetry_ContainsCurrentValues()
    {
        // Arrange
        _state.SetGain(-6.5);
        _state.SetMute(true);
        _state.SetTilt(7.5);
        _state.UpdateMeasurements(41.5, -12.25, true, Faults.Overtemperature);

        // Act
        Frame telemetry = _speaker.BuildTelemetry(60);

        // Assert
        Assert.Equal(Command.Telemetry, telemetry.Command);
        Assert.Equal(60, telemetry.MessageNumber);
        Assert.Equal(
            [
                0x10, 0x36, // temperature: 41.5 °C (4150)
                0xFB, 0x37, // level: -12.25 dB (-1225)
                0x01,       // limiter: active
                0xFD, 0x76, // gain: -6.5 dB
                0x01,       // mute: on
                0x02, 0xEE, // tilt: 7.5 degrees
                0x01,       // faults: overtemperature
            ],
            telemetry.Payload);
    }
}
