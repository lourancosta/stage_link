using StageLink.Web.Discovery;
using StageLink.Web.Speakers;

namespace StageLink.Web.Api;

// HTTP endpoints for speakers. Errors are thrown as exceptions and turned into
// HTTP status codes in one place: SpeakerExceptionHandler.
public static class SpeakerEndpoints
{
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
    }
}

// Request bodies, e.g. { "gain": -6.5 }
public record SetGainRequest(double Gain);

public record SetMuteRequest(bool Muted);

public record SetTiltRequest(double Tilt);
