using StageLink.Web.Discovery;
using StageLink.Web.Monitoring;
using StageLink.Web.Speakers;

namespace StageLink.Web.Realtime;

// Messages the server pushes to browsers. A typed interface means a misspelled
// message name is a compile error instead of a message nobody receives.
public interface ISpeakerHubClient
{
    Task SpeakersChanged(IReadOnlyList<DiscoveredSpeaker> speakers);

    Task Telemetry(string speakerId, TelemetryReading reading);

    Task EventRecorded(string speakerId, SpeakerEvent speakerEvent);
}
