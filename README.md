# 🎮 Presence Sync for Steam

[![Release](https://img.shields.io/github/v/release/danielchalmers/SteamPresenceSync)](https://github.com/danielchalmers/SteamPresenceSync/releases/latest)
[![License](https://img.shields.io/badge/License-GPLv3-blue.svg)](LICENSE)

This app monitors your Steam gaming activity and automatically updates your Steam Friends status:  
🟢 When you start playing a game your status is set to **Online**  
🔴 When you close the game your status is set back to **Offline**

## ⚙️ How It Works

The application uses Windows registry change notifications to monitor the `HKEY_CURRENT_USER\Software\Valve\Steam\RunningAppID` registry key (to check if AppID = 0). When Steam launches or closes a game, the registry value changes and triggers an event, which the application responds to. This event-based approach means the application only runs when changes occur, not constantly polling. It uses the Steam browser protocol (`steam://friends/status/...`) to update your status.
