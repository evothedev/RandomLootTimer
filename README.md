# 🚀 Join Our Discord Community!

Welcome! If you are looking for support, want to make suggestions on projects, or just want to see our plugin updates.

---

## 🤝 How to Join

Getting access to the server takes less than a minute:

1. Click the large banner to open the invite link.
2. Accept the invite.

<p align="left">
  <a href="https://discord.gg/tyc3dUsCzu">
    <img src="https://discord.com/api/guilds/1507400042925785178/widget.png?style=banner2" alt="Discord Banner 2"/>
  </a>
</p>

---

# Random Loot Timer

A robust, highly optimized, and feature-rich Rust server plugin that gives players completely random game items at customizable intervals. Features an on-screen HUD countdown, player opt-out capabilities, and a comprehensive in-game Admin Telemetry & Management dashboard.

## 🚀 Features

-   **Dynamic Item Database Scraping:** Scrapes Rust's actual engine database directly at startup. It automatically filters out unspawnable, broken, and server-crashing objects (like unfinished test items, static generators, or car chassis parts) to ensure only valid, functional loot is given.
-   **Smart Stacking System:** Intelligently checks item stackability. It automatically restricts unstackable items (like Weapons, Attire, or Tools) to a maximum drop of 1, preventing inventory overload while respecting custom amount configurations for stackable items.
-   **Live Countdown HUD:** An elegant, translucent screen overlay that updates every second, displaying exactly when the next round of loot is arriving.
-   **Player Autonomy (`/items`):** Normal players can instantly opt out of the loot drops. Opting out disables random loot deliveries and hides the HUD timer overlay.
-   **Admin Configuration Cockpit (`/lootconfig`):** A beautiful, cursor-enabled graphical user interface (CUI) for administrators.
-   **Real-time Telemetry Analytics:** Displays server uptime, total loop cycles completed, total items distributed, live active/opted-out player counts, and the total indexed pool size.

## 🛠️ Installation

### Oxide Users

1.  Download `RandomLootTimer.cs`.
2.  Move the file to your server's `oxide/plugins/` directory.
3.  The configuration file will automatically generate in `oxide/config/RandomLootTimer.json`.

### Carbon Users

1.  Download `RandomLootTimer.cs`.
2.  Move the file to your server's `carbon/plugins/` directory.
3.  The configuration file will automatically generate in `carbon/config/RandomLootTimer.json`.

## 🔑 Permissions

Admin functions require the `randomloottimer.admin` permission.

### How to Grant (Oxide)

```
oxide.grant user <username_or_steamid> randomloottimer.admin
# OR
oxide.grant group admin randomloottimer.admin
```

### How to Grant (Carbon)

```
c.grant user <username_or_steamid> randomloottimer.admin
# OR
c.grant group admin randomloottimer.admin
```

## 💬 Commands

### Player Commands

| Command | Type | Description |
| --- | --- | --- |
| `/items` | Chat | Toggles random loot drops and the screen HUD countdown ON/OFF. |

### Admin Commands

| Command | Type | Description |
| --- | --- | --- |
| `/lootconfig` | Chat | Opens the in-game Telemetry & Management dashboard. |

## ⚙️ Configuration

The configuration file allows you to adjust the baseline behaviors of the system.

```
{
  "IntervalSeconds": 10,
  "DefaultMinAmount": 1,
  "DefaultMaxAmount": 5,
  "EnabledGlobally": true,
  "EnableChatMessages": true
}
```

### Configuration Options Explained

-   **`IntervalSeconds`**: The duration of the countdown loop in seconds.
-   **`DefaultMinAmount`**: The minimum quantity of stackable items distributed per cycle.
-   **`DefaultMaxAmount`**: The maximum quantity of stackable items distributed per cycle.
-   **`EnabledGlobally`**: Sets whether the system is active server-wide on startup.
-   **`EnableChatMessages`**: Toggles whether players receive a custom chat message notifying them of their won items.

## 📊 The Admin Dashboard

Typing `/lootconfig` in chat brings up the administrative interface:

```
+-------------------------------------------------------------+
|          LOOT TIMER TELEMETRY & MANAGEMENT SYSTEM          |
+-------------------------------------------------------------+
|  OPERATIONAL STATUS CONTROLS  |   LIVE METRICS & ANALYTICS  |
|                               |                             |
|  [ SYSTEM: ACTIVE / STOPPED ] |   Runtime Performance Stats |
|                               |   • Server Uptime: 01h 12m  |
|  [ NOTIFY CHAT: ON / OFF ]    |   • Cycles Ran: 432         |
|                               |   • Items Given: 1,540      |
|  CURRENT INTERVAL: 10 SECONDS |                             |
|                               |   • Opted-In: 18 / 20       |
|  [ Set 10s ]   [ Set 30s ]    |   • Opted-Out: 2            |
|                               |   • Indexed Pool: 382 Items |
+-------------------------------------------------------------+
|                         [ DISMISS ]                         |
+-------------------------------------------------------------+
```

### Interactive Controls

-   **Toggle Global System:** Instantly pauses or resumes the cycle for all players. When paused, the countdown HUD vanishes globally.
-   **Toggle Chat Notification:** Switches between silent distribution and chat spam messages.
-   **Interval Hotkeys:** Instantly updates the timer countdown to 10 seconds or 30 seconds for immediate testing/speed adjustments.

## 📝 License

This project is open-source and free to modify, distribute, or integrate into your own server frameworks.
