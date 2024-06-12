using CounterStrikeSharp.API.Core;
using Nexd.MySQL;
using System.Text.RegularExpressions;

namespace CTBans;

public partial class CTBans
{
    private bool IsInt(string sVal)
    {
        foreach (char c in sVal)
        {
            int iN = (int)c;
            if ((iN > 57) || (iN < 48))
                return false;
        }
        return true;
    }


    #region Replace Chat Colors
    static string ReplaceColors(string input)
    {
        string[] colorPatterns = {
            "{default}", "{white}", "{darkred}", "{green}", "{lightyellow}",
            "{lightblue}", "{olive}", "{lime}", "{red}", "{lightpurple}",
            "{purple}", "{grey}", "{yellow}", "{gold}", "{silver}",
            "{blue}", "{darkblue}", "{bluegrey}", "{magenta}", "{lightred}",
            "{orange}"
        };
        string[] colorReplacements = {
            "\x01", "\x01", "\x02", "\x04", "\x09", "\x0B", "\x05",
            "\x06", "\x07", "\x03", "\x0E", "\x08", "\x09", "\x10",
            "\x0A", "\x0B", "\x0C", "\x0A", "\x0E", "\x0F", "\x10"
        };
        for (var i = 0; i < colorPatterns.Length; i++)
            input = input.Replace(colorPatterns[i], colorReplacements[i]);
        return input;
    }
    #endregion

    static void WriteColor(string message, ConsoleColor color)
    {
        var pieces = Regex.Split(message, @"(\[[^\]]*\])");

        for (int i = 0; i < pieces.Length; i++)
        {
            string piece = pieces[i];

            if (piece.StartsWith("[") && piece.EndsWith("]"))
            {
                Console.ForegroundColor = color;
                piece = piece.Substring(1, piece.Length - 2);
            }

            Console.Write(piece);
            Console.ResetColor();
        }

        Console.WriteLine();
    }
    public void CreateDatabase()
    {
        try
        {
            MySqlDb MySql = new MySqlDb(Config.DBHost, Config.DBUser, Config.DBPassword, Config.DBDatabase);

            MySql.ExecuteNonQueryAsync(
                @$"CREATE TABLE IF NOT EXISTS `{Config.DBTable}`
                (
                    `id` INT AUTO_INCREMENT PRIMARY KEY, 
                    `steamid` VARCHAR(32) NOT NULL, 
                    `name` VARCHAR(32) NULL, 
                    `start` BIGINT NOT NULL, 
                    `end` BIGINT NOT NULL, 
                    `reason` VARCHAR(32) NOT NULL, 
                    `admin_steamid` VARCHAR(32) NOT NULL, 
                    `admin_name` VARCHAR(32) NULL
                );");

            WriteColor($"CT BANS - *[MySQL Table {Config.DBTable} Created]", ConsoleColor.Green);
            WriteColor($"CT BANS - *[MySQL {Config.DBHost} Connected]", ConsoleColor.Green);
        }
        catch (Exception ex)
        {
            WriteColor($"CT BANS - *[MYSQL ERROR WHILE LOADING: {ex.Message}]*", ConsoleColor.DarkRed);
        }
    }
    public bool CheckBan(CCSPlayerController? player)
    {
        MySqlDb MySql = new MySqlDb(Config.DBHost, Config.DBUser, Config.DBPassword, Config.DBDatabase);

        MySqlQueryResult result = MySql!.ExecuteQuery($"SELECT * FROM {Config.DBTable} WHERE steamid = '{player!.SteamID}' ORDER BY id DESC LIMIT 1");
        if (result.Rows == 1)
            return true;

        return false;
    }
    public int GetPlayerBanTime(CCSPlayerController? player)
    {
        MySqlDb MySql = new MySqlDb(Config.DBHost, Config.DBUser, Config.DBPassword, Config.DBDatabase);

        MySqlQueryResult result = MySql!.ExecuteQuery($"SELECT * FROM {Config.DBTable} WHERE steamid = '{player!.SteamID}' ORDER BY id DESC LIMIT 1");
        if (result.Rows == 1)
            return result.Get<int>(0, "end");

        return -1;
    }
    public string GetPlayerBanReason(CCSPlayerController? player)
    {
        MySqlDb MySql = new MySqlDb(Config.DBHost, Config.DBUser, Config.DBPassword, Config.DBDatabase);

        MySqlQueryResult result = MySql!.ExecuteQuery($"SELECT * FROM {Config.DBTable} WHERE steamid = '{player!.SteamID}' ORDER BY id DESC LIMIT 1");
        if (result.Rows == 1)
            return $"{result.Get<string>(0, "reason")}";

        return "";
    }
    public void CheckIfIsBanned(CCSPlayerController? player) 
    {
        if (player == null)
            return;

        var client = player.Index;

        if (CheckBan(player) == true)
        {
            var timeRemaining = DateTimeOffset.FromUnixTimeSeconds(GetPlayerBanTime(player)) - DateTimeOffset.UtcNow;
            var nowtimeis = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var timeRemainingFormatted =
            $"{timeRemaining.Days}d {timeRemaining.Hours}:{timeRemaining.Minutes:D2}:{timeRemaining.Seconds:D2}";

            if (GetPlayerBanTime(player) < nowtimeis)
            {
                banned[client] = false;
                remaining[client] = null;
                reason[client] = null;
            }

            else
            {
                banned[client] = true;
                remaining[client] = $"{timeRemainingFormatted}";
                reason[client] = GetPlayerBanReason(player);
            }
        }

        else
        {
            if (session[client] == true)
            {
                banned[client] = true;
            }

            else
            {
                banned[client] = false;
                remaining[client] = null;
                reason[client] = null;
            }
        }

    }

}
