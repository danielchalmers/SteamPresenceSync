using Microsoft.Win32;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SteamPresenceSync;

// Helper class for Steam status operations
public static class SteamStatusManager
{
    public static string BuildStatusUri(string status) => $"steam://friends/status/{status}";
    
    public static void SetStatus(string status, int maxRetries, Action<string> log)
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                string steamUri = BuildStatusUri(status);
                log($"Attempt {attempt}/{maxRetries}: Setting status via {steamUri}");

                ProcessStartInfo psi = new ProcessStartInfo
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
                    int sleepMs = 1000 * attempt;
                    log($"Waiting {sleepMs}ms before retry...");
                    Thread.Sleep(sleepMs);
                }
            }
        }

        log($"Failed to set status after {maxRetries} attempts");
    }
}

// Helper class for handling app ID changes
public class AppIdChangeHandler
{
    public int? LastAppId { get; private set; }
    public bool IsInitialized { get; private set; }
    
    public bool ShouldProcessChange(int currentAppId)
    {
        // Skip logging and processing the initial null -> 0 transition
        if (!IsInitialized && currentAppId == 0)
        {
            LastAppId = currentAppId;
            IsInitialized = true;
            return false;
        }
        
        // Check if the app ID has changed
        bool hasChanged = !LastAppId.HasValue || currentAppId != LastAppId.Value;
        
        if (hasChanged)
        {
            LastAppId = currentAppId;
            IsInitialized = true;
        }
        
        return hasChanged;
    }
    
    public bool IsGameStarting(int appId) => appId != 0;
    
    public void Reset()
    {
        LastAppId = null;
        IsInitialized = false;
    }
}

class Program
{
    private const string SteamRegistryPath = @"Software\Valve\Steam";
    private const string RunningAppIdValueName = "RunningAppID";
    private const int DebounceSeconds = 60;
    private const int MaxRetries = 3;
    
    private static readonly AppIdChangeHandler _changeHandler = new AppIdChangeHandler();
    private static DateTime _lastChangeTime = DateTime.MinValue;
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
                if (_changeHandler.LastAppId != null)
                {
                    Log("Registry value not found.");
                    _changeHandler.Reset();
                }
                return;
            }

            int currentAppId = Convert.ToInt32(value);

            // Check if the app ID has changed
            if (_changeHandler.ShouldProcessChange(currentAppId))
            {
                Log($"App ID changed: {_changeHandler.LastAppId?.ToString() ?? "null"} -> {currentAppId}");
                
                lock (_lock)
                {
                    _lastChangeTime = DateTime.Now;
                    
                    // Cancel existing debounce timer if any
                    _debounceTimer?.Dispose();
                    
                    bool isGameStarting = _changeHandler.IsGameStarting(currentAppId);
                    
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
            int currentAppId = (int)state!;
            
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
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        string logMessage = $"[{timestamp}] {message}";
        Console.WriteLine(logMessage);
    }
}
