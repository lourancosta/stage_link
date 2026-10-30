using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace StageLink.Simulator;

// Broadcasts an Announce frame over UDP at a fixed interval, so apps on the
// network can discover the speaker without knowing its IP address.
// UDP keeps datagrams whole, so each datagram carries exactly one frame.
public class Announcer
{
    public const int DiscoveryPort = 51001;
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(2);

    private readonly Speaker _speaker;
    private readonly ushort _tcpPort;
    private readonly IReadOnlyList<IPEndPoint> _destinations;
    private readonly HashSet<IPEndPoint> _failedDestinations = [];
    private readonly TimeSpan _interval;
    private readonly TextWriter _log;

    private ushort _messageNumber;

    public Announcer(
        Speaker speaker,
        int tcpPort,
        IReadOnlyList<IPEndPoint> destinations,
        TimeSpan interval,
        TextWriter? log = null)
    {
        _speaker = speaker;
        _tcpPort = (ushort)tcpPort;
        _destinations = destinations;
        _interval = interval;
        _log = log ?? TextWriter.Null;
    }

    // Sends one Announce right away, then one per interval, until cancelled.
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        using var udp = new UdpClient();
        udp.EnableBroadcast = true;
        using var timer = new PeriodicTimer(_interval);

        try
        {
            do
            {
                // ushort wraps from 65535 back to 0, which is fine for a counter.
                _messageNumber++;

                // No lock needed: BuildAnnounce only reads the name and identity,
                // which never change after the speaker is created.
                byte[] datagram = _speaker.BuildAnnounce(_messageNumber, _tcpPort).Encode();

                foreach (IPEndPoint destination in _destinations)
                {
                    await SendAsync(udp, datagram, destination, cancellationToken);
                }
            }
            while (await timer.WaitForNextTickAsync(cancellationToken));
        }
        catch (OperationCanceledException)
        {
            // Stopping.
        }
    }

    // Where to announce: this machine (127.0.0.1), so local apps always see it,
    // plus the broadcast address of every active IPv4 network.
    // Sending to 255.255.255.255 is refused by macOS ("No route to host"),
    // so each network's own broadcast address is used instead.
    public static IReadOnlyList<IPEndPoint> LocalNetworkDestinations(int port)
    {
        var destinations = new List<IPEndPoint> { new(IPAddress.Loopback, port) };

        foreach (NetworkInterface network in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (network.OperationalStatus != OperationalStatus.Up
                || network.NetworkInterfaceType == NetworkInterfaceType.Loopback)
            {
                continue;
            }

            foreach (UnicastIPAddressInformation address in network.GetIPProperties().UnicastAddresses)
            {
                if (address.Address.AddressFamily == AddressFamily.InterNetwork)
                {
                    IPAddress broadcast = BroadcastAddress(address.Address, address.IPv4Mask);
                    destinations.Add(new IPEndPoint(broadcast, port));
                }
            }
        }

        return destinations;
    }

    // The mask's 1 bits mark the network part of an address and its 0 bits the
    // host part. Setting every host bit to 1 gives the network's broadcast address:
    // 192.168.1.42 with mask 255.255.255.0 → 192.168.1.255
    public static IPAddress BroadcastAddress(IPAddress address, IPAddress mask)
    {
        byte[] addressBytes = address.GetAddressBytes();
        byte[] maskBytes = mask.GetAddressBytes();
        byte[] broadcast = new byte[addressBytes.Length];

        for (int i = 0; i < broadcast.Length; i++)
        {
            broadcast[i] = (byte)(addressBytes[i] | ~maskBytes[i]);
        }

        return new IPAddress(broadcast);
    }

    private async Task SendAsync(UdpClient udp, byte[] datagram, IPEndPoint destination, CancellationToken cancellationToken)
    {
        try
        {
            await udp.SendAsync(datagram, destination, cancellationToken);
            _failedDestinations.Remove(destination);
        }
        catch (SocketException ex)
        {
            // For example a network that went down. Keep trying, but log each
            // failing destination only once instead of every 2 seconds.
            if (_failedDestinations.Add(destination))
            {
                _log.WriteLine($"Announce to {destination} failed: {ex.Message}");
            }
        }
    }
}
