using Microsoft.Extensions.Options;
using StageLink.Web.Monitoring;

namespace StageLink.Web.Tests.Monitoring;

public class SpeakerEventLogTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 31, 20, 0, 0, TimeSpan.Zero);

    private readonly SpeakerEventLog _log = new(Options.Create(new MonitoringOptions { MaxEventsPerSpeaker = 3 }));

    private static SpeakerEvent Event(int second, string message)
    {
        return new SpeakerEvent(T0.AddSeconds(second), EventSeverity.Info, message);
    }

    [Fact]
    public void Get_UnknownSpeaker_IsEmpty()
    {
        Assert.Empty(_log.Get("SL-NOPE"));
    }

    [Fact]
    public void Get_ReturnsNewestFirst()
    {
        _log.Add("SL-1", Event(1, "first"));
        _log.Add("SL-1", Event(2, "second"));

        Assert.Equal(["second", "first"], _log.Get("SL-1").Select(e => e.Message));
    }

    [Fact]
    public void Add_KeepsOnlyTheMostRecentEvents()
    {
        for (int i = 1; i <= 5; i++)
        {
            _log.Add("SL-1", Event(i, $"event {i}"));
        }

        Assert.Equal(["event 5", "event 4", "event 3"], _log.Get("SL-1").Select(e => e.Message));
    }

    [Fact]
    public void Speakers_HaveSeparateLogs()
    {
        _log.Add("SL-1", Event(1, "one"));
        _log.Add("SL-2", Event(1, "two"));

        Assert.Equal("one", Assert.Single(_log.Get("SL-1")).Message);
    }
}
