using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using Nexd.MySQL;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Commands.Targeting;

public partial class Plugin
{
    public void RegisterCommands()
    {
        foreach (var cmd in Config.Commands.CTBan.Split(','))
            AddCommand($"css_{cmd}", "Ban a player from CT side by name or SteamID64", BanCT);

        foreach (var cmd in Config.Commands.CTUnban.Split(','))
            AddCommand($"css_{cmd}", "Unban a player from CT side by name or SteamID64", UnbanCT);

        foreach (var cmd in Config.Commands.CTBanInfo.Split(','))
            AddCommand($"css_{cmd}", "Check a player's CT ban info by name or SteamID64", InfobanCT);

        foreach (var cmd in Config.Commands.AddCTBan.Split(','))
            AddCommand($"css_{cmd}", "Ban a player from CT side by name or SteamID64", AddCTBan);

        // Add a command for players to check ban status
        AddCommand("css_checkban", "Check CT ban status for yourself or another player by name or SteamID64", CheckOwnBan);
    }

    public void UnregisterCommands()
    {
        foreach (var cmd in Config.Commands.CTBan.Split(','))
            RemoveCommand($"css_{cmd}", BanCT);

        foreach (var cmd in Config.Commands.CTUnban.Split(','))
            RemoveCommand($"css_{cmd}", UnbanCT);

        foreach (var cmd in Config.Commands.CTBanInfo.Split(','))
            RemoveCommand($"css_{cmd}", InfobanCT);

        foreach (var cmd in Config.Commands.AddCTBan.Split(','))
            RemoveCommand($"css_{cmd}", AddCTBan);
    }

