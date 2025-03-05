using CounterStrikeSharp.API.Core;
using Nexd.MySQL;
using System;

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

    public static bool CheckBan(CCSPlayerController? player)
    {
        MySqlDb MySql = ConnectionString();

        MySqlQueryResult result = MySql!.ExecuteQuery($"SELECT * FROM {table} WHERE steamid = '{player!.SteamID}' AND status = 'ACTIVE' ORDER BY id DESC LIMIT 1");
        if (result.Rows == 1)
            return true;

        return false;
    }

    public static int GetPlayerBanDuration(CCSPlayerController? player)
    {
        MySqlDb MySql = ConnectionString();

        MySqlQueryResult result = MySql!.ExecuteQuery($"SELECT * FROM {table} WHERE steamid = '{player!.SteamID}' AND status = 'ACTIVE' ORDER BY id DESC LIMIT 1");
        if (result.Rows == 1)
            return result.Get<int>(0, "ban_duration");

        return -1;
    }

    public static int GetPlayerTimeServed(CCSPlayerController? player)
    {
        MySqlDb MySql = ConnectionString();

        MySqlQueryResult result = MySql!.ExecuteQuery($"SELECT * FROM {table} WHERE steamid = '{player!.SteamID}' AND status = 'ACTIVE' ORDER BY id DESC LIMIT 1");
        if (result.Rows == 1)
            return result.Get<int>(0, "time_served");

        return 0;
    }

    public static string GetPlayerBanReason(CCSPlayerController? player)
    {
        MySqlDb MySql = ConnectionString();

        MySqlQueryResult result = MySql!.ExecuteQuery($"SELECT * FROM {table} WHERE steamid = '{player!.SteamID}' AND status = 'ACTIVE' ORDER BY id DESC LIMIT 1");
        if (result.Rows == 1)
            return $"{result.Get<string>(0, "reason")}";

        return "";
    }

    public static void UpdatePlayerTimeServed(CCSPlayerController? player, int timeServed)
    {
        if (player == null || !player.IsValid)
            return;

        MySqlDb MySql = ConnectionString();
        MySql.ExecuteNonQueryAsync($"UPDATE `{table}` SET `time_served` = {timeServed} WHERE steamid = '{player.SteamID}' ORDER BY id DESC LIMIT 1");
    }

    public static void CheckIfIsBanned(CCSPlayerController? player)
    {
        if (player == null)
            return;

        var client = player.Index;

        if (CheckBan(player) == true)
        {
            int banDuration = GetPlayerBanDuration(player);
            int timeServed = GetPlayerTimeServed(player);

            if (banDuration == 0) // Permanent ban
            {
                Plugin.banned[client] = true;
                Plugin.remaining[client] = $"permanent";
                Plugin.reason[client] = GetPlayerBanReason(player);
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
                MySql.ExecuteNonQueryAsync($"UPDATE `{table}` SET `status` = 'EXPIRED' WHERE steamid = '{player.SteamID}' AND status = 'ACTIVE' ORDER BY id DESC LIMIT 1");
            }
            else
            {
                // Still banned
                int secondsRemaining = banDuration - timeServed;
                string timeRemainingFormatted = FormatTimeRemainingFromSeconds(secondsRemaining);

                Plugin.banned[client] = true;
                Plugin.remaining[client] = timeRemainingFormatted;
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