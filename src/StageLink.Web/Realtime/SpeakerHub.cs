using Microsoft.AspNetCore.SignalR;
using StageLink.Web.Discovery;
using StageLink.Web.Speakers;

namespace StageLink.Web.Realtime;

// The browser's live connection (/hubs/speakers).
// A new hub object is created for every call, so it must not keep state itself;
// shared state lives in TelemetryForwarder and SpeakerRegistry.
public sealed class SpeakerHub(SpeakerRegistry registry, TelemetryForwarder forwarder) : Hub<ISpeakerHubClient>
{
    // Send the current list right away, so the page doesn't wait for the next change.
    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SpeakersChanged(registry.GetAll());
        await base.OnConnectedAsync();
    }

    public async Task WatchSpeaker(string speakerId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, TelemetryForwarder.GroupName(speakerId));

        try
        {
            await forwarder.WatchAsync(Context.ConnectionId, speakerId, Context.ConnectionAborted);
        }
        catch (Exception ex) when (ex is SpeakerNotFoundException or SpeakerUnavailableException or SpeakerCommandException or TimeoutException or IOException)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, TelemetryForwarder.GroupName(speakerId));

            // Only HubException messages reach the browser; other exceptions become a
            // generic error so internal details never leak.
            throw new HubException(ex.Message);
        }
    }

    public async Task UnwatchSpeaker(string speakerId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, TelemetryForwarder.GroupName(speakerId));
        await forwarder.UnwatchAsync(Context.ConnectionId, speakerId);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await forwarder.RemoveConnectionAsync(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
