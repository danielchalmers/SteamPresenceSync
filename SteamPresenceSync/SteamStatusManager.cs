using System.Diagnostics;

namespace SteamPresenceSync;

// Helper class for Steam status operations
public static class SteamStatusManager
{
    public static string BuildStatusUri(string status) => $"steam://friends/status/{status}";
    
    public static void SetStatus(string status, int maxRetries, Action<string> log)
    {
        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var steamUri = BuildStatusUri(status);
                log($"Attempt {attempt}/{maxRetries}: Setting status via {steamUri}");

                var psi = new ProcessStartInfo
                {
                    FileName = steamUri,
                    UseShellExecute = true
                };

                Process.Start(psi);
                log($"Successfully set status to: {status}");
                return;
            }
            catch (Exception ex)
            {
                log($"Attempt {attempt}/{maxRetries} failed: {ex.Message}");
                
                if (attempt < maxRetries)
                {
                    var sleepMs = 1000 * attempt;
                    log($"Waiting {sleepMs}ms before retry...");
                    Thread.Sleep(sleepMs);
                }
            }
        }

        log($"Failed to set status after {maxRetries} attempts");
    }
}
