using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Options;
using StageLink.Protocol;

namespace StageLink.Web.Discovery;

// Runs for as long as the backend is running: receives Announce datagrams on
// UDP and keeps the SpeakerRegistry up to date.
public class DiscoveryService : BackgroundService
{
    private readonly SpeakerRegistry _registry;
    private readonly TimeProvider _time;
    private readonly ILogger<DiscoveryService> _logger;
    private readonly int _port;

    public DiscoveryService(
        SpeakerRegistry registry,
        TimeProvider time,
        IOptions<DiscoveryOptions> options,
        ILogger<DiscoveryService> logger)
    {
        _registry = registry;
        _time = time;
        _logger = logger;
        _port = options.Value.Port;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var udp = new UdpClient();

        // Let other programs listen on the same port too (for example a second backend).
        udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

        try
        {
            udp.Client.Bind(new IPEndPoint(IPAddress.Any, _port));
        }
        catch (SocketException ex)
        {
            // Don't take the whole backend down; the API still works, just without discovery.
            _logger.LogError(ex, "Discovery could not listen on UDP port {Port}", _port);
            return;
        }

        _logger.LogInformation("Listening for speakers on UDP port {Port}", ((IPEndPoint)udp.Client.LocalEndPoint!).Port);

        while (!stoppingToken.IsCancellationRequested)
        {
            UdpReceiveResult datagram;
            try
            {
                datagram = await udp.ReceiveAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break; // Backend is shutting down.
            }

            HandleDatagram(datagram);
        }
    }

    // One datagram = one frame. Anything that isn't a valid Announce is ignored.
    private void HandleDatagram(UdpReceiveResult datagram)
    {
        try
        {
            Frame frame = Frame.Decode(datagram.Buffer);
            if (frame.Command != Command.Announce)
            {
                return;
            }

            DiscoveredSpeaker speaker = DiscoveredSpeaker.FromAnnounce(frame, datagram.RemoteEndPoint.Address, _time.GetUtcNow());
            _registry.Update(speaker);
        }
        catch (Exception ex) when (ex is InvalidFrameException or EndOfStreamException or InvalidDataException)
        {
            _logger.LogDebug("Ignored invalid datagram from {Sender}: {Reason}", datagram.RemoteEndPoint, ex.Message);
        }
    }
}
