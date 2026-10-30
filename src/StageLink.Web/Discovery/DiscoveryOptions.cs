namespace StageLink.Web.Discovery;

// Settings for discovery, read from the "Discovery" section of appsettings.json.
public class DiscoveryOptions
{
    public int Port { get; set; } = 51001;

    // A speaker announces every 2 seconds; after 3 missed announcements it is gone.
    public TimeSpan SpeakerTimeout { get; set; } = TimeSpan.FromSeconds(6);
}
