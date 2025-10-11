using Microsoft.Win32;
using System.Diagnostics;

namespace SteamPresenceSync;

class Program
{
    private const string SteamRegistryPath = @"HKEY_CURRENT_USER\Software\Valve\Steam\ActiveProcess";
    private const string RunningAppIdValueName = "RunningAppID";
    private const int DebounceSeconds = 60;
    private const int MaxRetries = 3;
    
    private static int? _lastAppId = null;
    private static DateTime _lastChangeTime = DateTime.MinValue;
    private static bool _isInGameMode = false;
    private static bool _hasDebounceCompleted = false;

    static async Task Main(string[] args)
    {
        Log("Steam Presence Sync started");
        Log($"Monitoring registry key: {SteamRegistryPath}\\{RunningAppIdValueName}");
        Log($"Debounce period: {DebounceSeconds} seconds");
        Log($"Max retries: {MaxRetries}");

        // Monitor the registry key continuously
        while (true)
        {
            try
            {
                CheckSteamStatus();
                await Task.Delay(1000); // Check every second
            }
            catch (Exception ex)
            {
                Log($"Error in main loop: {ex.Message}");
                await Task.Delay(5000); // Wait longer on error
            }
        }
    }

    private static void CheckSteamStatus()
    {
        try
        {
            // Read the RunningAppID from registry
            object? value = Registry.GetValue(SteamRegistryPath, RunningAppIdValueName, null);
            
            if (value == null)
            {
                // Registry key doesn't exist - this happens when Steam is not running
                if (_lastAppId != null)
                {
                    Log("Registry key not found. Steam may not be running.");
                    _lastAppId = null;
                }
                return;
            }

            int currentAppId = Convert.ToInt32(value);

            // Check if the app ID has changed
            if (!_lastAppId.HasValue || currentAppId != _lastAppId.Value)
            {
                Log($"App ID changed: {_lastAppId?.ToString() ?? "null"} -> {currentAppId}");
                _lastAppId = currentAppId;
                _lastChangeTime = DateTime.Now;
                _hasDebounceCompleted = false;
            }

            // Check if debounce period has passed
            TimeSpan timeSinceChange = DateTime.Now - _lastChangeTime;
            if (timeSinceChange.TotalSeconds < DebounceSeconds)
            {
                // Still in debounce period, don't take action yet
                if (!_hasDebounceCompleted)
                {
                    double remainingSeconds = DebounceSeconds - timeSinceChange.TotalSeconds;
                    Log($"Debounce in progress, waiting {remainingSeconds:F0} more seconds...");
                    _hasDebounceCompleted = true; // Don't log this repeatedly
                }
                return;
            }

            // Debounce period has passed, determine desired state
            bool shouldBeInGameMode = currentAppId != 0;

            // Only take action if state needs to change
            if (shouldBeInGameMode != _isInGameMode)
            {
                if (shouldBeInGameMode)
                {
                    Log($"Game detected (AppID: {currentAppId}), setting status to Online");
                    SetSteamStatus("online");
                    _isInGameMode = true;
                }
                else
                {
                    Log("No game running (AppID: 0), setting status to Offline");
                    SetSteamStatus("offline");
                    _isInGameMode = false;
                }
            }
        }
        catch (Exception ex)
        {
            Log($"Error checking Steam status: {ex.Message}");
        }
    }

    private static void SetSteamStatus(string status)
    {
        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                string steamUri = $"steam://friends/status/{status}";
                Log($"Attempt {attempt}/{MaxRetries}: Setting status via {steamUri}");

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = steamUri,
                    UseShellExecute = true
                };

                Process.Start(psi);
                Log($"Successfully set status to: {status}");
                return; // Success, exit retry loop
            }
            catch (Exception ex)
            {
                Log($"Attempt {attempt}/{MaxRetries} failed: {ex.Message}");
                
                if (attempt < MaxRetries)
                {
                    int sleepMs = 1000 * attempt; // Exponential backoff
                    Log($"Waiting {sleepMs}ms before retry...");
                    Thread.Sleep(sleepMs);
                }
            }
        }

        Log($"Failed to set status after {MaxRetries} attempts");
    }

    private static void Log(string message)
    {
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        string logMessage = $"[{timestamp}] {message}";
        Console.WriteLine(logMessage);
    }
}
