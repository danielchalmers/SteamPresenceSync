namespace SteamPresenceSync.Tests;

public class SteamStatusManagerTests
{
    [Fact]
    public void BuildStatusUri_ReturnsCorrectUri_ForOnlineStatus()
    {
        var uri = SteamStatusManager.BuildStatusUri("online");
        Assert.Equal("steam://friends/status/online", uri);
    }

    [Fact]
    public void BuildStatusUri_ReturnsCorrectUri_ForOfflineStatus()
    {
        var uri = SteamStatusManager.BuildStatusUri("offline");
        Assert.Equal("steam://friends/status/offline", uri);
    }

    [Fact]
    public void BuildStatusUri_ReturnsCorrectUri_ForAnyStatus()
    {
        var uri = SteamStatusManager.BuildStatusUri("away");
        Assert.Equal("steam://friends/status/away", uri);
    }
}
