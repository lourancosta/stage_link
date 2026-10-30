using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

using StageLink.Protocol;
using StageLink.Web.Speakers;

namespace StageLink.Web.Api;

// Turns speaker-related exceptions into HTTP errors with a ProblemDetails body
// ({ "status": 422, "title": "...", "detail": "..." }), so endpoints need no try/catch.
public sealed class SpeakerExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        (int status, string title)? mapping = exception switch
        {
            SpeakerNotFoundException => (StatusCodes.Status404NotFound, "Speaker not found"),
            SpeakerCommandException { Code: NackCode.ValueOutOfRange } => (
                StatusCodes.Status422UnprocessableEntity,
                "Value out of range"),
            ArgumentOutOfRangeException => (StatusCodes.Status422UnprocessableEntity, "Value out of range"),
            SpeakerCommandException => (StatusCodes.Status400BadRequest, "Command rejected by speaker"),
            SpeakerUnavailableException or TimeoutException or IOException => (
                StatusCodes.Status503ServiceUnavailable,
                "Speaker unavailable"),
            InvalidDataException => (StatusCodes.Status502BadGateway, "Invalid reply from speaker"),
            _ => null, // Not ours: let the default handling produce a 500.
        };

        if (mapping is not { } map)
        {
            return false;
        }

        context.Response.StatusCode = map.status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = map.status,
                Title = map.title,
                Detail = exception.Message,
            },
        });
    }
}
