using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using StageLink.Web.Discovery;

namespace StageLink.Web.Realtime;

// Checks the speaker list regularly and pushes it to every browser when it changed.
// Checking on a timer also catches speakers that expired (went silent).
public sealed class SpeakerListBroadcaster(
    SpeakerRegistry registry,
    IHubContext<SpeakerHub, ISpeakerHubClient> hub,
    IOptions<DiscoveryOptions> options) : BackgroundService
{
    private readonly TimeSpan _interval = options.Value.ListCheckInterval;

    // Changed means a speaker was added, removed, or its details changed.
    // A newer LastSeen alone doesn't count: that happens every 2 seconds.
    public static bool HasChanged(IReadOnlyList<DiscoveredSpeaker> previous, IReadOnlyList<DiscoveredSpeaker> current)
    {
        return !previous.Select(WithoutLastSeen).SequenceEqual(current.Select(WithoutLastSeen));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        IReadOnlyList<DiscoveredSpeaker> previous = [];
        using var timer = new PeriodicTimer(_interval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                IReadOnlyList<DiscoveredSpeaker> current = registry.GetAll();
                if (HasChanged(previous, current))
                {
                    await hub.Clients.All.SpeakersChanged(current);
                    previous = current;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
    }

    private static DiscoveredSpeaker WithoutLastSeen(DiscoveredSpeaker speaker)
    {
        return speaker with { LastSeen = default };
    }
}
