using CounterStrikeSharp.API.Core;
using Nexd.MySQL;
using System;
using System.Threading.Tasks;

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

    // Helper method to format time remaining in a concise way
    public static string FormatTimeRemaining(TimeSpan timeRemaining)
    {
        if (timeRemaining.Days > 0)
        {
            return $"{timeRemaining.Days}d {timeRemaining.Hours}h";
        }
        else if (timeRemaining.Hours > 0)
        {
            return $"{timeRemaining.Hours}h {timeRemaining.Minutes}m";
        }
        else if (timeRemaining.Minutes > 0)
        {
            return $"{timeRemaining.Minutes}m {timeRemaining.Seconds}s";
        }
        else
        {
            return $"{timeRemaining.Seconds}s";
        }
    }

    // Helper method to format time remaining from seconds
    public static string FormatTimeRemainingFromSeconds(int secondsRemaining)
    {
        if (secondsRemaining <= 0)
            return "0s";

        TimeSpan timeRemaining = TimeSpan.FromSeconds(secondsRemaining);
        return FormatTimeRemaining(timeRemaining);
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
                    `ban_duration` INT NOT NULL, 
                    `time_served` INT NOT NULL DEFAULT 0, 
                    `reason` VARCHAR(32) NOT NULL, 
                    `admin_steamid` VARCHAR(32) NOT NULL, 
                    `admin_name` VARCHAR(32) NULL,
                    `status` VARCHAR(10) NOT NULL DEFAULT 'ACTIVE'
                );");

            // Check if we need to migrate existing data
            MySqlQueryResult columnCheck = MySql.ExecuteQuery($"SHOW COLUMNS FROM `{table}` LIKE 'end'");
            if (columnCheck.Rows > 0)
            {
                // The old 'end' column exists, we need to migrate
                Utils.WriteColor($"CT BANS - *[MIGRATING DATABASE SCHEMA]*", ConsoleColor.Yellow);

                // Add new columns if they don't exist
                MySql.ExecuteNonQueryAsync($"ALTER TABLE `{table}` ADD COLUMN IF NOT EXISTS `ban_duration` INT NOT NULL DEFAULT 0");
                MySql.ExecuteNonQueryAsync($"ALTER TABLE `{table}` ADD COLUMN IF NOT EXISTS `time_served` INT NOT NULL DEFAULT 0");

                // Update ban_duration for existing records
                MySql.ExecuteNonQueryAsync($"UPDATE `{table}` SET `ban_duration` = (`end` - `start`) WHERE `end` > 0");

                // Drop the old column
                MySql.ExecuteNonQueryAsync($"ALTER TABLE `{table}` DROP COLUMN `end`");

                Utils.WriteColor($"CT BANS - *[DATABASE MIGRATION COMPLETE]*", ConsoleColor.Green);
            }

            // Check if we need to add the status column
            MySqlQueryResult statusColumnCheck = MySql.ExecuteQuery($"SHOW COLUMNS FROM `{table}` LIKE 'status'");
            if (statusColumnCheck.Rows == 0)
            {
                // The status column doesn't exist, we need to add it
                Utils.WriteColor($"CT BANS - *[ADDING STATUS COLUMN]*", ConsoleColor.Yellow);
                MySql.ExecuteNonQueryAsync($"ALTER TABLE `{table}` ADD COLUMN `status` VARCHAR(10) NOT NULL DEFAULT 'ACTIVE'");
                Utils.WriteColor($"CT BANS - *[STATUS COLUMN ADDED]*", ConsoleColor.Green);
            }
        }
        catch (Exception ex)
        {
            Utils.WriteColor($"CT BANS - *[MYSQL ERROR WHILE LOADING: {ex.Message}]*", ConsoleColor.DarkRed);
        }
    }

    public static async Task<bool> CheckBanAsync(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid)
            return false;

        MySqlDb MySql = ConnectionString();

        MySqlQueryResult result = await MySql!.ExecuteQueryAsync($"SELECT * FROM {table} WHERE steamid = '{player.SteamID}' AND status = 'ACTIVE' ORDER BY id DESC LIMIT 1");
        if (result.Rows == 1)
            return true;

        return false;
    }

    public static bool CheckBan(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid)
            return false;

        // For EventPlayerConnectFull and other sync contexts, we need to run synchronously
        return Task.Run(() => CheckBanAsync(player)).Result;
    }

    public static async Task<int> GetPlayerBanDurationAsync(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid)
            return -1;

        MySqlDb MySql = ConnectionString();

        MySqlQueryResult result = await MySql!.ExecuteQueryAsync($"SELECT * FROM {table} WHERE steamid = '{player.SteamID}' AND status = 'ACTIVE' ORDER BY id DESC LIMIT 1");
        if (result.Rows == 1)
            return result.Get<int>(0, "ban_duration");

        return -1;
    }

    public static int GetPlayerBanDuration(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid)
            return -1;

        return Task.Run(() => GetPlayerBanDurationAsync(player)).Result;
    }

    public static async Task<int> GetPlayerTimeServedAsync(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid)
            return 0;

        MySqlDb MySql = ConnectionString();

        MySqlQueryResult result = await MySql!.ExecuteQueryAsync($"SELECT * FROM {table} WHERE steamid = '{player.SteamID}' AND status = 'ACTIVE' ORDER BY id DESC LIMIT 1");
        if (result.Rows == 1)
            return result.Get<int>(0, "time_served");

        return 0;
    }

    public static int GetPlayerTimeServed(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid)
            return 0;

        return Task.Run(() => GetPlayerTimeServedAsync(player)).Result;
    }

    public static async Task<string> GetPlayerBanReasonAsync(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid)
            return string.Empty;

        MySqlDb MySql = ConnectionString();

        MySqlQueryResult result = await MySql!.ExecuteQueryAsync($"SELECT * FROM {table} WHERE steamid = '{player.SteamID}' AND status = 'ACTIVE' ORDER BY id DESC LIMIT 1");
        if (result.Rows == 1)
            return result.Get<string>(0, "reason");

        return string.Empty;
    }

    public static string GetPlayerBanReason(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid)
            return string.Empty;

        return Task.Run(() => GetPlayerBanReasonAsync(player)).Result;
    }

    public static async Task UpdatePlayerTimeServedAsync(CCSPlayerController? player, int timeServed)
    {
        if (player == null || !player.IsValid)
            return;

        MySqlDb MySql = ConnectionString();
        await MySql.ExecuteNonQueryAsync($"UPDATE `{table}` SET `time_served` = {timeServed} WHERE steamid = '{player.SteamID}' ORDER BY id DESC LIMIT 1");
    }

    // Keep the old method for backward compatibility but make it call the async version
    public static void UpdatePlayerTimeServed(CCSPlayerController? player, int timeServed)
    {
        Task.Run(() => UpdatePlayerTimeServedAsync(player, timeServed)).Wait();
    }

    public static async Task CheckIfIsBannedAsync(CCSPlayerController? player)
    {
        if (player == null)
            return;

        var client = player.Index;

        if (await CheckBanAsync(player) == true)
        {
            int banDuration = await GetPlayerBanDurationAsync(player);
            int timeServed = await GetPlayerTimeServedAsync(player);

            if (banDuration == 0) // Permanent ban
            {
                Plugin.banned[client] = true;
                Plugin.remaining[client] = $"permanent";
                Plugin.reason[client] = await GetPlayerBanReasonAsync(player);
                return;
            }

            if (timeServed >= banDuration)
            {
                // Ban has been served
                Plugin.banned[client] = false;
                Plugin.remaining[client] = null;
                Plugin.reason[client] = null;

                // Update the ban status to EXPIRED instead of deleting
                MySqlDb MySql = ConnectionString();
                await MySql.ExecuteNonQueryAsync($"UPDATE `{table}` SET `status` = 'EXPIRED' WHERE steamid = '{player.SteamID}' AND status = 'ACTIVE' ORDER BY id DESC LIMIT 1");
            }
            else
            {
                // Still banned
                int secondsRemaining = banDuration - timeServed;
                string timeRemainingFormatted = FormatTimeRemainingFromSeconds(secondsRemaining);

                Plugin.banned[client] = true;
                Plugin.remaining[client] = timeRemainingFormatted;
                Plugin.reason[client] = await GetPlayerBanReasonAsync(player);
            }
        }
        else
        {
            Plugin.banned[client] = false;
            Plugin.remaining[client] = null;
            Plugin.reason[client] = null;
        }
    }

    // Keep the old method for backward compatibility but make it call the async version
    public static void CheckIfIsBanned(CCSPlayerController? player)
    {
        Task.Run(() => CheckIfIsBannedAsync(player)).Wait();
    }
}