using System.Globalization;
using StageLink.Protocol;
using StageLink.Web.Speakers;

namespace StageLink.Web.Monitoring;

// Turns two consecutive Telemetry readings into the events that happened between them.
// Pure logic (no state, no I/O), so every rule is easy to unit-test.
public static class EventDetector
{
    private static readonly (Faults Fault, string Label)[] FaultLabels =
    [
        (Faults.Overtemperature, "Overtemperature"),
        (Faults.AmplifierFault, "Amplifier fault"),
        (Faults.PowerSupplyFault, "Power supply fault"),
    ];

    public static IReadOnlyList<SpeakerEvent> Compare(TelemetryReading previous, TelemetryReading current, DateTimeOffset time)
    {
        var events = new List<SpeakerEvent>();

        void Add(EventSeverity severity, string message) => events.Add(new SpeakerEvent(time, severity, message));

        // Settings first: a change often causes the next ones in the same reading
        // (raising the gain engages the limiter), so the log reads cause, then effect.
        if (current.Gain != previous.Gain)
        {
            Add(EventSeverity.Info, $"Gain changed to {Signed(current.Gain)} dB");
        }

        if (current.Muted != previous.Muted)
        {
            Add(EventSeverity.Info, current.Muted ? "Muted" : "Unmuted");
        }

        if (current.Tilt != previous.Tilt)
        {
            Add(EventSeverity.Info, $"Tilt changed to {Signed(current.Tilt)}°");
        }

        if (!previous.LimiterActive && current.LimiterActive)
        {
            Add(EventSeverity.Warning, "Limiter engaged");
        }
        else if (previous.LimiterActive && !current.LimiterActive)
        {
            Add(EventSeverity.Info, "Limiter released");
        }

        foreach ((Faults fault, string label) in FaultLabels)
        {
            bool was = previous.Faults.HasFlag(fault);
            bool now = current.Faults.HasFlag(fault);
            if (!was && now)
            {
                Add(EventSeverity.Error, $"{label} detected");
            }
            else if (was && !now)
            {
                Add(EventSeverity.Info, $"{label} cleared");
            }
        }

        return events;
    }

    // "+12.0", "-6.0", "0.0". Invariant culture: always a dot, whatever the server's language.
    private static string Signed(double value)
    {
        return value.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture);
    }
}
