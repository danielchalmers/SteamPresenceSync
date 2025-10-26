using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace SteamPresenceSync;

class Program
{
    private const string SteamRegistryPath = @"Software\Valve\Steam";
    private const string RunningAppIdValueName = "RunningAppID";
    private const int DebounceSeconds = 60;
    private const int MaxRetries = 3;

    private static readonly AppIdChangeHandler _changeHandler = new();
    private static Timer? _debounceTimer = null;
    private static readonly object _lock = new();

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
                var result = RegNotifyChangeKeyValue(
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
            var value = Registry.GetValue($"HKEY_CURRENT_USER\\{SteamRegistryPath}", RunningAppIdValueName, null);

            if (value == null)
            {
                // Registry key doesn't exist - this happens when Steam is not running
                if (_changeHandler.LastAppId != null)
                {
                    Log("Registry value not found.");
                    _changeHandler.Reset();
                }
                return;
            }

            var currentAppId = Convert.ToInt32(value);

            // Capture the old value before checking if changed
            var previousAppId = _changeHandler.LastAppId;

            // Check if the app ID has changed
            if (_changeHandler.ShouldProcessChange(currentAppId))
            {
                Log($"App ID changed: {previousAppId?.ToString() ?? "null"} -> {currentAppId}");

                lock (_lock)
                {
                    // Cancel existing debounce timer if any
                    _debounceTimer?.Dispose();

                    var isGameStarting = _changeHandler.IsGameStarting(currentAppId);

                    if (isGameStarting)
                    {
                        // Game starting - no debounce, set status immediately
                        Log($"Game detected (AppID: {currentAppId}), setting status to Online immediately");
                        SteamStatusManager.SetStatus("online", MaxRetries, Log);
                    }
                    else
                    {
                        // Game ending - use debounce to wait and see if user launches another game
                        _debounceTimer = new Timer(OnDebounceComplete, currentAppId,
                            TimeSpan.FromSeconds(DebounceSeconds), Timeout.InfiniteTimeSpan);

                        Log($"Game closed, debounce timer started. Will set status to Offline in {DebounceSeconds} seconds if no new game starts...");
                    }
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
            var currentAppId = (int)state!;

            lock (_lock)
            {
                // Verify the app ID hasn't changed during debounce period
                if (_changeHandler.LastAppId.HasValue && _changeHandler.LastAppId.Value == currentAppId && currentAppId == 0)
                {
                    // Only set to offline after debounce
                    Log("Debounce complete. No game started, setting status to Offline");
                    SteamStatusManager.SetStatus("offline", MaxRetries, Log);
                }
                else if (_changeHandler.LastAppId.HasValue && _changeHandler.LastAppId.Value != currentAppId)
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

    private static void Log(string message)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var logMessage = $"[{timestamp}] {message}";
        Console.WriteLine(logMessage);
    }
}
