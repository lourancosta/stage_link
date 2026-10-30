namespace StageLink.Web.Discovery;

// Settings for discovery, read from the "Discovery" section of appsettings.json.
public class DiscoveryOptions
{
    public int Port { get; set; } = 51001;

    // A speaker announces every 2 seconds; after 3 missed announcements it is gone.
    public TimeSpan SpeakerTimeout { get; set; } = TimeSpan.FromSeconds(6);

    // How often to check whether the speaker list changed (and push it to browsers).
    public TimeSpan ListCheckInterval { get; set; } = TimeSpan.FromSeconds(1);
}
