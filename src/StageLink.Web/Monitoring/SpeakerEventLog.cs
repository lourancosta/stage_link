using Microsoft.Extensions.Options;

namespace StageLink.Web.Monitoring;

// The most recent events per speaker, newest first. Thread-safe.
public class SpeakerEventLog(IOptions<MonitoringOptions> options)
{
    private readonly int _maxEvents = options.Value.MaxEventsPerSpeaker;
    private readonly Dictionary<string, LinkedList<SpeakerEvent>> _events = [];
    private readonly Lock _lock = new();

    public void Add(string speakerId, SpeakerEvent speakerEvent)
    {
        lock (_lock)
        {
            if (!_events.TryGetValue(speakerId, out LinkedList<SpeakerEvent>? events))
            {
                events = new LinkedList<SpeakerEvent>();
                _events[speakerId] = events;
            }

            events.AddFirst(speakerEvent);
            while (events.Count > _maxEvents)
            {
                events.RemoveLast();
            }
        }
    }

    public IReadOnlyList<SpeakerEvent> Get(string speakerId)
    {
        lock (_lock)
        {
            return _events.TryGetValue(speakerId, out LinkedList<SpeakerEvent>? events) ? events.ToList() : [];
        }
    }

    public bool Has(string speakerId)
    {
        lock (_lock)
        {
            return _events.ContainsKey(speakerId);
        }
    }
}
