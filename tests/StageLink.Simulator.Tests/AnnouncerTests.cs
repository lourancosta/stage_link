using System.Net;
using System.Net.Sockets;
using StageLink.Protocol;
using StageLink.Simulator;

namespace StageLink.Simulator.Tests;

// Integration test: real UDP datagrams on localhost.
public class AnnouncerTests
{
    [Fact]
    public async Task RunAsync_SendsAnnounceRepeatedly()
    {
        // Arrange: a UDP "app" listening on any free port
        using var receiver = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var destination = (IPEndPoint)receiver.Client.LocalEndPoint!;

        var speaker = new Speaker(new SpeakerState("Main L"), new SpeakerIdentity("SL-1", "Sim", "1.0"));
        var announcer = new Announcer(speaker, 51000, [destination], TimeSpan.FromMilliseconds(100));

        using var stop = new CancellationTokenSource();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        Task running = announcer.RunAsync(stop.Token);
        UdpReceiveResult first = await receiver.ReceiveAsync(timeout.Token);
        UdpReceiveResult second = await receiver.ReceiveAsync(timeout.Token);
        stop.Cancel();
        await running;

        // Assert: one datagram = one complete frame
        Frame a = Frame.Decode(first.Buffer);
        Frame b = Frame.Decode(second.Buffer);
        Assert.Equal(Command.Announce, a.Command);
        Assert.Equal(a.MessageNumber + 1, b.MessageNumber);

        var payload = new ByteReader(a.Payload);
        Assert.Equal("SL-1", payload.ReadString());
        Assert.Equal("Main L", payload.ReadString());
        Assert.Equal("Sim", payload.ReadString());
        Assert.Equal("1.0", payload.ReadString());
        Assert.Equal(51000, payload.ReadUInt16());
    }

    [Theory]
    [InlineData("192.168.1.42", "255.255.255.0", "192.168.1.255")]
    [InlineData("10.0.5.9", "255.255.0.0", "10.0.255.255")]
    [InlineData("172.16.20.7", "255.255.240.0", "172.16.31.255")]
    public void BroadcastAddress_SetsAllHostBitsToOne(string ip, string mask, string expected)
    {
        IPAddress broadcast = Announcer.BroadcastAddress(IPAddress.Parse(ip), IPAddress.Parse(mask));

        Assert.Equal(IPAddress.Parse(expected), broadcast);
    }
}
