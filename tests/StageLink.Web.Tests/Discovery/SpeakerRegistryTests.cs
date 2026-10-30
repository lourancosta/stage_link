using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using StageLink.Web.Discovery;

namespace StageLink.Web.Tests.Discovery;

public class SpeakerRegistryTests
{
    // Fake clock: tests move time forward instantly instead of waiting.
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 30, 12, 0, 0, TimeSpan.Zero));
    private readonly SpeakerRegistry _registry;

    public SpeakerRegistryTests()
    {
        var options = Options.Create(new DiscoveryOptions { SpeakerTimeout = TimeSpan.FromSeconds(6) });
        _registry = new SpeakerRegistry(_time, options);
    }

    private DiscoveredSpeaker Speaker(string id, string name = "Main L", string address = "127.0.0.1")
    {
        return new DiscoveredSpeaker(id, name, "StageLink Sim", "1.0.0", address, 51000, _time.GetUtcNow());
    }

    [Fact]
    public void Update_AddsNewSpeaker()
    {
        _registry.Update(Speaker("SL-1"));

        DiscoveredSpeaker speaker = Assert.Single(_registry.GetAll());
        Assert.Equal("SL-1", speaker.Id);
    }

    [Fact]
    public void Update_SameIdTwice_KeepsOneEntry()
    {
        // The same speaker is heard via 127.0.0.1 and via the LAN broadcast.
        _registry.Update(Speaker("SL-1", address: "127.0.0.1"));
        _registry.Update(Speaker("SL-1", address: "192.168.1.64"));

        Assert.Single(_registry.GetAll());
    }

    [Fact]
    public void Update_SameIdFromAnotherAddress_KeepsFirstAddress()
    {
        _registry.Update(Speaker("SL-1", address: "127.0.0.1"));
        _registry.Update(Speaker("SL-1", address: "192.168.1.64"));

        Assert.Equal("127.0.0.1", _registry.GetAll()[0].Address);
    }

    [Fact]
    public void Update_SameId_TakesNewName()
    {
        _registry.Update(Speaker("SL-1", name: "Main L"));
        _registry.Update(Speaker("SL-1", name: "Main Left"));

        Assert.Equal("Main Left", _registry.GetAll()[0].Name);
    }

    [Fact]
    public void GetAll_HidesSpeakerSilentLongerThanTimeout()
    {
        _registry.Update(Speaker("SL-1"));

        _time.Advance(TimeSpan.FromSeconds(6.1));

        Assert.Empty(_registry.GetAll());
    }

    [Fact]
    public void GetAll_KeepsSpeakerThatKeepsAnnouncing()
    {
        _registry.Update(Speaker("SL-1"));
        _time.Advance(TimeSpan.FromSeconds(4));
        _registry.Update(Speaker("SL-1"));
        _time.Advance(TimeSpan.FromSeconds(4));

        Assert.Single(_registry.GetAll());
    }

    [Fact]
    public void Update_AfterExpiry_TakesNewAddress()
    {
        _registry.Update(Speaker("SL-1", address: "127.0.0.1"));
        _time.Advance(TimeSpan.FromSeconds(7));
        _registry.Update(Speaker("SL-1", address: "192.168.1.64"));

        Assert.Equal("192.168.1.64", _registry.GetAll()[0].Address);
    }

    [Fact]
    public void GetAll_SortsByName()
    {
        _registry.Update(Speaker("SL-2", name: "Sub"));
        _registry.Update(Speaker("SL-1", name: "Main L"));
        _registry.Update(Speaker("SL-3", name: "Main R"));

        Assert.Equal(["Main L", "Main R", "Sub"], _registry.GetAll().Select(s => s.Name));
    }
}
