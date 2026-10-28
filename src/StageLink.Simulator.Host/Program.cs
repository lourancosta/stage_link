using System.Net;
using System.Net.Sockets;
using StageLink.Simulator;

// Usage: dotnet run --project src/StageLink.Simulator.Host [port] [name]
const int DefaultPort = 51000;

int port = args.Length > 0 ? int.Parse(args[0]) : DefaultPort;
string name = args.Length > 1 ? args[1] : "Main L";

SpeakerState state;
try
{
    state = new SpeakerState(name);
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine($"Invalid speaker name \"{name}\": {ex.Message}");
    return 1;
}

var server = new SpeakerServer(new Speaker(state), IPAddress.Any, port, Console.Out);

// Ctrl+C stops the server cleanly instead of killing the process.
using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    shutdown.Cancel();
};

try
{
    server.Start();
}
catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
{
    Console.Error.WriteLine($"Port {port} is already in use. Is another simulator running?");
    Console.Error.WriteLine($"Stop it, or pick another port: dotnet run --project src/StageLink.Simulator.Host {port + 1}");
    return 1;
}

Console.WriteLine($"Speaker \"{name}\" listening on port {server.Port}. Press Ctrl+C to stop.");

await server.RunAsync(shutdown.Token);
Console.WriteLine("Stopped.");
return 0;
