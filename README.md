# Steam-Presence-Sync
Automatically toggle your Steam friends list status as you play — let friends know when you're gaming, then vanish when you quit

## Overview
Steam Presence Sync is a Windows background application that monitors your Steam gaming activity and automatically updates your Steam Friends status:
- When you start playing a game (AppID ≠ 0), your status is set to **Online**
- When you close the game (AppID = 0), your status is set back to **Offline**

## Features
- 🎮 **Automatic Status Management**: Seamlessly toggles your Steam status based on game activity
- ⚡ **Event-Based Monitoring**: Uses Windows registry change notifications instead of polling - no constant CPU usage
- ⏱️ **Smart Debounce**: Instantly goes online when a game starts, but waits 60 seconds after closing to see if you launch another game
- 🔄 **Retry Logic**: Attempts up to 3 times with exponential backoff to ensure status changes succeed
- 📝 **Comprehensive Logging**: All actions are logged with timestamps for easy monitoring
- 🪟 **Background Operation**: Runs quietly in the background with no UI

## How It Works
The application uses Windows registry change notifications to monitor the `HKEY_CURRENT_USER\Software\Valve\Steam\RunningAppID` registry key. When Steam launches or closes a game, the registry value changes and triggers an event, which the application responds to. This event-based approach means the application only runs when changes occur, not constantly polling. It uses the Steam browser protocol (`steam://friends/status/...`) to update your status.

## Building and Running

### Prerequisites
- Windows operating system
- .NET 9.0 SDK or later
- Steam installed

### Build
```bash
dotnet build SteamPresenceSync.sln
```

### Run
```bash
dotnet run --project SteamPresenceSync/SteamPresenceSync.csproj
```

Or run the compiled executable:
```bash
SteamPresenceSync/bin/Debug/net9.0-windows/SteamPresenceSync.exe
```

## Configuration
The following constants can be modified in `Program.cs`:
- `DebounceSeconds`: Time to wait before changing status (default: 60 seconds)
- `MaxRetries`: Maximum retry attempts for status changes (default: 3)

## License
This project is licensed under the GNU General Public License v3.0 - see the [LICENSE](LICENSE) file for details.
