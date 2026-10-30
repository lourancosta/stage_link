using System.Text.Json.Serialization;
using StageLink.Web.Api;
using StageLink.Web.Discovery;
using StageLink.Web.Realtime;
using StageLink.Web.Speakers;

var builder = WebApplication.CreateBuilder(args);

// Discovery: settings, the shared speaker list, and the background UDP listener.
builder.Services.Configure<DiscoveryOptions>(builder.Configuration.GetSection("Discovery"));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<SpeakerRegistry>();
builder.Services.AddHostedService<DiscoveryService>();

// Speakers: one shared TCP connection per speaker.
builder.Services.Configure<SpeakerOptions>(builder.Configuration.GetSection("Speakers"));
builder.Services.AddSingleton<SpeakerManager>();

// Live updates: SignalR hub, speaker list pushes and Telemetry forwarding.
// Enums (like Faults) are sent as names instead of numbers.
builder.Services.AddSignalR()
    .AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddHostedService<SpeakerListBroadcaster>();
builder.Services.AddSingleton<TelemetryForwarder>();
builder.Services.AddHostedService(services => services.GetRequiredService<TelemetryForwarder>());

// Errors: speaker exceptions become HTTP status codes with a ProblemDetails body.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<SpeakerExceptionHandler>();

var app = builder.Build();

app.UseExceptionHandler();

// A simple "is the backend up?" check, used by the frontend and by tests.
app.MapGet("/api/health", () => Results.Ok(new { Status = "ok" }));

app.MapSpeakerEndpoints();
app.MapHub<SpeakerHub>("/hubs/speakers");

app.Run();