    public void BanCT(CCSPlayerController? player, CommandInfo info)
    {
        if (!Utils.HasPermission(player))
        {
            player?.PrintToChat($"{Localizer["prefix"]} {Localizer["no_permission"]}");
            return;
        }

        if (string.IsNullOrEmpty(info.ArgString))
        {
            info.ReplyToCommand($"{Localizer["prefix"]} {Localizer["no_args"]}");
            info.ReplyToCommand($"css_ctban <PlayerName/SteamID> <Minutes> 'REASON'");
            return;
        }

        var target = GetTarget(info);
        if (target == null)
        {
            info.ReplyToCommand($"css_ctban <PlayerName/SteamID> <Minutes> 'REASON'");
            return;
        }

        // Get the first target (we only support banning one player at a time)
        var targetPlayer = target.First();
        string SteamID = targetPlayer.SteamID.ToString();
        string PlayerName = targetPlayer.PlayerName;

        var TimeMinutes = info.ArgByIndex(2);
        var Reason = info.GetArg(3);

        if (TimeMinutes is null or "" || !Utils.IsInt(TimeMinutes))
        {
            info.ReplyToCommand($"{Localizer["prefix"]} {Localizer["time_numbers"]}");
            info.ReplyToCommand($"css_ctban <PlayerName/SteamID> <Minutes> 'REASON'");
            return;
        }

        if (Reason == "")
            Reason = "none";

        int BanDuration;
        if (TimeMinutes == "0") BanDuration = 0; // Permanent ban
        else BanDuration = Convert.ToInt32(TimeMinutes) * 60; // Convert minutes to seconds

        string adminSteamID;
        string adminName;
        if (player == null || !player.IsValid)
        {
            adminSteamID = "CONSOLE";
            adminName = "CONSOLE";
        }
        else
        {
            adminSteamID = player.SteamID.ToString();
            adminName = player.PlayerName;
        }

        MySqlDb MySql = new MySqlDb(Config.Database.Host, Config.Database.Username, Config.Database.Password, Config.Database.Name);
        MySqlQueryResult result = MySql!.ExecuteQuery($"SELECT * FROM {Config.Database.Table} WHERE steamid = '{SteamID}' AND status = 'ACTIVE' ORDER BY id DESC LIMIT 1");

        var bannedplayer = Utilities.GetPlayerFromSteamId(ulong.Parse(SteamID));

        if (result.Rows == 0)
        {
            MySqlQueryValue values = new MySqlQueryValue()
            .Add("steamid", $"{SteamID}")
            .Add("name", $"{PlayerName}")
            .Add("start", $"{DateTime.UtcNow.GetUnixEpoch()}")
            .Add("ban_duration", $"{BanDuration}")
            .Add("time_served", "0")
            .Add("reason", $"{Reason}")
            .Add("admin_steamid", $"{adminSteamID}")
            .Add("admin_name", $"{adminName}")
            .Add("status", "ACTIVE");
            MySql.Table(Config.Database.Table).Insert(values);

            if (bannedplayer != null && bannedplayer.IsValid)
            {
                bannedplayer.ChangeTeam(CsTeam.Terrorist);

                // Initialize tracking for the banned player
                var client = bannedplayer.Index;
                banned[client] = true;
                timeServed[client] = 0;
                isPlayerAlive[client] = false;
                aliveStartTime[client] = null;

                // Format the remaining time for display
                if (BanDuration == 0)
                {
                    remaining[client] = "permanent";
                }
                else
                {
                    remaining[client] = Database.FormatTimeRemainingFromSeconds(BanDuration);
                }

                reason[client] = Reason;

                _ = Database.CheckIfIsBannedAsync(bannedplayer);
            }

            Server.PrintToChatAll($" {Localizer["prefix"]} {(TimeMinutes == "0" ? $"{Localizer["banned_announce_perma", PlayerName, TimeMinutes, Reason]}" : $"{Localizer["banned_announce", PlayerName, TimeMinutes, Reason]}")}");
        }
        else
        {
            int timeServedValue = result.Get<int>(0, "time_served");
            int currentBanDuration = result.Get<int>(0, "ban_duration");

            if (currentBanDuration == 0)
            {
                info.ReplyToCommand($"{Localizer["prefix"]} {Localizer["already_banned"]}");
                return;
            }

            if (timeServedValue >= currentBanDuration)
            {
                // Previous ban has been served, create a new ban
                MySqlQueryValue values = new MySqlQueryValue()
                .Add("steamid", $"{SteamID}")
                .Add("name", $"{PlayerName}")
                .Add("start", $"{DateTime.UtcNow.GetUnixEpoch()}")
                .Add("ban_duration", $"{BanDuration}")
                .Add("time_served", "0")
                .Add("reason", $"{Reason}")
                .Add("admin_steamid", $"{adminSteamID}")
                .Add("admin_name", $"{adminName}")
                .Add("status", "ACTIVE");
                MySql.Table(Config.Database.Table).Insert(values);

                if (bannedplayer != null && bannedplayer.IsValid)
                {
                    bannedplayer.ChangeTeam(CsTeam.Terrorist);

                    // Initialize tracking for the banned player
                    var client = bannedplayer.Index;
                    banned[client] = true;
                    timeServed[client] = 0;
                    isPlayerAlive[client] = false;
                    aliveStartTime[client] = null;

                    // Format the remaining time for display
                    if (BanDuration == 0)
                    {
                        remaining[client] = "permanent";
                    }
                    else
                    {
                        remaining[client] = Database.FormatTimeRemainingFromSeconds(BanDuration);
                    }

                    reason[client] = Reason;

                    _ = Database.CheckIfIsBannedAsync(bannedplayer);
                }

                Server.PrintToChatAll($" {Localizer["prefix"]} {(TimeMinutes == "0" ? $"{Localizer["banned_announce_perma", PlayerName, TimeMinutes, Reason]}" : $"{Localizer["banned_announce", PlayerName, TimeMinutes, Reason]}")}");
            }
            else info.ReplyToCommand($"{Localizer["prefix"]} {Localizer["already_banned"]}");
        }
    }

