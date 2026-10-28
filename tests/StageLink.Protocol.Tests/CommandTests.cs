using StageLink.Protocol;

namespace StageLink.Protocol.Tests;

public class CommandTests
{
    [Theory]
    [InlineData(Command.Announce, 0x01)]
    [InlineData(Command.Ping, 0x02)]
    [InlineData(Command.Pong, 0x03)]
    [InlineData(Command.GetState, 0x10)]
    [InlineData(Command.State, 0x11)]
    [InlineData(Command.SetGain, 0x20)]
    [InlineData(Command.SetMute, 0x21)]
    [InlineData(Command.SetTilt, 0x22)]
    [InlineData(Command.Subscribe, 0x30)]
    [InlineData(Command.Unsubscribe, 0x31)]
    [InlineData(Command.Telemetry, 0x32)]
    [InlineData(Command.Ack, 0x7E)]
    [InlineData(Command.Nack, 0x7F)]
    public void Command_HasExpectedWireCode(Command command, int expected)
    {
        Assert.Equal(expected, (byte)command);
    }
}