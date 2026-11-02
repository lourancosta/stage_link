using Microsoft.AspNetCore.SignalR;
using StageLink.Web.Discovery;

namespace StageLink.Web.Realtime;

// The browser's live connection (/hubs/speakers).
// Watching a speaker just joins its group; the backend's SpeakerMonitor already
// follows every speaker, and LiveUpdatesBroadcaster sends its readings and events
// to the group. SignalR removes a closed connection from its groups by itself.
public sealed class SpeakerHub(SpeakerRegistry registry) : Hub<ISpeakerHubClient>
{
    // Send the current list right away, so the page doesn't wait for the next change.
    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SpeakersChanged(registry.GetAll());
        await base.OnConnectedAsync();
    }

    public async Task WatchSpeaker(string speakerId)
    {
        if (registry.Find(speakerId) is null)
        {
            // Only HubException messages reach the browser; other exceptions become a
            // generic error so internal details never leak.
            throw new HubException($"No speaker with id '{speakerId}' has been discovered.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, LiveUpdatesBroadcaster.GroupName(speakerId));
    }

    public async Task UnwatchSpeaker(string speakerId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, LiveUpdatesBroadcaster.GroupName(speakerId));
    }
}
