using StageLink.Protocol;

namespace StageLink.Web.Tests;

// Builds Announce frames the way a speaker would send them.
public static class TestAnnouncements
{
    public static Frame Announce(string id, string name, ushort tcpPort = 51000, ushort messageNumber = 1)
    {
        var payload = new ByteWriter();
        payload.WriteString(id);
        payload.WriteString(name);
        payload.WriteString("StageLink Sim");
        payload.WriteString("1.0.0");
        payload.WriteUInt16(tcpPort);

        return new Frame(Command.Announce, messageNumber, payload.ToArray());
    }
}
