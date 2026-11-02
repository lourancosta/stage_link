using StageLink.Web.Discovery;
using StageLink.Web.Monitoring;
using StageLink.Web.Speakers;

namespace StageLink.Web.Api;

// HTTP endpoints for speakers. Errors are thrown as exceptions and turned into
// HTTP status codes in one place: SpeakerExceptionHandler.
public static class SpeakerEndpoints
{
    // 404 for a speaker that is neither on the network now nor has recorded data.
    private static void EnsureKnown(string id, SpeakerRegistry registry, bool hasData)
    {
        if (!hasData && registry.Find(id) is null)
        {
            throw new SpeakerNotFoundException(id);
        }
    }

    public static void MapSpeakerEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder speakers = app.MapGroup("/api/speakers");

        // Speakers currently announcing themselves on the network.
        speakers.MapGet("/", (SpeakerRegistry registry) => registry.GetAll());

        speakers.MapGet("/{id}/status", async (string id, SpeakerManager manager, CancellationToken cancellationToken) =>
        {
            SpeakerClient client = await manager.GetClientAsync(id, cancellationToken);
            return await client.GetStatusAsync(cancellationToken);
        });

        speakers.MapPut("/{id}/gain", async (string id, SetGainRequest request, SpeakerManager manager, CancellationToken cancellationToken) =>
        {
            SpeakerClient client = await manager.GetClientAsync(id, cancellationToken);
            await client.SetGainAsync(request.Gain, cancellationToken);
            return Results.NoContent();
        });

        speakers.MapPut("/{id}/mute", async (string id, SetMuteRequest request, SpeakerManager manager, CancellationToken cancellationToken) =>
        {
            SpeakerClient client = await manager.GetClientAsync(id, cancellationToken);
            await client.SetMuteAsync(request.Muted, cancellationToken);
            return Results.NoContent();
        });

        speakers.MapPut("/{id}/tilt", async (string id, SetTiltRequest request, SpeakerManager manager, CancellationToken cancellationToken) =>
        {
            SpeakerClient client = await manager.GetClientAsync(id, cancellationToken);
            await client.SetTiltAsync(request.Tilt, cancellationToken);
            return Results.NoContent();
        });

        speakers.MapPut("/{id}/name", async (string id, SetNameRequest request, SpeakerManager manager, SpeakerRegistry registry, CancellationToken cancellationToken) =>
        {
            SpeakerClient client = await manager.GetClientAsync(id, cancellationToken);
            await client.SetNameAsync(request.Name, cancellationToken);
            registry.Rename(id, request.Name); // show it everywhere now, not at the next announcement
            return Results.NoContent();
        });

        // Last 10 minutes of temperature and level, oldest first.
        // Still available for a while after a speaker goes offline.
        speakers.MapGet("/{id}/history", (string id, SpeakerHistory history, SpeakerRegistry registry) =>
        {
            EnsureKnown(id, registry, history.Has(id));
            return history.Get(id);
        });

        // Recent events, newest first.
        speakers.MapGet("/{id}/events", (string id, SpeakerEventLog events, SpeakerRegistry registry) =>
        {
            EnsureKnown(id, registry, events.Has(id));
            return events.Get(id);
        });
    }
}

// Request bodies, e.g. { "gain": -6.5 }
public record SetGainRequest(double Gain);

public record SetMuteRequest(bool Muted);

public record SetTiltRequest(double Tilt);

public record SetNameRequest(string Name);
