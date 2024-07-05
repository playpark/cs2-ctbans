using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using Nexd.MySQL;
using CounterStrikeSharp.API.Modules.Utils;

namespace CTBans;

public static class GetUnixTime
{
    public static int GetUnixEpoch(this DateTime dateTime)
    {
        var unixTime = dateTime.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return (int)unixTime.TotalSeconds;
    }
}

public partial class CTBans
{
    public bool PermissionCheck(CCSPlayerController? player)
    {
        if (!AdminManager.PlayerHasPermissions(player, $"{Config.Permission}"))
        {
            player?.PrintToChat($"{Localizer["prefix"]} {Localizer["no_permission"]}");
            return true;
        }
        return false;
    }

    public void BanCT(CCSPlayerController? player, CommandInfo info)
    {
        if (PermissionCheck(player))
            return;

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

        if (TimeMinutes is null or "" || !IsInt(TimeMinutes))
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

        MySqlDb MySql = new MySqlDb(Config.DBHost, Config.DBUser, Config.DBPassword, Config.DBDatabase);
        MySqlQueryResult result = MySql!.ExecuteQuery($"SELECT * FROM {Config.DBTable} WHERE steamid = '{SteamID}' ORDER BY id DESC LIMIT 1");

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
            MySql.Table(Config.DBTable).Insert(values);

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
                MySql.Table(Config.DBTable).Insert(values);

                Server.PrintToChatAll($" {Localizer["prefix"]} {(TimeMinutes == "0" ? $"{Localizer["banned_announce_perma", PlayerName, TimeMinutes, Reason]}" : $"{Localizer["banned_announce", PlayerName, TimeMinutes, Reason]}")}");
                bannedplayer!.ChangeTeam(CsTeam.Terrorist);
            }
            else info.ReplyToCommand($"{Localizer["prefix"]} {Localizer["already_banned"]}");
        }
    }

    public void UnbanCT(CCSPlayerController? player, CommandInfo info)
    {
        if (PermissionCheck(player))
            return;

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

        MySqlDb MySql = new MySqlDb(Config.DBHost, Config.DBUser, Config.DBPassword, Config.DBDatabase);
        MySqlQueryResult result = MySql!.ExecuteQuery($"SELECT * FROM {Config.DBTable} WHERE steamid = '{SteamID}' ORDER BY id DESC LIMIT 1");

        if (result.Rows == 0)
            info.ReplyToCommand($"{Localizer["prefix"]} {Localizer["not_banned"]}");
        else
        {
            MySql.ExecuteQuery($"DELETE FROM {Config.DBTable} WHERE steamid = '{SteamID}' ORDER BY id DESC LIMIT 1");
            info.ReplyToCommand($"{Localizer["prefix"]} {Localizer["unbanned", SteamID]}");
        }
    }

    public void InfobanCT(CCSPlayerController? player, CommandInfo info)
    {
        if (PermissionCheck(player))
            return;

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

        MySqlDb MySql = new MySqlDb(Config.DBHost, Config.DBUser, Config.DBPassword, Config.DBDatabase);
        MySqlQueryResult result = MySql!.Table($"{Config.DBTable}").Where(MySqlQueryCondition.New("steamid", "=", SteamID)).Select();

        if (result.Rows == 0)
            info.ReplyToCommand($"{Localizer["prefix"]} {Localizer["not_banned"]}");
        else
        {
            var time = result.Get<int>(0, "end");
            string reason = result.Get<string>(0, "reason");

            var timeRemaining = DateTimeOffset.FromUnixTimeSeconds(time) - DateTimeOffset.UtcNow;
            var timeRemainingFormatted = $"{timeRemaining.Days}d {timeRemaining.Hours}:{timeRemaining.Minutes:D2}:{timeRemaining.Seconds:D2}";
            if (time == 0) timeRemainingFormatted = "permanently banned";
            player!.PrintToChat(Localizer["ban_info_1"]);
            player!.PrintToChat(Localizer["ban_info_2", SteamID]);
            player!.PrintToChat(Localizer["ban_info_3", timeRemainingFormatted]);
            player!.PrintToChat(Localizer["ban_info_4", reason]);
            player!.PrintToChat(Localizer["ban_info_1"]);
        }
    }
}
