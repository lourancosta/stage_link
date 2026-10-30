var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// A simple "is the backend up?" check, used by the frontend and by tests.
app.MapGet("/api/health", () => Results.Ok(new { Status = "ok" }));

app.Run();
