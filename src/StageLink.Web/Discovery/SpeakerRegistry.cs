using Microsoft.Extensions.Options;

namespace StageLink.Web.Discovery;

// The live list of discovered speakers, one entry per id.
// Written by the discovery service and read by HTTP requests at the same
// time, so every access goes through a lock.
public class SpeakerRegistry
{
    private readonly TimeProvider _time;
    private readonly TimeSpan _timeout;
    private readonly Dictionary<string, DiscoveredSpeaker> _speakers = [];
    private readonly Lock _lock = new();

    public SpeakerRegistry(TimeProvider time, IOptions<DiscoveryOptions> options)
    {
        _time = time;
        _timeout = options.Value.SpeakerTimeout;
    }

    public void Update(DiscoveredSpeaker speaker)
    {
        lock (_lock)
        {
            // The same speaker is usually heard on more than one route (127.0.0.1 and
            // the LAN). Keep the first address while it's alive, so it doesn't flip
            // back and forth; take everything else (name, last seen) from the new one.
            if (_speakers.TryGetValue(speaker.Id, out DiscoveredSpeaker? existing) && !IsExpired(existing))
            {
                speaker = speaker with { Address = existing.Address };
            }

            _speakers[speaker.Id] = speaker;
        }
    }

    // Speakers heard recently, sorted by name. Silent ones are removed.
    public IReadOnlyList<DiscoveredSpeaker> GetAll()
    {
        lock (_lock)
        {
            foreach (DiscoveredSpeaker expired in _speakers.Values.Where(IsExpired).ToList())
            {
                _speakers.Remove(expired.Id);
            }

            return _speakers.Values
                .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(s => s.Id, StringComparer.Ordinal)
                .ToList();
        }
    }

    // A speaker heard recently, or null if it's unknown or has gone silent.
    public DiscoveredSpeaker? Find(string id)
    {
        lock (_lock)
        {
            return _speakers.TryGetValue(id, out DiscoveredSpeaker? speaker) && !IsExpired(speaker) ? speaker : null;
        }
    }

    private bool IsExpired(DiscoveredSpeaker speaker)
    {
        return _time.GetUtcNow() - speaker.LastSeen > _timeout;
    }
}
