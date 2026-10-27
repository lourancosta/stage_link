using StageLink.Protocol;

namespace StageLink.Protocol.Tests;

public class CommandTests
{
    [Theory]
    [InlineData(Command.SetGain, 0x01)]
    [InlineData(Command.GetGain, 0x02)]
    [InlineData(Command.SetMute, 0x03)]
    public void Command_HasExpectedWireCode(Command command, int expected)
    {
        Assert.Equal(expected, (byte)command);
    }
}