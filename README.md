<div align="center">
  <h2><strong>zFaceItScoreboard7GC</strong></h2>
  <h3>High-Performance FaceIT Scoreboard for CS2 (SwiftlyS2)</h3>
</div>

<p align="center">
  <img src="https://img.shields.io/badge/SwiftlyS2-CS2-orange" alt="SwiftlyS2">
  <img src="https://img.shields.io/badge/.NET-10.0-blue" alt=".NET 10.0">
  <img src="https://img.shields.io/badge/Author-vhming-green" alt="Author">
</p>

Display FaceIT Level, Challenger, Ban, or Unranked pins directly on the CS2 scoreboard (TAB) with zero lag and maximum efficiency.

---

## 🌟 Features

- **Multi-Provider API Architecture**:
  - **Primary (Default)**: `https://api.vhming.dev/v1/server/faceit/player/{steamid}` (Header: `X-API-SERVERKEY`).
  - **Fallback**: Official FaceIT API v4 (`open.faceit.com/data/v4/players`) when primary is unreachable.
- **Custom Collectible Coins & Pins**:
  - **Level 1 – 10**: Item IDs `1061, 1032, 1041, 1074, 1067, 1088, 1039, 1055, 1087, 1017`.
  - **Challenger**: Item ID `1089` (when player is Challenger).
  - **Banned**: Item ID `1018` (when player is Banned on FaceIT - highest priority).
  - **Null / Unranked**: Item ID `5138` (when player has no FaceIT account).
- **Temporary In-Match Fake Overrides**:
  - Set temporary fake ranks via command for testing or events.
  - **Match-Only**: Stored in RAM only, never written to disk, auto-cleared on map change/unload or player disconnect.
- **Event-Driven & Extreme Optimization**:
  - **Zero Tick Spam**: Removed all `OnTick` continuous loops. Updates are triggered on `player_spawn`, `round_freeze_end`, and on-connect completion.
  - **Smart Dirty-Checking**: If slot 5 already matches the target coin, the plugin exits immediately, generating **0 packets and 0% CPU overhead**.
- **Thread-Safety & Crash-Resistant**:
  - All schema / rank manipulations are executed on the Main Thread via `Core.Scheduler.NextTick`.
  - Local cache uses **Atomic File Replacement** (`.tmp` + atomic rename) to completely prevent file corruption.
- **Full Localization**:
  - 30+ language files (`en.jsonc`, `vn.jsonc`, `ru.jsonc`, `zh-CN.jsonc`, etc.) with client auto-detection.
  - Standard prefix `[gold]FaceIT [silver]» ` with clean color bracket styling.

---

## ⚙️ Configuration (`configs/plugins/zFaceItScoreboard7GC/config.jsonc`)

The configuration file is automatically generated from `config.template.jsonc` on first load:

```jsonc
{
  "FaceIT": {
    // Primary API Server Key for https://api.vhming.dev
    "VhmingApiKey": "your_vhming_server_key_here",

    // Fallback Official FaceIT API Key (from https://developers.faceit.com/)
    "FaceitApiKey": "",

    // HTTP request timeout in seconds
    "RequestTimeoutSeconds": 8,

    // Maximum concurrent requests
    "MaxConcurrentRequests": 4,

    // Coin item IDs for levels 1-10
    "LevelCoins": {
      "1": 1061,
      "2": 1032,
      "3": 1041,
      "4": 1074,
      "5": 1067,
      "6": 1088,
      "7": 1039,
      "8": 1055,
      "9": 1087,
      "10": 1017
    },

    // Challenger pin ID
    "ChallengerCoinId": 1089,

    // Banned pin ID
    "BannedCoinId": 1018,

    // Unranked / No Faceit pin ID
    "NullCoinId": 5138,

    // If false (default), level display is permanently enabled and players CANNOT turn it off.
    // If true, players can use !faceit / !fl to toggle display.
    "EnableToggleCommand": false,

    // Permission required to use !fake-faceit and !unfake-faceit commands
    "FakeFaceitPermission": "admin.faceit.fake.scoreboard",

    // Static fake FaceIT overrides mapped by SteamID64 ("1"-"10", "challenger", "ban", "null")
    "FakeFaceit": {
      "76561198000000000": "10",
      "76561198000000001": "challenger",
      "76561198000000002": "ban"
    },

    // Cache expiry in hours
    "CacheExpiryHours": 24,

    // Auto-save interval in seconds
    "AutoSaveIntervalSeconds": 120,

    // Safety verification check interval in seconds
    "SafetyCheckIntervalSeconds": 3.0
  }
}
```

---

## 🎮 Commands

### 1. Toggle Command
| Command | Aliases | Description | Permission |
| :--- | :--- | :--- | :--- |
| `!faceit` | `!fl` | Toggles player's own FaceIT badge display (on/off) | Everyone (Disabled if `EnableToggleCommand: false`) |

---

### 2. Fake FaceIT Commands (Temporary - Match Only)

> [!NOTE]
> All fake modifications made via commands are **strictly in-memory (temporary)**. They are **never saved to the cache file** and are **automatically cleared upon map change / match end or player disconnect**.

| Command | Aliases | Syntax | Description | Permission |
| :--- | :--- | :--- | :--- | :--- |
| `!fake-faceit` | `!fakefaceit`, `!ffl` | `!fake-faceit <player\|steamid> <1-10\|challenger\|ban\|null>` | Sets a temporary fake FaceIT rank for a player | `admin.faceit.fake.scoreboard` |
| `!fake-faceit` | `!fakefaceit`, `!ffl` | `!fake-faceit <1-10\|challenger\|ban\|null>` | Sets a temporary fake FaceIT rank for yourself | `admin.faceit.fake.scoreboard` |
| `!fake-faceit` | `!fakefaceit`, `!ffl` | `!fake-faceit <player\|steamid> reset` (or `clear`) | Clears fake override and restores real API stats | `admin.faceit.fake.scoreboard` |
| `!fake-faceit` | `!fakefaceit`, `!ffl` | `!fake-faceit clear-all` | Clears all active temporary fake overrides on the server | `admin.faceit.fake.scoreboard` |
| `!unfake-faceit`| `!unfakefaceit`, `!clearfake`, `!unffl` | `!unfake-faceit` | Clears temporary fake for yourself | `admin.faceit.fake.scoreboard` |
| `!unfake-faceit`| `!unfakefaceit`, `!clearfake`, `!unffl` | `!unfake-faceit <player\|steamid>` | Clears temporary fake for a specific target | `admin.faceit.fake.scoreboard` |
| `!unfake-faceit`| `!unfakefaceit`, `!clearfake`, `!unffl` | `!unfake-faceit all` | Clears all temporary fake overrides on the server | `admin.faceit.fake.scoreboard` |

#### Command Examples:
- `!fake-faceit vhming 10` - Sets Level 10 for player `vhming` immediately.
- `!fake-faceit 76561199801916052 challenger` - Sets Challenger badge for target SteamID.
- `!fake-faceit vhming ban` - Sets Banned badge for `vhming`.
- `!unfake-faceit vhming` - Restores `vhming` back to real API data.
- `!unfake-faceit all` - Clears all temporary fake overrides on the server.

*Server console can also execute these commands without any permission restrictions.*

---

## 🛠️ Building & Packaging

```bash
# Build the plugin
dotnet build

# Publish and create release zip package
dotnet publish -c Release
```

The compiled package and zip file will be generated in `build/publish/zFaceItScoreboard7GC` and `build/zFaceItScoreboard7GC.zip`.
