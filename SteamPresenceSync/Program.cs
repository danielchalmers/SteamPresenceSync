using Microsoft.Win32;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SteamPresenceSync;

class Program
{
    private const string SteamRegistryPath = @"Software\Valve\Steam\ActiveProcess";
    private const string RunningAppIdValueName = "RunningAppID";
    private const int DebounceSeconds = 60;
    private const int MaxRetries = 3;
    
    private static int? _lastAppId = null;
    private static DateTime _lastChangeTime = DateTime.MinValue;
    private static bool _isInGameMode = false;
    private static Timer? _debounceTimer = null;
    private static readonly object _lock = new object();

    // Win32 API constants for registry change notifications
    private const int REG_NOTIFY_CHANGE_LAST_SET = 0x00000004;
    
    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern int RegNotifyChangeKeyValue(
        IntPtr hKey,
        bool bWatchSubtree,
        int dwNotifyFilter,
        IntPtr hEvent,
        bool fAsynchronous);

    static async Task Main(string[] args)
    {
        Log("Steam Presence Sync started");
        Log($"Monitoring registry key: HKEY_CURRENT_USER\\{SteamRegistryPath}\\{RunningAppIdValueName}");
        Log($"Debounce period: {DebounceSeconds} seconds");
        Log($"Max retries: {MaxRetries}");
        Log("Using event-based registry monitoring (not polling)");

        // Check initial state
        CheckSteamStatus();

        // Start monitoring registry for changes
        await MonitorRegistryChanges();
    }

    private static async Task MonitorRegistryChanges()
    {
        while (true)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(SteamRegistryPath, false);
                
                if (key == null)
                {
                    Log("Registry key not found. Waiting for Steam to start...");
                    await Task.Delay(5000); // Check every 5 seconds if Steam is not running
                    continue;
                }

                // Create an event to wait on
                using var changeEvent = new ManualResetEvent(false);
                
                // Register for change notifications
                int result = RegNotifyChangeKeyValue(
                    key.Handle.DangerousGetHandle(),
                    false, // Don't watch subtree
                    REG_NOTIFY_CHANGE_LAST_SET, // Notify on value changes
                    changeEvent.SafeWaitHandle.DangerousGetHandle(),
                    true); // Asynchronous

                if (result != 0)
                {
                    Log($"Failed to register for registry notifications. Error code: {result}");
                    await Task.Delay(5000);
                    continue;
                }

                Log("Waiting for registry changes...");
                
                // Wait for the registry change event
                await Task.Run(() => changeEvent.WaitOne());
                
                Log("Registry change detected");
                CheckSteamStatus();
            }
            catch (Exception ex)
            {
                Log($"Error in monitoring loop: {ex.Message}");
                await Task.Delay(5000);
            }
        }
    }

    private static void CheckSteamStatus()
    {
        try
        {
            // Read the RunningAppID from registry
            object? value = Registry.GetValue($"HKEY_CURRENT_USER\\{SteamRegistryPath}", RunningAppIdValueName, null);
            
            if (value == null)
            {
                // Registry key doesn't exist - this happens when Steam is not running
                if (_lastAppId != null)
                {
                    Log("Registry value not found.");
                    _lastAppId = null;
                }
                return;
            }

            int currentAppId = Convert.ToInt32(value);

            // Check if the app ID has changed
            if (!_lastAppId.HasValue || currentAppId != _lastAppId.Value)
            {
                Log($"App ID changed: {_lastAppId?.ToString() ?? "null"} -> {currentAppId}");
                
                lock (_lock)
                {
                    _lastAppId = currentAppId;
                    _lastChangeTime = DateTime.Now;
                    
                    // Cancel existing debounce timer if any
                    _debounceTimer?.Dispose();
                    
                    // Start debounce timer
                    _debounceTimer = new Timer(OnDebounceComplete, currentAppId, 
                        TimeSpan.FromSeconds(DebounceSeconds), Timeout.InfiniteTimeSpan);
                    
                    Log($"Debounce timer started, will process change in {DebounceSeconds} seconds...");
                }
            }
        }
        catch (Exception ex)
        {
            Log($"Error checking Steam status: {ex.Message}");
        }
    }

    private static void OnDebounceComplete(object? state)
    {
        try
        {
            int currentAppId = (int)state!;
            
            lock (_lock)
            {
                // Verify the app ID hasn't changed during debounce period
                if (_lastAppId.HasValue && _lastAppId.Value == currentAppId)
                {
                    bool shouldBeInGameMode = currentAppId != 0;
                    
                    // Only take action if state needs to change
                    if (shouldBeInGameMode != _isInGameMode)
                    {
                        if (shouldBeInGameMode)
                        {
                            Log($"Debounce complete. Game detected (AppID: {currentAppId}), setting status to Online");
                            SetSteamStatus("online");
                            _isInGameMode = true;
                        }
                        else
                        {
                            Log("Debounce complete. No game running (AppID: 0), setting status to Offline");
                            SetSteamStatus("offline");
                            _isInGameMode = false;
                        }
                    }
                }
                else
                {
                    Log("App ID changed during debounce period, action cancelled");
                }
            }
        }
        catch (Exception ex)
        {
            Log($"Error in debounce callback: {ex.Message}");
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
