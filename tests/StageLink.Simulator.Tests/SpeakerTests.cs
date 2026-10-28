using StageLink.Protocol;
using StageLink.Simulator;

namespace StageLink.Simulator.Tests;

public class SpeakerTests
{
    private readonly Speaker _speaker = new(new SpeakerState("Main L"));

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
}