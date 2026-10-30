namespace StageLink.Simulator;

// What the speaker remembers about one connection.
// The Speaker is shared by every connected app; a Session belongs to just one.
public class Session
{
    // Null while this connection is not subscribed to telemetry.
    public TimeSpan? TelemetryInterval { get; private set; }

    // Telemetry frames reuse the message number of the Subscribe request.
    public ushort TelemetryMessageNumber { get; private set; }

    public void Subscribe(TimeSpan interval, ushort messageNumber)
    {
        TelemetryInterval = interval;
        TelemetryMessageNumber = messageNumber;
    }

    public void Unsubscribe()
    {
        TelemetryInterval = null;
    }
}
