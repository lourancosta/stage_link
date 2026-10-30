using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace StageLink.Web.Tests;

// Helpers for starting the backend in tests without touching the real UDP port 51001.
public static class TestApp
{
    public static WebApplicationFactory<Program> WithDiscoveryPort(int port)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseSetting("Discovery:Port", port.ToString()));
    }

    // Asks the OS for a free UDP port, then releases it for the test to use.
    public static int FreeUdpPort()
    {
        using var probe = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)probe.Client.LocalEndPoint!).Port;
    }
}
