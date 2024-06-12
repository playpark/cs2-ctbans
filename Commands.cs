using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using Nexd.MySQL;
using CounterStrikeSharp.API.Modules.Utils;

namespace CTBans;

public partial class CTBans
{
    public bool PlayerHasPermission(CCSPlayerController? player)
    {
        if (!AdminManager.PlayerHasPermissions(player, $"{Config.Permission}"))
        {
            player!.PrintToChat(ReplaceColors($" {Config.Prefix} You do not have permission to use this command!"));
            return false;
        }
        return true;
    }

    [ConsoleCommand("css_ctsessionban", "ctban a player")]
    public void addsessionban(CCSPlayerController? player, CommandInfo info)
    {
        if (!PlayerHasPermission(player))
            return;

        var Player = info.ArgByIndex(1);
        var Reason = info.GetArg(2);

        if (Reason == null)
        {
            info.ReplyToCommand($" {Config.Prefix} Reason can not be a number!");
            info.ReplyToCommand($" {Config.Prefix} Example : css_ctsessionban <PlayerName> 'REASON'");
            return;
        }

        foreach (var find_player in Utilities.GetPlayers())
        {
            if (find_player.PlayerName.ToString() == Player)
            {
                info.ReplyToCommand($" {Config.Prefix} Player Name '{Player}' has been banned!");
            }
        }

        info.ReplyToCommand($" {Config.Prefix} You successful ban player {Player}");
        foreach (var find_player in Utilities.GetPlayers())
        {
            if (find_player.PlayerName.ToString() == Player)
            {
                find_player.PrintToChat($" {Config.Prefix} You are banned from {ChatColors.LightBlue}CT{ChatColors.Default} by admin {ChatColors.Red}{player!.PlayerName}{ChatColors.Default} for reason: {ChatColors.Gold}{Reason} ");
                Showinfo[find_player.Index] = 1;
                banned[find_player.Index] = true;
                reason[find_player.Index] = $"{Reason}";
                session[find_player.Index] = true;
                find_player.ChangeTeam(CsTeam.Terrorist);
            }
        }

    }

