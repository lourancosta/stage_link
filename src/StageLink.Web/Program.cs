using StageLink.Web.Discovery;

var builder = WebApplication.CreateBuilder(args);

// Discovery: settings, the shared speaker list, and the background UDP listener.
builder.Services.Configure<DiscoveryOptions>(builder.Configuration.GetSection("Discovery"));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<SpeakerRegistry>();
builder.Services.AddHostedService<DiscoveryService>();

var app = builder.Build();

// A simple "is the backend up?" check, used by the frontend and by tests.
app.MapGet("/api/health", () => Results.Ok(new { Status = "ok" }));

// Speakers currently announcing themselves on the network.
app.MapGet("/api/speakers", (SpeakerRegistry registry) => registry.GetAll());

app.Run();
