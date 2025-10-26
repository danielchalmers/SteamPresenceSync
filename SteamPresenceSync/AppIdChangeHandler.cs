namespace SteamPresenceSync;

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
        var hasChanged = !LastAppId.HasValue || currentAppId != LastAppId.Value;
        
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
