using CounterStrikeSharp.API.Core;
using System.Text.Json.Serialization;

namespace CTBans;

public class ConfigBan : BasePluginConfig
{
    [JsonPropertyName("DB_Table")] public string DBTable { get; set; } = "ctbans";
    [JsonPropertyName("DB_Host")] public string DBHost { get; set; } = "localhost";
    [JsonPropertyName("DB_User")] public string DBUser { get; set; } = "user";
    [JsonPropertyName("DB_Password")] public string DBPassword { get; set; } = "password";
    [JsonPropertyName("DB_Database")] public string DBDatabase { get; set; } = "database";
    [JsonPropertyName("DB_Port")] public int DBPort { get; set; } = 3306;

    [JsonPropertyName("Permission")] public string Permission { get; set; } = "@css/ban";
    [JsonPropertyName("CTBan_Commands")] public string[] CommandsCTBan { get; set; } = ["ctban", "banct"];
    [JsonPropertyName("CTUnban_Commands")] public string[] CommandsCTUnban { get; set; } = ["ctunban", "unctban"];
    [JsonPropertyName("CTBanInfo_Commands")]  public string[] CommandsCTBanInfo { get; set; } = ["ctbaninfo", "infoctban", "ctbancheck", "checkctban", "isctban", "isctbanned"];

    [JsonPropertyName("Deny_Sound")] public string JoinDenySound { get; set; } = "sounds/ui/counter_beep.vsnd";
}