    public void UnbanCT(CCSPlayerController? player, CommandInfo info)
    {
        if (!Utils.HasPermission(player))
        {
            player?.PrintToChat($"{Localizer["prefix"]} {Localizer["no_permission"]}");
            return;
        }

        if (string.IsNullOrEmpty(info.ArgString))
        {
            info.ReplyToCommand($"{Localizer["prefix"]} {Localizer["no_args"]}");
            info.ReplyToCommand($"css_unctban <PlayerName/SteamID64>");
            return;
        }

        var targetInfo = GetTargetOrSteamID64(info);
        if (targetInfo == null)
        {
            info.ReplyToCommand($"css_unctban <PlayerName/SteamID64>");
            return;
        }

        string SteamID = targetInfo.SteamID;

        MySqlDb MySql = new MySqlDb(Config.Database.Host, Config.Database.Username, Config.Database.Password, Config.Database.Name);
        MySqlQueryResult result = MySql!.ExecuteQuery($"SELECT * FROM {Config.Database.Table} WHERE steamid = '{SteamID}' AND status = 'ACTIVE' ORDER BY id DESC LIMIT 1");

        if (result.Rows == 0)
            info.ReplyToCommand($"{Localizer["prefix"]} {Localizer["not_banned"]}");
        else
        {
            // Update the status to UNBANNED instead of deleting the record
            MySql.ExecuteQuery($"UPDATE {Config.Database.Table} SET status = 'UNBANNED' WHERE steamid = '{SteamID}' AND status = 'ACTIVE' ORDER BY id DESC LIMIT 1");
            info.ReplyToCommand($"{Localizer["prefix"]} {Localizer["unbanned", SteamID]}");
        }
    }

    public void InfobanCT(CCSPlayerController? player, CommandInfo info)
    {
        if (string.IsNullOrEmpty(info.ArgString))
        {
            info.ReplyToCommand($"{Localizer["prefix"]} {Localizer["no_args"]}");
            info.ReplyToCommand($"css_ctbaninfo <PlayerName/SteamID64>");
            return;
        }

        var targetInfo = GetTargetOrSteamID64(info);
        if (targetInfo == null)
        {
            info.ReplyToCommand($"css_ctbaninfo <PlayerName/SteamID64>");
            return;
        }

        string SteamID = targetInfo.SteamID;

        MySqlDb MySql = new MySqlDb(Config.Database.Host, Config.Database.Username, Config.Database.Password, Config.Database.Name);
        MySqlQueryResult result = MySql!.Table($"{Config.Database.Table}").Where(MySqlQueryCondition.New("steamid", "=", SteamID)).Where(MySqlQueryCondition.New("status", "=", "ACTIVE")).Select();

        if (result.Rows == 0)
            info.ReplyToCommand($"{Localizer["prefix"]} {Localizer["not_banned"]}");
        else
        {
            int banDuration = result.Get<int>(0, "ban_duration");
            int timeServed = result.Get<int>(0, "time_served");
            string reason = result.Get<string>(0, "reason");

            string timeRemainingFormatted;
            if (banDuration == 0)
            {
                timeRemainingFormatted = "permanently banned";
            }
            else
            {
                int secondsRemaining = banDuration - timeServed;
                if (secondsRemaining <= 0)
                {
                    timeRemainingFormatted = "ban has been served";
                    // Update the ban status to EXPIRED instead of deleting
                    MySql.ExecuteNonQueryAsync($"UPDATE `{Config.Database.Table}` SET `status` = 'EXPIRED' WHERE steamid = '{SteamID}' AND status = 'ACTIVE' ORDER BY id DESC LIMIT 1");
                }
                else
                {
                    timeRemainingFormatted = Database.FormatTimeRemainingFromSeconds(secondsRemaining);
                }
            }

            player!.PrintToChat(Localizer["ban_info_1"]);
            player!.PrintToChat(Localizer["ban_info_2", SteamID]);
            player!.PrintToChat($"Player: {targetInfo.PlayerName}");
            player!.PrintToChat(Localizer["ban_info_3", timeRemainingFormatted]);
            player!.PrintToChat(Localizer["ban_info_4", reason]);
            player!.PrintToChat($"Time served: {TimeSpan.FromSeconds(timeServed).ToString(@"d\d\ hh\:mm\:ss")}");
            player!.PrintToChat(Localizer["ban_info_1"]);
        }
    }

