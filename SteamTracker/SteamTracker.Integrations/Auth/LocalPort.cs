using System.Net;
using System.Net.Sockets;

namespace SteamTracker.Integrations.Auth;

internal static class LocalPort
{
    public static int FindFree()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }
}
