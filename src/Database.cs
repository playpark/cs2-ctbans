using CounterStrikeSharp.API.Core;
using Nexd.MySQL;

public class Database
{
    private static string table = "ctbans";
    private static string host = "localhost";
    private static string username = "user";
    private static string password = "password";
    private static string name = "database";
    private static int port = 3306;

    private static MySqlDb ConnectionString()
    {
        return new MySqlDb(host, username, password, name, port);
    }

    public static void Load()
    {
        try
        {
            table = Plugin.Instance.Config.Database.Table;
            host = Plugin.Instance.Config.Database.Host;
            username = Plugin.Instance.Config.Database.Username;
            password = Plugin.Instance.Config.Database.Password;
            name = Plugin.Instance.Config.Database.Name;
            port = Plugin.Instance.Config.Database.Port;

            MySqlDb MySql = ConnectionString();

            MySql.ExecuteNonQueryAsync(
                @$"CREATE TABLE IF NOT EXISTS `{table}`
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
        }
        catch (Exception ex)
        {
            Utils.WriteColor($"CT BANS - *[MYSQL ERROR WHILE LOADING: {ex.Message}]*", ConsoleColor.DarkRed);
        }
    }

    public static bool CheckBan(CCSPlayerController? player)
    {
        MySqlDb MySql = ConnectionString();

        MySqlQueryResult result = MySql!.ExecuteQuery($"SELECT * FROM {table} WHERE steamid = '{player!.SteamID}' ORDER BY id DESC LIMIT 1");
        if (result.Rows == 1)
            return true;

        return false;
    }

    public static int GetPlayerBanTime(CCSPlayerController? player)
    {
        MySqlDb MySql = ConnectionString();

        MySqlQueryResult result = MySql!.ExecuteQuery($"SELECT * FROM {table} WHERE steamid = '{player!.SteamID}' ORDER BY id DESC LIMIT 1");
        if (result.Rows == 1)
            return result.Get<int>(0, "end");

        return -1;
    }

    public static string GetPlayerBanReason(CCSPlayerController? player)
    {
        MySqlDb MySql = ConnectionString();

        MySqlQueryResult result = MySql!.ExecuteQuery($"SELECT * FROM {table} WHERE steamid = '{player!.SteamID}' ORDER BY id DESC LIMIT 1");
        if (result.Rows == 1)
            return $"{result.Get<string>(0, "reason")}";

        return "";
    }

    public static void CheckIfIsBanned(CCSPlayerController? player)
    {
        if (player == null)
            return;

        var client = player.Index;

        if (CheckBan(player) == true)
        {
            var timeRemaining = DateTimeOffset.FromUnixTimeSeconds(GetPlayerBanTime(player)) - DateTimeOffset.UtcNow;
            var timeCurrent = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var timeRemainingFormatted =
            $"{timeRemaining.Days}d {timeRemaining.Hours}:{timeRemaining.Minutes:D2}:{timeRemaining.Seconds:D2}";

            if (GetPlayerBanTime(player) == 0)
            {
                Plugin.banned[client] = true;
                Plugin.remaining[client] = $"permanent";
                Plugin.reason[client] = GetPlayerBanReason(player);
                return;
            }

            if (GetPlayerBanTime(player) < timeCurrent)
            {
                Plugin.banned[client] = false;
                Plugin.remaining[client] = null;
                Plugin.reason[client] = null;
            }
            else
            {
                Plugin.banned[client] = true;
                Plugin.remaining[client] = $"{timeRemainingFormatted}";
                Plugin.reason[client] = GetPlayerBanReason(player);
            }
        }
        else
        {
            Plugin.banned[client] = false;
            Plugin.remaining[client] = null;
            Plugin.reason[client] = null;
        }
    }
}