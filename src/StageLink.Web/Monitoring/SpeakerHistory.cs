using Microsoft.Extensions.Options;
using StageLink.Web.Speakers;

namespace StageLink.Web.Monitoring;

public record TemperatureSample(DateTimeOffset Time, double Temperature, double Level);

// Recent temperature and level per speaker (by default 10 minutes, one sample per second).
// Written by the monitor and read by HTTP requests at the same time, so it uses a lock.
public class SpeakerHistory(IOptions<MonitoringOptions> options)
{
    private readonly TimeSpan _length = options.Value.HistoryLength;
    private readonly TimeSpan _sampleInterval = options.Value.HistorySampleInterval;
    private readonly Dictionary<string, Queue<TemperatureSample>> _samples = [];
    private readonly Lock _lock = new();

    public void Add(string speakerId, DateTimeOffset time, TelemetryReading reading)
    {
        lock (_lock)
        {
            if (!_samples.TryGetValue(speakerId, out Queue<TemperatureSample>? samples))
            {
                samples = new Queue<TemperatureSample>();
                _samples[speakerId] = samples;
            }

            // Telemetry arrives faster than we want to store it: keep one per interval.
            if (samples.Count > 0 && time - samples.Last().Time < _sampleInterval)
            {
                return;
            }

            samples.Enqueue(new TemperatureSample(time, reading.Temperature, reading.Level));

            // A queue makes dropping the oldest samples cheap.
            while (samples.Count > 0 && time - samples.Peek().Time > _length)
            {
                samples.Dequeue();
            }
        }
    }

    public IReadOnlyList<TemperatureSample> Get(string speakerId)
    {
        lock (_lock)
        {
            return _samples.TryGetValue(speakerId, out Queue<TemperatureSample>? samples) ? samples.ToList() : [];
        }
    }

    public bool Has(string speakerId)
    {
        lock (_lock)
        {
            return _samples.ContainsKey(speakerId);
        }
    }
}