    public void AddCTBan(CCSPlayerController? player, CommandInfo info)
    {
        if (!Utils.HasPermission(player))
        {
            player?.PrintToChat($"{Localizer["prefix"]} {Localizer["no_permission"]}");
            return;
        }

        if (string.IsNullOrEmpty(info.ArgString))
        {
            info.ReplyToCommand($"{Localizer["prefix"]} {Localizer["no_args"]}");
            info.ReplyToCommand($"css_addctban <PlayerName/SteamID64> <Minutes> 'REASON'");
            return;
        }

        var targetInfo = GetTargetOrSteamID64(info);
        if (targetInfo == null)
        {
            info.ReplyToCommand($"css_addctban <PlayerName/SteamID64> <Minutes> 'REASON'");
            return;
        }

        string SteamID = targetInfo.SteamID;
        string PlayerName = targetInfo.PlayerName;

        var TimeMinutes = info.ArgByIndex(2);
        var Reason = info.GetArg(3);

        // Validate time
        if (TimeMinutes is null or "" || !Utils.IsInt(TimeMinutes))
        {
            info.ReplyToCommand($"{Localizer["prefix"]} {Localizer["time_numbers"]}");
            info.ReplyToCommand($"css_addctban <PlayerName/SteamID64> <Minutes> 'REASON'");
            return;
        }

        if (Reason == "")
            Reason = "none";

        int BanDuration;
        if (TimeMinutes == "0") BanDuration = 0; // Permanent ban
        else BanDuration = Convert.ToInt32(TimeMinutes) * 60; // Convert minutes to seconds

        string adminSteamID;
        string adminName;
        if (player == null || !player.IsValid)
        {
            adminSteamID = "CONSOLE";
            adminName = "CONSOLE";
        }
        else
        {
            // Try to get admin's SteamID64
            ulong steamID64;
            if (TryGetSteamID64(player, out steamID64))
            {
                adminSteamID = steamID64.ToString();
            }
            else
            {
                adminSteamID = player.SteamID.ToString();
            }
            adminName = player.PlayerName;
        }

        MySqlDb MySql = new MySqlDb(Config.Database.Host, Config.Database.Username, Config.Database.Password, Config.Database.Name);
        MySqlQueryResult result = MySql!.ExecuteQuery($"SELECT * FROM {Config.Database.Table} WHERE steamid = '{SteamID}' AND (status = 'ACTIVE' OR status = 'EXPIRED' OR status = 'UNBANNED') ORDER BY id DESC LIMIT 1");

        // Use the player from targetInfo if available
        CCSPlayerController? bannedplayer = targetInfo.Player;

        if (result.Rows == 0)
        {
            MySqlQueryValue values = new MySqlQueryValue()
            .Add("steamid", $"{SteamID}")
            .Add("name", $"{PlayerName}")
            .Add("start", $"{DateTime.UtcNow.GetUnixEpoch()}")
            .Add("ban_duration", $"{BanDuration}")
            .Add("time_served", "0")
            .Add("reason", $"{Reason}")
            .Add("admin_steamid", $"{adminSteamID}")
            .Add("admin_name", $"{adminName}")
            .Add("status", "ACTIVE");
            MySql.Table(Config.Database.Table).Insert(values);

            // If player is online, move them to T team and initialize tracking
            if (bannedplayer != null && bannedplayer.IsValid)
            {
                bannedplayer.ChangeTeam(CsTeam.Terrorist);

                // Initialize tracking for the banned player
                var client = bannedplayer.Index;
                banned[client] = true;
                timeServed[client] = 0;
                isPlayerAlive[client] = false;
                aliveStartTime[client] = null;

                // Format the remaining time for display
                if (BanDuration == 0)
                {
                    remaining[client] = "permanent";
                }
                else
                {
                    remaining[client] = Database.FormatTimeRemainingFromSeconds(BanDuration);
                }

                reason[client] = Reason;

                _ = Database.CheckIfIsBannedAsync(bannedplayer);
            }

            info.ReplyToCommand($"{Localizer["prefix"]} {(TimeMinutes == "0" ? $"Permanently banned {PlayerName} ({SteamID}) from CT side. Reason: {Reason}" : $"Banned {PlayerName} ({SteamID}) from CT side for {TimeMinutes} minutes of alive time. Reason: {Reason}")}");
            Server.PrintToChatAll($" {Localizer["prefix"]} {(TimeMinutes == "0" ? $"{Localizer["banned_announce_perma", PlayerName, TimeMinutes, Reason]}" : $"{Localizer["banned_announce", PlayerName, TimeMinutes, Reason]}")}");
        }
        else
        {
            int timeServedValue = result.Get<int>(0, "time_served");
            int currentBanDuration = result.Get<int>(0, "ban_duration");

            if (currentBanDuration == 0)
            {
                info.ReplyToCommand($"{Localizer["prefix"]} {Localizer["already_banned"]}");
                return;
            }

            if (timeServedValue >= currentBanDuration)
            {
                // Previous ban has been served, create a new ban
                MySqlQueryValue values = new MySqlQueryValue()
                .Add("steamid", $"{SteamID}")
                .Add("name", $"{PlayerName}")
                .Add("start", $"{DateTime.UtcNow.GetUnixEpoch()}")
                .Add("ban_duration", $"{BanDuration}")
                .Add("time_served", "0")
                .Add("reason", $"{Reason}")
                .Add("admin_steamid", $"{adminSteamID}")
                .Add("admin_name", $"{adminName}")
                .Add("status", "ACTIVE");
                MySql.Table(Config.Database.Table).Insert(values);

                // If player is online, move them to T team and initialize tracking
                if (bannedplayer != null && bannedplayer.IsValid)
                {
                    bannedplayer.ChangeTeam(CsTeam.Terrorist);

                    // Initialize tracking for the banned player
                    var client = bannedplayer.Index;
                    banned[client] = true;
                    timeServed[client] = 0;
                    isPlayerAlive[client] = false;
                    aliveStartTime[client] = null;

                    // Format the remaining time for display
                    if (BanDuration == 0)
                    {
                        remaining[client] = "permanent";
                    }
                    else
                    {
                        remaining[client] = Database.FormatTimeRemainingFromSeconds(BanDuration);
                    }

                    reason[client] = Reason;

                    _ = Database.CheckIfIsBannedAsync(bannedplayer);
                }

                info.ReplyToCommand($"{Localizer["prefix"]} {(TimeMinutes == "0" ? $"Permanently banned {PlayerName} ({SteamID}) from CT side. Reason: {Reason}" : $"Banned {PlayerName} ({SteamID}) from CT side for {TimeMinutes} minutes of alive time. Reason: {Reason}")}");
                Server.PrintToChatAll($" {Localizer["prefix"]} {(TimeMinutes == "0" ? $"{Localizer["banned_announce_perma", PlayerName, TimeMinutes, Reason]}" : $"{Localizer["banned_announce", PlayerName, TimeMinutes, Reason]}")}");
            }
            else info.ReplyToCommand($"{Localizer["prefix"]} {Localizer["already_banned"]}");
        }
    }

