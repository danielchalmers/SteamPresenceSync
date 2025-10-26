namespace SteamPresenceSync.Tests;

public class AppIdChangeHandlerTests
{
    [Fact]
    public void ShouldProcessChange_ReturnsFalse_ForInitialZeroAppId()
    {
        var handler = new AppIdChangeHandler();
        
        var result = handler.ShouldProcessChange(0);
        
        Assert.False(result);
        Assert.Equal(0, handler.LastAppId);
        Assert.True(handler.IsInitialized);
    }

    [Fact]
    public void ShouldProcessChange_ReturnsTrue_ForFirstNonZeroAppId()
    {
        var handler = new AppIdChangeHandler();
        
        var result = handler.ShouldProcessChange(123);
        
        Assert.True(result);
        Assert.Equal(123, handler.LastAppId);
        Assert.True(handler.IsInitialized);
    }

    [Fact]
    public void ShouldProcessChange_ReturnsTrue_WhenAppIdChanges()
    {
        var handler = new AppIdChangeHandler();
        handler.ShouldProcessChange(123);
        
        var result = handler.ShouldProcessChange(456);
        
        Assert.True(result);
        Assert.Equal(456, handler.LastAppId);
    }

    [Fact]
    public void ShouldProcessChange_ReturnsFalse_WhenAppIdStaysSame()
    {
        var handler = new AppIdChangeHandler();
        handler.ShouldProcessChange(123);
        
        var result = handler.ShouldProcessChange(123);
        
        Assert.False(result);
        Assert.Equal(123, handler.LastAppId);
    }

    [Fact]
    public void ShouldProcessChange_ReturnsTrue_AfterReset()
    {
        var handler = new AppIdChangeHandler();
        handler.ShouldProcessChange(123);
        handler.Reset();
        
        var result = handler.ShouldProcessChange(123);
        
        Assert.True(result);
        Assert.Equal(123, handler.LastAppId);
    }

    [Fact]
    public void IsGameStarting_ReturnsTrue_ForNonZeroAppId()
    {
        var handler = new AppIdChangeHandler();
        
        Assert.True(handler.IsGameStarting(123));
        Assert.True(handler.IsGameStarting(1));
        Assert.True(handler.IsGameStarting(999));
    }

    [Fact]
    public void IsGameStarting_ReturnsFalse_ForZeroAppId()
    {
        var handler = new AppIdChangeHandler();
        
        Assert.False(handler.IsGameStarting(0));
    }

    [Fact]
    public void Reset_ClearsStateCompletely()
    {
        var handler = new AppIdChangeHandler();
        handler.ShouldProcessChange(123);
        
        handler.Reset();
        
        Assert.Null(handler.LastAppId);
        Assert.False(handler.IsInitialized);
    }

    [Theory]
    [InlineData(0, 123, true)]
    [InlineData(123, 456, true)]
    [InlineData(123, 0, true)]
    public void ShouldProcessChange_HandlesTransitions_Correctly(int firstAppId, int secondAppId, bool expectedSecondResult)
    {
        var handler = new AppIdChangeHandler();
        
        // Handle special case: initial 0 should not be processed
        if (firstAppId == 0)
        {
            Assert.False(handler.ShouldProcessChange(firstAppId));
        }
        else
        {
            Assert.True(handler.ShouldProcessChange(firstAppId));
        }
        
        var result = handler.ShouldProcessChange(secondAppId);
        
        Assert.Equal(expectedSecondResult, result);
    }
}
