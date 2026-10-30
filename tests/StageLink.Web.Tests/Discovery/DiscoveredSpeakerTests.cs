using System.Net;
using StageLink.Protocol;
using StageLink.Web.Discovery;

namespace StageLink.Web.Tests.Discovery;

public class DiscoveredSpeakerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FromAnnounce_ReadsAllFields()
    {
        Frame announce = TestAnnouncements.Announce("SL-51000", "Main L", tcpPort: 51000);

        DiscoveredSpeaker speaker = DiscoveredSpeaker.FromAnnounce(announce, IPAddress.Parse("192.168.1.64"), Now);

        Assert.Equal("SL-51000", speaker.Id);
        Assert.Equal("Main L", speaker.Name);
        Assert.Equal("StageLink Sim", speaker.Model);
        Assert.Equal("1.0.0", speaker.Firmware);
        Assert.Equal("192.168.1.64", speaker.Address);
        Assert.Equal(51000, speaker.TcpPort);
        Assert.Equal(Now, speaker.LastSeen);
    }

    [Fact]
    public void FromAnnounce_ThrowsWhenNotAnAnnounce()
    {
        var ping = new Frame(Command.Ping, 1, []);

        Assert.Throws<ArgumentException>(() => DiscoveredSpeaker.FromAnnounce(ping, IPAddress.Loopback, Now));
    }

    [Fact]
    public void FromAnnounce_ThrowsOnTruncatedPayload()
    {
        // Only the id, nothing after it
        var payload = new ByteWriter();
        payload.WriteString("SL-1");
        var announce = new Frame(Command.Announce, 1, payload.ToArray());

        Assert.Throws<EndOfStreamException>(() => DiscoveredSpeaker.FromAnnounce(announce, IPAddress.Loopback, Now));
    }
}