    public void CheckOwnBan(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || !player.IsValid)
            return;

        // If no arguments, check own ban
        if (string.IsNullOrEmpty(info.ArgString))
        {
            var client = player.Index;

            // Force a refresh of the ban status from the database
            _ = Database.CheckIfIsBannedAsync(player);

            if (banned[client] == true)
            {
                // Show the ban information to the player
                ShowInfo(player);
                player.PrintToChat(Localizer["banned", remaining[client]!]);
            }
            else
            {
                player.PrintToChat(Localizer["not_banned"]);
            }
            return;
        }

        // Check if player has permission to check other players' bans
        if (!Utils.HasPermission(player))
        {
            player.PrintToChat($"{Localizer["prefix"]} {Localizer["no_permission"]}");
            return;
        }

        var targetInfo = GetTargetOrSteamID64(info);
        if (targetInfo == null)
        {
            player.PrintToChat($"css_checkban <PlayerName/SteamID64>");
            return;
        }

        string SteamID = targetInfo.SteamID;

        // Check if the player is online
        if (targetInfo.IsOnline)
        {
            // If player is online, check their ban status
            var targetClient = targetInfo.Player!.Index;
            _ = Database.CheckIfIsBannedAsync(targetInfo.Player);

            if (banned[targetClient] == true)
            {
                player.PrintToChat(Localizer["ban_info_1"]);
                player.PrintToChat(Localizer["ban_info_2", SteamID]);
                player.PrintToChat($"Player: {targetInfo.PlayerName}");
                player.PrintToChat(Localizer["ban_info_3", remaining[targetClient]!]);
                player.PrintToChat(Localizer["ban_info_4", reason[targetClient]!]);
                player.PrintToChat($"Time served: {TimeSpan.FromSeconds(timeServed[targetClient] ?? 0):d\\d\\ hh\\:mm\\:ss}");
                player.PrintToChat(Localizer["ban_info_1"]);
            }
            else
            {
                player.PrintToChat($"{Localizer["prefix"]} {targetInfo.PlayerName} {Localizer["not_banned"]}");
            }
            return;
        }

