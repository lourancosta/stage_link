namespace StageLink.Web.Monitoring;

public enum EventSeverity
{
    Info,
    Warning,
    Error,
}

// Something that happened to a speaker, for its event log.
public record SpeakerEvent(DateTimeOffset Time, EventSeverity Severity, string Message);