    [ConsoleCommand("css_ctban", "ctban a player")]
    public void addban(CCSPlayerController? player, CommandInfo info)
    {
        if (!PlayerHasPermission(player))
            return;

        var SteamID = info.ArgByIndex(1);
        var TimeMinutes = info.ArgByIndex(2);
        var Reason = info.GetArg(3);
        var Bannedby = "";

        if (info.ArgString == "" || info.ArgString == null)
        {
            info.ReplyToCommand(ReplaceColors($" {Config.Prefix} No args found!"));
            info.ReplyToCommand(ReplaceColors($" Example : css_ctban <PlayerName/SteamID> <Minutes> 'REASON'"));
            return;
        }

        if (player == null)
            Bannedby = "CONSOLE";
        else
            Bannedby = player.SteamID.ToString();

        foreach (var find_player in Utilities.GetPlayers())
        {
            if (find_player.PlayerName.ToString() == SteamID)
            {
                info.ReplyToCommand(ReplaceColors($" {Config.Prefix} Player found!"));
                SteamID = find_player.SteamID.ToString();
                var PlayerName = find_player.PlayerName;
            }
            else
            {
                if (SteamID == null || !IsInt(SteamID))
                {
                    info.ReplyToCommand(ReplaceColors($" {Config.Prefix} Player not found, Steamid must be number!"));
                    info.ReplyToCommand(ReplaceColors($" Example : css_ctban <PlayerName/SteamID> <Minutes> 'REASON'"));
                    return;
                }
            }
        }

        if (TimeMinutes == null || !IsInt(TimeMinutes))
        {
            info.ReplyToCommand(ReplaceColors($" {Config.Prefix} Time must be in hours!"));
            info.ReplyToCommand(ReplaceColors($" Example : css_ctban <PlayerName/SteamID> <Minutes> 'REASON'"));

            return;
        }

        else if (Reason == null || IsInt(Reason))
        {
            info.ReplyToCommand(ReplaceColors($" {Config.Prefix} Reason can not be a number!"));
            info.ReplyToCommand(ReplaceColors($" Example : css_ctban <PlayerName/SteamID> <Minutes> 'REASON'"));
            return;
        }

        else
        {
            var TimeToUTC = DateTime.UtcNow.AddMinutes(Convert.ToInt32(TimeMinutes)).GetUnixEpoch();
            var BanTime = 0;

            if (TimeMinutes == "0")
                BanTime = 0;

            else
                BanTime = DateTime.UtcNow.AddMinutes(Convert.ToInt32(TimeMinutes)).GetUnixEpoch();

            var timeRemaining = DateTimeOffset.FromUnixTimeSeconds(TimeToUTC) - DateTimeOffset.UtcNow;
            var timeRemainingFormatted = $"{timeRemaining.Days}d {timeRemaining.Hours:D2}:{timeRemaining.Minutes:D2}:{timeRemaining.Seconds:D2}";

            MySqlDb MySql = new MySqlDb(Config.DBHost, Config.DBUser, Config.DBPassword, Config.DBDatabase);

            MySqlQueryResult result = MySql!.ExecuteQuery($"SELECT * FROM {Config.DBTable} WHERE steamid = '{player!.SteamID}' ORDER BY id DESC LIMIT 1");

            if (result.Rows == 0)
            {
                MySqlQueryValue values = new MySqlQueryValue()
                .Add("steamid", $"{SteamID}")
                .Add("name", $"{Utilities.GetPlayerFromSteamId(ulong.Parse(SteamID)).PlayerName}")
                .Add("start", $"{DateTime.UtcNow.GetUnixEpoch()}")
                .Add("end", $"{BanTime}")
                .Add("reason", $"{Reason}")
                .Add("admin_steamid", $"{Bannedby}")
                .Add("admin_name", $"{Utilities.GetPlayerFromSteamId(ulong.Parse(Bannedby)).PlayerName}");
                MySql.Table(Config.DBTable).Insert(values);

                info.ReplyToCommand(ReplaceColors($" {Config.Prefix} You banned the player {SteamID} from CT"));
                foreach (var find_player in Utilities.GetPlayers())
                {
                    if(find_player.SteamID.ToString() == SteamID)
                    {
                        Server.PrintToChatAll(ReplaceColors($" {Config.Prefix} {ChatColors.LightPurple}{find_player.PlayerName}{ChatColors.White} has been banned from {ChatColors.LightBlue}CT{ChatColors.Default} for {TimeMinutes} minutes, reason: {ChatColors.Grey}{Reason}"));
                        find_player.ChangeTeam(CsTeam.Terrorist);
                    }
                }
            }
            else
            {
                long currentTime = DateTime.UtcNow.GetUnixEpoch();
                long endTime = Convert.ToInt64(result[0]["end"]);

                if (currentTime > endTime)
                {
                    MySqlQueryValue values = new MySqlQueryValue()
                    .Add("steamid", $"{SteamID}")
                    .Add("name", $"{Utilities.GetPlayerFromSteamId(ulong.Parse(SteamID)).PlayerName}")
                    .Add("start", $"{DateTime.UtcNow.GetUnixEpoch()}")
                    .Add("end", $"{BanTime}")
                    .Add("reason", $"{Reason}")
                    .Add("admin_steamid", $"{Bannedby}")
                    .Add("admin_name", $"{Utilities.GetPlayerFromSteamId(ulong.Parse(Bannedby)).PlayerName}");
                    MySql.Table(Config.DBTable).Insert(values);

                    info.ReplyToCommand(ReplaceColors($" {Config.Prefix} You banned the player {SteamID} from CT"));
                    foreach (var find_player in Utilities.GetPlayers())
                    {
                        if (find_player.SteamID.ToString() == SteamID)
                        {
                            Server.PrintToChatAll(ReplaceColors($" {Config.Prefix} {ChatColors.LightPurple}{find_player.PlayerName}{ChatColors.White} has been banned from {ChatColors.LightBlue}CT{ChatColors.Default} for {TimeMinutes} minutes, reason: {ChatColors.Grey}{Reason}"));
                            find_player.ChangeTeam(CsTeam.Terrorist);
                        }
                    }
                }
                else
                {
                    info.ReplyToCommand(ReplaceColors($" {Config.Prefix} This SteamID is already banned!"));
                }
            }
        }
    }

