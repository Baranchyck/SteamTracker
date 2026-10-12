using System.ComponentModel;
using System.Diagnostics;
using SteamTracker.Domain.Exceptions; 

namespace SteamTracker.Integrations.Auth;

internal static class BrowserLauncher
{
    public static void Open(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Win32Exception ex)
        {
            throw new AuthFailedException("Could not open the system browser.", ex);
        }
    }
}