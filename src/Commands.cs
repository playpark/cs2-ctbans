using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using Nexd.MySQL;
using CounterStrikeSharp.API.Modules.Utils;

public partial class Plugin
{
    public void RegisterCommands()
    {
        foreach (var cmd in Config.Commands.CTBan.Split(','))
            AddCommand($"css_{cmd}", "Toggle build mode", BanCT);

        foreach (var cmd in Config.Commands.CTUnban.Split(','))
            AddCommand($"css_{cmd}", "Toggle build mode", UnbanCT);

        foreach (var cmd in Config.Commands.CTBanInfo.Split(','))
            AddCommand($"css_{cmd}", "Toggle build mode", InfobanCT);

        foreach (var cmd in Config.Commands.AddCTBan.Split(','))
            AddCommand($"css_{cmd}", "Ban a player from CT side by SteamID", AddCTBan);
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

        var SteamID = info.ArgByIndex(1);
        var TimeMinutes = info.ArgByIndex(2);
        var Reason = info.GetArg(3);
        string PlayerName = SteamID;
        bool playerFound = false;

        foreach (var find_player in Utilities.GetPlayers())
        {
            if (find_player.PlayerName == SteamID)
            {
                PlayerName = find_player.PlayerName;
                SteamID = find_player.SteamID.ToString();
                playerFound = true;
                break;
            }
            if (find_player.SteamID.ToString() == SteamID)
            {
                PlayerName = find_player.PlayerName;
                playerFound = true;
                break;
            }
        }

        if (!playerFound)
        {
            info.ReplyToCommand($"{Localizer["prefix"]} {Localizer["not_found"]}");
            info.ReplyToCommand($"css_ctban <PlayerName/SteamID> <Minutes> 'REASON'");
            return;
        }

        if (TimeMinutes is null or "" || !Utils.IsInt(TimeMinutes))
        {
            info.ReplyToCommand($"{Localizer["prefix"]} {Localizer["time_numbers"]}");
            info.ReplyToCommand($"css_ctban <PlayerName/SteamID> <Minutes> 'REASON'");
            return;
        }

        if (Reason == "")
            Reason = "none";

        int BanTime;
        if (TimeMinutes == "0") BanTime = 0;
        else BanTime = DateTime.UtcNow.AddMinutes(Convert.ToInt32(TimeMinutes)).GetUnixEpoch();

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
        MySqlQueryResult result = MySql!.ExecuteQuery($"SELECT * FROM {Config.Database.Table} WHERE steamid = '{SteamID}' ORDER BY id DESC LIMIT 1");

        var bannedplayer = Utilities.GetPlayerFromSteamId(ulong.Parse(SteamID));

        if (result.Rows == 0)
        {
            MySqlQueryValue values = new MySqlQueryValue()
            .Add("steamid", $"{SteamID}")
            .Add("name", $"{PlayerName}")
            .Add("start", $"{DateTime.UtcNow.GetUnixEpoch()}")
            .Add("end", $"{BanTime}")
            .Add("reason", $"{Reason}")
            .Add("admin_steamid", $"{adminSteamID}")
            .Add("admin_name", $"{adminName}");
            MySql.Table(Config.Database.Table).Insert(values);

            bannedplayer!.ChangeTeam(CsTeam.Terrorist);
            Server.PrintToChatAll($" {Localizer["prefix"]} {(TimeMinutes == "0" ? $"{Localizer["banned_announce_perma", bannedplayer.PlayerName, TimeMinutes, Reason]}" : $"{Localizer["banned_announce", bannedplayer.PlayerName, TimeMinutes, Reason]}")}");
        }
        else
        {
            long currentTime = DateTime.UtcNow.GetUnixEpoch();
            long endTime = Convert.ToInt64(result[0]["end"]);

            if (endTime == 0)
            {
                info.ReplyToCommand($"{Localizer["prefix"]} {Localizer["already_banned"]}");
                return;
            }

            if (currentTime > endTime)
            {
                MySqlQueryValue values = new MySqlQueryValue()
                .Add("steamid", $"{SteamID}")
                .Add("name", $"{PlayerName}")
                .Add("start", $"{DateTime.UtcNow.GetUnixEpoch()}")
                .Add("end", $"{BanTime}")
                .Add("reason", $"{Reason}")
                .Add("admin_steamid", $"{adminSteamID}")
                .Add("admin_name", $"{adminName}");
                MySql.Table(Config.Database.Table).Insert(values);

                Server.PrintToChatAll($" {Localizer["prefix"]} {(TimeMinutes == "0" ? $"{Localizer["banned_announce_perma", PlayerName, TimeMinutes, Reason]}" : $"{Localizer["banned_announce", PlayerName, TimeMinutes, Reason]}")}");
                bannedplayer!.ChangeTeam(CsTeam.Terrorist);
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
            info.ReplyToCommand($"css_unctban <SteamID>");
            return;
        }

        var SteamID = info.ArgByIndex(1);
        string PlayerName;
        bool playerFound = false;

        foreach (var find_player in Utilities.GetPlayers())
        {
            if (find_player.PlayerName == SteamID)
            {
                PlayerName = find_player.PlayerName;
                SteamID = find_player.SteamID.ToString();
                playerFound = true;
                break;
            }
            if (find_player.SteamID.ToString() == SteamID)
            {
                PlayerName = find_player.PlayerName;
                playerFound = true;
                break;
            }
        }

        if (!playerFound)
        {
            info.ReplyToCommand($"{Localizer["prefix"]} {Localizer["not_found"]}");
            info.ReplyToCommand($"css_unctban <PlayerName/SteamID>");
            return;
        }

        MySqlDb MySql = new MySqlDb(Config.Database.Host, Config.Database.Username, Config.Database.Password, Config.Database.Name);
        MySqlQueryResult result = MySql!.ExecuteQuery($"SELECT * FROM {Config.Database.Table} WHERE steamid = '{SteamID}' ORDER BY id DESC LIMIT 1");

        if (result.Rows == 0)
            info.ReplyToCommand($"{Localizer["prefix"]} {Localizer["not_banned"]}");
        else
        {
            MySql.ExecuteQuery($"DELETE FROM {Config.Database.Table} WHERE steamid = '{SteamID}' ORDER BY id DESC LIMIT 1");
            info.ReplyToCommand($"{Localizer["prefix"]} {Localizer["unbanned", SteamID]}");
        }
    }

    public void InfobanCT(CCSPlayerController? player, CommandInfo info)
    {
        if (string.IsNullOrEmpty(info.ArgString))
        {
            info.ReplyToCommand($"{Localizer["prefix"]} {Localizer["no_args"]}");
            info.ReplyToCommand($"css_ctbaninfo <SteamID>");
            return;
        }

        var SteamID = info.ArgByIndex(1);
        string PlayerName;
        bool playerFound = false;

        foreach (var find_player in Utilities.GetPlayers())
        {
            if (find_player.PlayerName == SteamID)
            {
                PlayerName = find_player.PlayerName;
                SteamID = find_player.SteamID.ToString();
                playerFound = true;
                break;
            }
            if (find_player.SteamID.ToString() == SteamID)
            {
                PlayerName = find_player.PlayerName;
                playerFound = true;
                break;
            }
        }

        if (!playerFound)
        {
            info.ReplyToCommand($"{Localizer["prefix"]} {Localizer["not_found"]}");
            info.ReplyToCommand($"css_ctbaninfo <PlayerName/SteamID>");
            return;
        }

        MySqlDb MySql = new MySqlDb(Config.Database.Host, Config.Database.Username, Config.Database.Password, Config.Database.Name);
        MySqlQueryResult result = MySql!.Table($"{Config.Database.Table}").Where(MySqlQueryCondition.New("steamid", "=", SteamID)).Select();

        if (result.Rows == 0)
            info.ReplyToCommand($"{Localizer["prefix"]} {Localizer["not_banned"]}");
        else
        {
            var unixtime = result.Get<int>(0, "end");
            string reason = result.Get<string>(0, "reason");

            var timeRemaining = DateTimeOffset.FromUnixTimeSeconds(unixtime) - DateTimeOffset.UtcNow;
            var timeRemainingFormatted = $"{timeRemaining.Days}d {timeRemaining.Hours}:{timeRemaining.Minutes:D2}:{timeRemaining.Seconds:D2}";

            if (timeRemaining.Seconds < 0)
                timeRemainingFormatted = "permanently banned";

            player!.PrintToChat(Localizer["ban_info_1"]);
            player!.PrintToChat(Localizer["ban_info_2", SteamID]);
            player!.PrintToChat(Localizer["ban_info_3", timeRemainingFormatted]);
            player!.PrintToChat(Localizer["ban_info_4", reason]);
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
            info.ReplyToCommand($"css_addctban <SteamID> <PlayerName> <Minutes> 'REASON'");
            return;
        }

        var SteamID = info.ArgByIndex(1);
        var PlayerName = info.ArgByIndex(2);
        var TimeMinutes = info.ArgByIndex(3);
        var Reason = info.GetArg(4);

        // Validate SteamID format (basic validation)
        if (string.IsNullOrEmpty(SteamID) || (!SteamID.StartsWith("STEAM_") && !SteamID.All(char.IsDigit)))
        {
            info.ReplyToCommand($"{Localizer["prefix"]} Invalid SteamID format");
            info.ReplyToCommand($"css_addctban <SteamID> <PlayerName> <Minutes> 'REASON'");
            return;
        }

        // Validate player name
        if (string.IsNullOrEmpty(PlayerName))
        {
            info.ReplyToCommand($"{Localizer["prefix"]} Player name cannot be empty");
            info.ReplyToCommand($"css_addctban <SteamID> <PlayerName> <Minutes> 'REASON'");
            return;
        }

        // Validate time
        if (TimeMinutes is null or "" || !Utils.IsInt(TimeMinutes))
        {
            info.ReplyToCommand($"{Localizer["prefix"]} {Localizer["time_numbers"]}");
            info.ReplyToCommand($"css_addctban <SteamID> <PlayerName> <Minutes> 'REASON'");
            return;
        }

        if (Reason == "")
            Reason = "none";

        int BanTime;
        if (TimeMinutes == "0") BanTime = 0;
        else BanTime = DateTime.UtcNow.AddMinutes(Convert.ToInt32(TimeMinutes)).GetUnixEpoch();

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
        MySqlQueryResult result = MySql!.ExecuteQuery($"SELECT * FROM {Config.Database.Table} WHERE steamid = '{SteamID}' ORDER BY id DESC LIMIT 1");

        // Check if player is online and get a reference to them if they are
        CCSPlayerController? bannedplayer = null;
        foreach (var find_player in Utilities.GetPlayers())
        {
            if (find_player.SteamID.ToString() == SteamID)
            {
                bannedplayer = find_player;
                break;
            }
        }

        if (result.Rows == 0)
        {
            MySqlQueryValue values = new MySqlQueryValue()
            .Add("steamid", $"{SteamID}")
            .Add("name", $"{PlayerName}")
            .Add("start", $"{DateTime.UtcNow.GetUnixEpoch()}")
            .Add("end", $"{BanTime}")
            .Add("reason", $"{Reason}")
            .Add("admin_steamid", $"{adminSteamID}")
            .Add("admin_name", $"{adminName}");
            MySql.Table(Config.Database.Table).Insert(values);

            // If player is online, move them to T team
            if (bannedplayer != null && bannedplayer.IsValid)
            {
                bannedplayer.ChangeTeam(CsTeam.Terrorist);
                Database.CheckIfIsBanned(bannedplayer);
            }

            info.ReplyToCommand($"{Localizer["prefix"]} {(TimeMinutes == "0" ? $"Permanently banned {PlayerName} ({SteamID}) from CT side. Reason: {Reason}" : $"Banned {PlayerName} ({SteamID}) from CT side for {TimeMinutes} minutes. Reason: {Reason}")}");
            Server.PrintToChatAll($" {Localizer["prefix"]} {(TimeMinutes == "0" ? $"{Localizer["banned_announce_perma", PlayerName, TimeMinutes, Reason]}" : $"{Localizer["banned_announce", PlayerName, TimeMinutes, Reason]}")}");
        }
        else
        {
            long currentTime = DateTime.UtcNow.GetUnixEpoch();
            long endTime = Convert.ToInt64(result[0]["end"]);

            if (endTime == 0)
            {
                info.ReplyToCommand($"{Localizer["prefix"]} {Localizer["already_banned"]}");
                return;
            }

            if (currentTime > endTime)
            {
                MySqlQueryValue values = new MySqlQueryValue()
                .Add("steamid", $"{SteamID}")
                .Add("name", $"{PlayerName}")
                .Add("start", $"{DateTime.UtcNow.GetUnixEpoch()}")
                .Add("end", $"{BanTime}")
                .Add("reason", $"{Reason}")
                .Add("admin_steamid", $"{adminSteamID}")
                .Add("admin_name", $"{adminName}");
                MySql.Table(Config.Database.Table).Insert(values);

                // If player is online, move them to T team
                if (bannedplayer != null && bannedplayer.IsValid)
                {
                    bannedplayer.ChangeTeam(CsTeam.Terrorist);
                    Database.CheckIfIsBanned(bannedplayer);
                }

                info.ReplyToCommand($"{Localizer["prefix"]} {(TimeMinutes == "0" ? $"Permanently banned {PlayerName} ({SteamID}) from CT side. Reason: {Reason}" : $"Banned {PlayerName} ({SteamID}) from CT side for {TimeMinutes} minutes. Reason: {Reason}")}");
                Server.PrintToChatAll($" {Localizer["prefix"]} {(TimeMinutes == "0" ? $"{Localizer["banned_announce_perma", PlayerName, TimeMinutes, Reason]}" : $"{Localizer["banned_announce", PlayerName, TimeMinutes, Reason]}")}");
            }
            else info.ReplyToCommand($"{Localizer["prefix"]} {Localizer["already_banned"]}");
        }
    }
}