    [ConsoleCommand("css_unctban", "unctban a player")]
    [ConsoleCommand("css_ctunban", "unctban a player")]
    public void UnbanCT(CCSPlayerController? player, CommandInfo info)
    {
        if (!PlayerHasPermission(player))
            return;

        var SteamID = info.ArgByIndex(1);

        if (info.ArgString == "" || info.ArgString == null)
        {
            info.ReplyToCommand(ReplaceColors($" {Config.Prefix} No args found!"));
            info.ReplyToCommand(ReplaceColors($" Example : css_unctban <SteamID>"));
            return;
        }

        if (SteamID == null || !IsInt(SteamID))
        {
            info.ReplyToCommand(ReplaceColors($" {Config.Prefix} Steamid is must be number!"));
            info.ReplyToCommand($" Example : css_unctban <SteamID>");
            return;
        }

        MySqlDb MySql = new MySqlDb(Config.DBHost, Config.DBUser, Config.DBPassword, Config.DBDatabase);

        MySqlQueryResult result = MySql!.ExecuteQuery($"SELECT * FROM {Config.DBTable} WHERE steamid = '{player!.SteamID}' ORDER BY id DESC LIMIT 1");

        if (result.Rows == 0)
            info.ReplyToCommand(ReplaceColors($" {Config.Prefix} This steamid is not banned from CT!"));

        else
        {
            MySql.ExecuteQuery($"DELETE FROM {Config.DBTable} WHERE steamid = '{player!.SteamID}' ORDER BY id DESC LIMIT 1");
            info.ReplyToCommand(ReplaceColors($" {Config.Prefix} You unbanned player ({SteamID}) from CT!"));
        }

    }

    [ConsoleCommand("css_isctbanned", "info about a ctban")]
    [ConsoleCommand("css_isctban", "info about a ctban")]
    [ConsoleCommand("css_ctbancheck", "info about a ctban")]
    public void InfobanCT(CCSPlayerController? player, CommandInfo info)
    {
        if (!PlayerHasPermission(player))
            return;

        var SteamID = info.ArgByIndex(1);

        if (SteamID == null || !IsInt(SteamID))
        {
            info.ReplyToCommand($" {Config.Prefix} Steamid is must be number!");
            info.ReplyToCommand($" Example : css_isctbanned <SteamID>");
            return;
        }

        MySqlDb MySql = new MySqlDb(Config.DBHost, Config.DBUser, Config.DBPassword, Config.DBDatabase);

        MySqlQueryResult result = MySql!.Table($"{Config.DBTable}").Where(MySqlQueryCondition.New("steamid", "=", SteamID)).Select();

        if (result.Rows == 0)
            info.ReplyToCommand($" {Config.Prefix} This SteamID is not banned from CT!");

        else
        {
            var time = result.Get<int>(0, "end");
            string reason = result.Get<string>(0, "reason");

            var timeRemaining = DateTimeOffset.FromUnixTimeSeconds(time) - DateTimeOffset.UtcNow;
            var nowtimeis = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var timeRemainingFormatted =
            $"{timeRemaining.Days}d {timeRemaining.Hours}:{timeRemaining.Minutes:D2}:{timeRemaining.Seconds:D2}";
            player!.PrintToChat($" {ChatColors.Red}|-------------| {ChatColors.Default}Info about {SteamID} {ChatColors.Red}|-------------|");
            player!.PrintToChat($" {ChatColors.Default}SteamID {ChatColors.Red}{SteamID}{ChatColors.Default} is {ChatColors.Red}banned.");
            player!.PrintToChat($" {ChatColors.Default}Reason: {ChatColors.Red}{reason}{ChatColors.Default}.");
            player!.PrintToChat($" {ChatColors.Default}Time of ban: {ChatColors.Red}{timeRemainingFormatted}");
            player!.PrintToChat($" {ChatColors.Red}|-------------| {ChatColors.Default}Info about {SteamID} {ChatColors.Red}|-------------|");
        }
    }

}