        // For offline players, query the database directly
        MySqlDb MySql = new MySqlDb(Config.Database.Host, Config.Database.Username, Config.Database.Password, Config.Database.Name);
        MySqlQueryResult result = MySql!.Table($"{Config.Database.Table}").Where(MySqlQueryCondition.New("steamid", "=", SteamID)).Where(MySqlQueryCondition.New("status", "=", "ACTIVE")).Select();

        if (result.Rows == 0)
            player.PrintToChat($"{Localizer["prefix"]} {Localizer["not_banned"]}");
        else
        {
            int banDuration = result.Get<int>(0, "ban_duration");
            int timeServed = result.Get<int>(0, "time_served");
            string reason = result.Get<string>(0, "reason");
            string playerName = result.Get<string>(0, "name");

            string timeRemainingFormatted;
            if (banDuration == 0)
            {
                timeRemainingFormatted = "permanently banned";
            }
            else
            {
                int secondsRemaining = banDuration - timeServed;
                if (secondsRemaining <= 0)
                {
                    timeRemainingFormatted = "ban has been served";
                    // Update the ban status to EXPIRED instead of deleting
                    MySql.ExecuteNonQueryAsync($"UPDATE `{Config.Database.Table}` SET `status` = 'EXPIRED' WHERE steamid = '{SteamID}' AND status = 'ACTIVE' ORDER BY id DESC LIMIT 1");
                }
                else
                {
                    timeRemainingFormatted = Database.FormatTimeRemainingFromSeconds(secondsRemaining);
                }
            }

            player.PrintToChat(Localizer["ban_info_1"]);
            player.PrintToChat(Localizer["ban_info_2", SteamID]);
            player.PrintToChat($"Player: {playerName}");
            player.PrintToChat(Localizer["ban_info_3", timeRemainingFormatted]);
            player.PrintToChat(Localizer["ban_info_4", reason]);
            player.PrintToChat($"Time served: {TimeSpan.FromSeconds(timeServed).ToString(@"d\d\ hh\:mm\:ss")}");
            player.PrintToChat(Localizer["ban_info_1"]);
        }
    }

    private TargetResult? GetTarget(CommandInfo command)
    {
        var matches = command.GetArgTargetResult(1);

        if (!matches.Any())
        {
            command.ReplyToCommand($"{Localizer["prefix"]} {Localizer["not_found"]}");
            return null;
        }

        if (command.GetArg(1).StartsWith('@'))
            return matches;

        if (matches.Count() == 1)
            return matches;

        command.ReplyToCommand($"{Localizer["prefix"]} Multiple targets found for \"{command.GetArg(1)}\".");
        return null;
    }

    private class TargetInfo
    {
        public string SteamID { get; set; } = string.Empty;
        public string PlayerName { get; set; } = string.Empty;
        public CCSPlayerController? Player { get; set; } = null;
        public bool IsOnline => Player != null && Player.IsValid;
    }

    private TargetInfo? GetTargetOrSteamID64(CommandInfo command)
    {
        string input = command.GetArg(1);

        // Check if input is a potential SteamID64 (17 digits)
        if (input.Length == 17 && input.All(char.IsDigit))
        {
            // Use SteamID64 directly without converting
            string steamID64 = input;

            // First check if player is online by converting their SteamID to SteamID64
            foreach (var player in Utilities.GetPlayers())
            {
                if (player == null || !player.IsValid)
                    continue;

                // Convert player's SteamID to SteamID64 for comparison
                ulong playerSteamID64;
                if (TryGetSteamID64(player, out playerSteamID64) && playerSteamID64.ToString() == steamID64)
                {
                    return new TargetInfo
                    {
                        SteamID = steamID64,
                        PlayerName = player.PlayerName,
                        Player = player
                    };
                }
            }

            // If not online, check database for player name
            MySqlDb MySql = new MySqlDb(Config.Database.Host, Config.Database.Username, Config.Database.Password, Config.Database.Name);
            MySqlQueryResult result = MySql!.ExecuteQuery($"SELECT name FROM {Config.Database.Table} WHERE steamid = '{steamID64}' ORDER BY id DESC LIMIT 1");

            if (result.Rows > 0)
            {
                string playerName = result.Get<string>(0, "name");
                return new TargetInfo
                {
                    SteamID = steamID64,
                    PlayerName = playerName
                };
            }

            // If not in database, return with just the SteamID64
            return new TargetInfo
            {
                SteamID = steamID64,
                PlayerName = "Unknown"
            };
        }

        // Try to find target using the standard GetTarget method
        var target = GetTarget(command);
        if (target != null && target.Any())
        {
            var player = target.First();
            if (player == null || !player.IsValid)
                return null;

            ulong steamID64;

            // Try to get SteamID64 from player
            if (TryGetSteamID64(player, out steamID64))
            {
                return new TargetInfo
                {
                    SteamID = steamID64.ToString(),
                    PlayerName = player.PlayerName,
                    Player = player
                };
            }
            else
            {
                // Fallback to original SteamID if conversion fails
                return new TargetInfo
                {
                    SteamID = player.SteamID.ToString(),
                    PlayerName = player.PlayerName,
                    Player = player
                };
            }
        }

        return null;
    }

    public bool TryGetSteamID64(CCSPlayerController player, out ulong steamID64)
    {
        try
        {
            if (player == null || !player.IsValid)
            {
                steamID64 = 0;
                return false;
            }

            // Get the SteamID in STEAM_X:Y:Z format
            string steamID = player.SteamID.ToString();

            // Parse the components
            string[] parts = steamID.Split(':');
            if (parts.Length == 3 && parts[0].StartsWith("STEAM_"))
            {
                ulong y = ulong.Parse(parts[1]);
                ulong z = ulong.Parse(parts[2]);

                // Calculate SteamID64
                steamID64 = 76561197960265728UL + (z * 2) + y;
                return true;
            }

            // If already in SteamID64 format
            if (ulong.TryParse(steamID, out steamID64) && steamID.Length == 17)
            {
                return true;
            }
        }
        catch
        {
            // Conversion failed
        }

        steamID64 = 0;
        return false;
    }
}
