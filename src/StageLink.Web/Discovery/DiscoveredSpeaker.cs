using System.Net;
using StageLink.Protocol;

namespace StageLink.Web.Discovery;

// A speaker found through its Announce messages.
// Address is a string (not IPAddress) so it serializes cleanly to JSON.
public record DiscoveredSpeaker(
    string Id,
    string Name,
    string Model,
    string Firmware,
    string Address,
    ushort TcpPort,
    DateTimeOffset LastSeen)
{
    // Announce data: id, name, model, firmware (each length + ASCII), TCP port (2)
    public static DiscoveredSpeaker FromAnnounce(Frame announce, IPAddress sender, DateTimeOffset seenAt)
    {
        if (announce.Command != Command.Announce)
        {
            throw new ArgumentException($"Expected an Announce frame, got {announce.Command}.", nameof(announce));
        }

        var reader = new ByteReader(announce.Payload);
        string id = reader.ReadString();
        string name = reader.ReadString();
        string model = reader.ReadString();
        string firmware = reader.ReadString();
        ushort tcpPort = reader.ReadUInt16();

        return new DiscoveredSpeaker(id, name, model, firmware, sender.ToString(), tcpPort, seenAt);
    }
}
