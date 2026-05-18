using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Random Loot Timer", "Evo", "2.1.0")]
    [Description("Gives players completely random rust items with a customizable interval, chat toggle, global toggle, and telemetry panel.")]
    public class RandomLootTimer : RustPlugin
    {
        private Configuration config;
        private Timer lootTimer;
        private int currentCountdown;
        
        // Tracks players who opted out via /items
        private HashSet<ulong> optedOutPlayers = new HashSet<ulong>();
        
        // Dynamically compiled list of valid server items
        private List<ItemDefinition> validItemDefinitions = new List<ItemDefinition>();

        // Analytics metrics
        private DateTime pluginStartTime;
        private int totalCyclesCompleted = 0;
        private int totalItemsDistributed = 0;

        #region Configuration

        private class Configuration
        {
            public int IntervalSeconds = 10;
            public int DefaultMinAmount = 1;
            public int DefaultMaxAmount = 5;
            public bool EnabledGlobally = true;
            public bool EnableChatMessages = true;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) LoadDefaultConfig();
            }
            catch
            {
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig() => config = new Configuration();
        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion

        #region Hooks & Initialization

        private void Init()
        {
            permission.RegisterPermission("randomloottimer.admin", this);
        }

        private void OnServerInitialized()
        {
            pluginStartTime = DateTime.UtcNow;
            CompileValidItemsPool();
            currentCountdown = config.IntervalSeconds;
            StartLootLoop();
        }

        private void Unload()
        {
            lootTimer?.Destroy();
            foreach (var player in BasePlayer.activePlayerList)
            {
                DestroyCountdownGui(player);
                CuiHelper.DestroyUi(player, "LootAdminGui");
            }
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (config.EnabledGlobally && !optedOutPlayers.Contains(player.userID))
            {
                CreateCountdownGui(player);
            }
        }

        #endregion

        #region Dynamic Item Filtering Logic

        private void CompileValidItemsPool()
        {
            validItemDefinitions.Clear();

            // Scrape Rust's actual database directly
            var allItems = ItemManager.GetItemDefinitions();
            if (allItems == null) return;

            foreach (var item in allItems)
            {
                if (item == null) continue;
                
                string shortname = item.shortname;

                // Strip away items that are broken, unspawnable, or server-crashing entities
                if (string.IsNullOrEmpty(shortname) || 
                    shortname.Contains("mod.car") || 
                    shortname.Contains("chassis") || 
                    shortname.Contains("test") ||
                    shortname.Contains("generator.static") ||
                    (item.condition.enabled && item.condition.max <= 0))
                {
                    continue;
                }

                // Filter categories to only fun, usable real loot
                if (item.category == ItemCategory.Weapon ||
                    item.category == ItemCategory.Medical ||
                    item.category == ItemCategory.Attire ||
                    item.category == ItemCategory.Tool ||
                    item.category == ItemCategory.Component ||
                    item.category == ItemCategory.Ammunition ||
                    item.category == ItemCategory.Resources ||
                    item.category == ItemCategory.Items)
                {
                    validItemDefinitions.Add(item);
                }
            }

            Puts($"[Loot Timer] Compiled {validItemDefinitions.Count} high-quality game items for distribution!");
        }

        #endregion

        #region Core Loop & Giving Rewards

        private void StartLootLoop()
        {
            lootTimer?.Destroy();
            lootTimer = timer.Repeat(1f, 0, () =>
            {
                if (!config.EnabledGlobally)
                {
                    // Global pause: hide all active count panels instantly
                    UpdateCountdownGuiAll();
                    return;
                }

                currentCountdown--;

                if (currentCountdown <= 0)
                {
                    GiveRandomLoot();
                    currentCountdown = config.IntervalSeconds;
                }

                UpdateCountdownGuiAll();
            });
        }

        private void GiveRandomLoot()
        {
            if (validItemDefinitions.Count == 0) return;

            int cycleItemCount = 0;
            int targetedPlayersCount = 0;

            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player == null || !player.IsConnected || optedOutPlayers.Contains(player.userID)) 
                    continue;

                var randomDefinition = validItemDefinitions[UnityEngine.Random.Range(0, validItemDefinitions.Count)];
                
                // Limit massive items if not normally stackable
                int maxAmount = randomDefinition.stackable > 1 ? config.DefaultMaxAmount : 1;
                int amount = UnityEngine.Random.Range(config.DefaultMinAmount, maxAmount + 1);

                Item item = ItemManager.Create(randomDefinition, amount, 0UL);
                if (item != null)
                {
                    player.GiveItem(item);
                    cycleItemCount += amount;
                    targetedPlayersCount++;

                    if (config.EnableChatMessages)
                    {
                        player.ChatMessage($"<color=#FFA500>[Loot Roulette]</color> You won <color=#55FF55>{amount}x {randomDefinition.displayName.english}</color>!");
                    }
                }
            }

            if (targetedPlayersCount > 0)
            {
                totalCyclesCompleted++;
                totalItemsDistributed += cycleItemCount;
            }
        }

        #endregion

        #region Player Commands

        [ChatCommand("items")]
        private void CmdToggleItems(BasePlayer player, string command, string[] args)
        {
            if (optedOutPlayers.Contains(player.userID))
            {
                optedOutPlayers.Remove(player.userID);
                player.ChatMessage("<color=#FFA500>[Loot Roulette]</color> You have <color=#55FF55>ENABLED</color> random loot drops and countdowns.");
                if (config.EnabledGlobally)
                {
                    CreateCountdownGui(player);
                }
            }
            else
            {
                optedOutPlayers.Add(player.userID);
                player.ChatMessage("<color=#FFA500>[Loot Roulette]</color> You have <color=#FF5555>DISABLED</color> drops. Use <color=#FFA500>/items</color> to opt back in.");
                DestroyCountdownGui(player);
            }
        }

        #endregion

        #region UI - Countdown Overlay

        private void CreateCountdownGui(BasePlayer player)
        {
            DestroyCountdownGui(player);
            if (!config.EnabledGlobally || optedOutPlayers.Contains(player.userID)) return;

            var elements = new CuiElementContainer();
            string panelName = "LootCountdownGui";

            elements.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0.5" },
                RectTransform = { AnchorMin = "0.42 0.92", AnchorMax = "0.58 0.96" }
            }, "Hud", panelName);

            elements.Add(new CuiLabel
            {
                Text = { Text = $"Next Loot In: {currentCountdown}s", FontSize = 13, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, panelName, "CountdownText");

            CuiHelper.AddUi(player, elements);
        }

        private void UpdateCountdownGuiAll()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (optedOutPlayers.Contains(player.userID) || !config.EnabledGlobally)
                {
                    DestroyCountdownGui(player);
                    continue;
                }
                CreateCountdownGui(player);
            }
        }

        private void DestroyCountdownGui(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "LootCountdownGui");
        }

        #endregion

        #region UI - Admin Configuration Menu (Dashboard Cockpit)

        [ChatCommand("lootconfig")]
        private void CmdLootConfig(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "randomloottimer.admin"))
            {
                player.ChatMessage("You do not have permission to use this command.");
                return;
            }
            OpenAdminGui(player);
        }

        private void OpenAdminGui(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "LootAdminGui");
            var elements = new CuiElementContainer();

            // Background Outer Canvas
            elements.Add(new CuiPanel
            {
                Image = { Color = "0.08 0.08 0.08 0.98" },
                RectTransform = { AnchorMin = "0.2 0.2", AnchorMax = "0.8 0.8" },
                CursorEnabled = true
            }, "Overlay", "LootAdminGui");

            // Header Container Banner
            elements.Add(new CuiPanel
            {
                Image = { Color = "0.15 0.15 0.15 1" },
                RectTransform = { AnchorMin = "0 0.88", AnchorMax = "1 1" }
            }, "LootAdminGui", "HeaderPanel");

            elements.Add(new CuiLabel
            {
                Text = { Text = "LOOT TIMER TELEMETRY & MANAGEMENT SYSTEM", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 0.6 0 1" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, "HeaderPanel");

            // --- LEFT HALF: CONTROLS & COMMANDS ---
            // Timer Settings Headers
            elements.Add(new CuiLabel
            {
                Text = { Text = "OPERATIONAL STATUS CONTROLS", FontSize = 12, Align = TextAnchor.UpperLeft, Color = "0.7 0.7 0.7 1" },
                RectTransform = { AnchorMin = "0.05 0.75", AnchorMax = "0.45 0.82" }
            }, "LootAdminGui");

            // Toggle Global System Button
            string globalStatus = config.EnabledGlobally ? "SYSTEM: <color=#55FF55>ACTIVE</color>" : "SYSTEM: <color=#FF5555>STOPPED</color>";
            elements.Add(new CuiButton
            {
                Button = { Command = "loot_toggle_global", Color = "0.15 0.15 0.15 1" },
                RectTransform = { AnchorMin = "0.05 0.62", AnchorMax = "0.45 0.72" },
                Text = { Text = globalStatus, Align = TextAnchor.MiddleCenter, FontSize = 13 }
            }, "LootAdminGui");

            // Toggle Chat Notification Button
            string chatStatus = config.EnableChatMessages ? "NOTIFY CHAT: <color=#55FF55>ON</color>" : "NOTIFY CHAT: <color=#FF5555>OFF</color>";
            elements.Add(new CuiButton
            {
                Button = { Command = "loot_toggle_chat", Color = "0.15 0.15 0.15 1" },
                RectTransform = { AnchorMin = "0.05 0.50", AnchorMax = "0.45 0.60" },
                Text = { Text = chatStatus, Align = TextAnchor.MiddleCenter, FontSize = 13 }
            }, "LootAdminGui");

            // Quick Interval Loop Swaps
            elements.Add(new CuiLabel
            {
                Text = { Text = $"CURRENT INTERVAL: {config.IntervalSeconds} SECONDS", FontSize = 11, Align = TextAnchor.MiddleLeft, Color = "0.7 0.7 0.7 1" },
                RectTransform = { AnchorMin = "0.05 0.40", AnchorMax = "0.45 0.46" }
            }, "LootAdminGui");

            elements.Add(new CuiButton
            {
                Button = { Command = "loot_set_timer 10", Color = "0.2 0.4 0.2 1" },
                RectTransform = { AnchorMin = "0.05 0.28", AnchorMax = "0.23 0.38" },
                Text = { Text = "Set 10s", Align = TextAnchor.MiddleCenter, FontSize = 12 }
            }, "LootAdminGui");

            elements.Add(new CuiButton
            {
                Button = { Command = "loot_set_timer 30", Color = "0.2 0.2 0.4 1" },
                RectTransform = { AnchorMin = "0.27 0.28", AnchorMax = "0.45 0.38" },
                Text = { Text = "Set 30s", Align = TextAnchor.MiddleCenter, FontSize = 12 }
            }, "LootAdminGui");


            // --- RIGHT HALF: STATS & ANALYTICS ---
            // Header for Metrics
            elements.Add(new CuiLabel
            {
                Text = { Text = "LIVE METRICS & ANALYTICS", FontSize = 12, Align = TextAnchor.UpperLeft, Color = "0.7 0.7 0.7 1" },
                RectTransform = { AnchorMin = "0.55 0.75", AnchorMax = "0.95 0.82" }
            }, "LootAdminGui");

            // Stats Block Card panel backplate
            elements.Add(new CuiPanel
            {
                Image = { Color = "0.05 0.05 0.05 0.9" },
                RectTransform = { AnchorMin = "0.55 0.22", AnchorMax = "0.95 0.72" }
            }, "LootAdminGui", "StatsCard");

            // Calculate live status telemetry
            TimeSpan uptime = DateTime.UtcNow - pluginStartTime;
            string uptimeStr = string.Format("{0:D2}h {1:D2}m {2:D2}s", uptime.Hours, uptime.Minutes, uptime.Seconds);
            int activePlayers = BasePlayer.activePlayerList.Count;
            int optedInCount = activePlayers - optedOutPlayers.Count;

            string metricsText = 
                $"<b>Runtime Performance Stats</b>\n\n" +
                $"• <b>Server Uptime:</b>  <color=#FFA500>{uptimeStr}</color>\n" +
                $"• <b>Cycles Ran:</b>     <color=#FFA500>{totalCyclesCompleted}</color>\n" +
                $"• <b>Items Given:</b>    <color=#FFA500>{totalItemsDistributed}</color>\n\n" +
                $"• <b>Opted-In Users:</b> <color=#55FF55>{optedInCount}</color> / {activePlayers}\n" +
                $"• <b>Opted-Out Users:</b> <color=#FF5555>{optedOutPlayers.Count}</color>\n" +
                $"• <b>Indexed Pool Size:</b> <color=#55FF55>{validItemDefinitions.Count} Items</color>";

            elements.Add(new CuiLabel
            {
                Text = { Text = metricsText, FontSize = 12, Align = TextAnchor.UpperLeft, Color = "0.9 0.9 0.9 1" },
                RectTransform = { AnchorMin = "0.05 0.05", AnchorMax = "0.95 0.95" }
            }, "StatsCard");


            // Footer / Exit Button
            elements.Add(new CuiButton
            {
                Button = { Command = "loot_close_gui", Color = "0.7 0.2 0.2 1" },
                RectTransform = { AnchorMin = "0.4 0.05", AnchorMax = "0.6 0.13" },
                Text = { Text = "DISMISS", Align = TextAnchor.MiddleCenter, FontSize = 12 }
            }, "LootAdminGui");

            CuiHelper.AddUi(player, elements);
        }

        #endregion

        #region Console Commands (GUI Callbacks)

        [ConsoleCommand("loot_toggle_global")]
        private void CcmdToggleGlobal(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !permission.UserHasPermission(player.UserIDString, "randomloottimer.admin")) return;

            config.EnabledGlobally = !config.EnabledGlobally;
            SaveConfig();
            
            // Clean up visual elements immediately across all connected users if system is toggled off
            UpdateCountdownGuiAll();
            OpenAdminGui(player);
            
            player.ChatMessage($"[Loot Config] Global activation status updated to: {config.EnabledGlobally}");
        }

        [ConsoleCommand("loot_toggle_chat")]
        private void CcmdToggleChat(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !permission.UserHasPermission(player.UserIDString, "randomloottimer.admin")) return;

            config.EnableChatMessages = !config.EnableChatMessages;
            SaveConfig();
            OpenAdminGui(player);

            player.ChatMessage($"[Loot Config] Chat notifications updated to: {config.EnableChatMessages}");
        }

        [ConsoleCommand("loot_set_timer")]
        private void CcmdSetTimer(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !permission.UserHasPermission(player.UserIDString, "randomloottimer.admin")) return;

            if (arg.HasArgs(1) && int.TryParse(arg.Args[0], out int newSeconds))
            {
                config.IntervalSeconds = newSeconds;
                currentCountdown = newSeconds;
                SaveConfig();
                StartLootLoop();
                OpenAdminGui(player);
            }
        }

        [ConsoleCommand("loot_close_gui")]
        private void CcmdCloseGui(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            CuiHelper.DestroyUi(player, "LootAdminGui");
        }

        #endregion
    }
}