using StageLink.Web.Discovery;
using StageLink.Web.Realtime;

namespace StageLink.Web.Tests.Realtime;

public class SpeakerListBroadcasterTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);

    private static DiscoveredSpeaker Speaker(string id, string name = "Main L", DateTimeOffset? lastSeen = null)
    {
        return new DiscoveredSpeaker(id, name, "StageLink Sim", "1.0.0", "127.0.0.1", 51000, lastSeen ?? T0);
    }

    [Fact]
    public void HasChanged_OnlyLastSeenDiffers_IsFalse()
    {
        // Every announcement refreshes LastSeen; that alone isn't worth pushing.
        Assert.False(SpeakerListBroadcaster.HasChanged(
            [Speaker("SL-1", lastSeen: T0)],
            [Speaker("SL-1", lastSeen: T0.AddSeconds(2))]));
    }

    [Fact]
    public void HasChanged_SpeakerAdded_IsTrue()
    {
        Assert.True(SpeakerListBroadcaster.HasChanged([], [Speaker("SL-1")]));
    }

    [Fact]
    public void HasChanged_SpeakerRemoved_IsTrue()
    {
        Assert.True(SpeakerListBroadcaster.HasChanged([Speaker("SL-1")], []));
    }

    [Fact]
    public void HasChanged_SpeakerRenamed_IsTrue()
    {
        Assert.True(SpeakerListBroadcaster.HasChanged([Speaker("SL-1", "Main L")], [Speaker("SL-1", "Main Left")]));
    }
}
