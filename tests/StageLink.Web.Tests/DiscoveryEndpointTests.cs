using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using Microsoft.AspNetCore.Mvc.Testing;

namespace StageLink.Web.Tests;

// Integration test: real UDP datagrams in, JSON out of GET /api/speakers.
public class DiscoveryEndpointTests : IDisposable
{
    private readonly int _discoveryPort = TestApp.FreeUdpPort();
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public DiscoveryEndpointTests()
    {
        _factory = TestApp.WithDiscoveryPort(_discoveryPort);
        _client = _factory.CreateClient(); // starts the app, including the discovery service
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private sealed record SpeakerDto(string Id, string Name, string Model, string Firmware, string Address, int TcpPort);

    private async Task SendDatagramAsync(byte[] datagram)
    {
        using var udp = new UdpClient();
        await udp.SendAsync(datagram, new IPEndPoint(IPAddress.Loopback, _discoveryPort));
    }

    // Discovery happens in the background, so poll until the speaker shows up.
    private async Task<List<SpeakerDto>> WaitForSpeakersAsync(int count)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (true)
        {
            List<SpeakerDto>? speakers = await _client.GetFromJsonAsync<List<SpeakerDto>>("/api/speakers", timeout.Token);
            if (speakers is not null && speakers.Count >= count)
            {
                return speakers;
            }

            await Task.Delay(50, timeout.Token);
        }
    }

    [Fact]
    public async Task GetSpeakers_IsEmptyAtStart()
    {
        List<SpeakerDto>? speakers = await _client.GetFromJsonAsync<List<SpeakerDto>>("/api/speakers");

        Assert.NotNull(speakers);
        Assert.Empty(speakers);
    }

    [Fact]
    public async Task Announce_MakesSpeakerAppear()
    {
        await SendDatagramAsync(TestAnnouncements.Announce("SL-51000", "Main L", tcpPort: 51000).Encode());

        List<SpeakerDto> speakers = await WaitForSpeakersAsync(1);

        SpeakerDto speaker = Assert.Single(speakers);
        Assert.Equal("SL-51000", speaker.Id);
        Assert.Equal("Main L", speaker.Name);
        Assert.Equal("127.0.0.1", speaker.Address);
        Assert.Equal(51000, speaker.TcpPort);
    }

    [Fact]
    public async Task GarbageDatagrams_AreIgnored()
    {
        await SendDatagramAsync([0x00, 0x01, 0x02]);                                    // not a frame
        await SendDatagramAsync(new StageLink.Protocol.Frame(StageLink.Protocol.Command.Ping, 1, []).Encode()); // not an Announce
        await SendDatagramAsync(TestAnnouncements.Announce("SL-1", "Main L").Encode());

        List<SpeakerDto> speakers = await WaitForSpeakersAsync(1);

        Assert.Equal("SL-1", Assert.Single(speakers).Id);
    }
}
