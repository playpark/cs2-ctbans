using CounterStrikeSharp.API.Core;
using System.Text.Json.Serialization;

namespace CTBans;

public class ConfigBan : BasePluginConfig
{
    [JsonPropertyName("Prefix")] public string Prefix { get; set; } = "{red}[ctbans]{white}";
    [JsonPropertyName("Permission")] public string Permission { get; set; } = "@css/ban";

    [JsonPropertyName("DBTable")] public string DBTable { get; set; } = "ctbans";
    [JsonPropertyName("DBHost")] public string DBHost { get; set; } = "localhost";
    [JsonPropertyName("DBUser")] public string DBUser { get; set; } = "user";
    [JsonPropertyName("DBPassword")] public string DBPassword { get; set; } = "password";
    [JsonPropertyName("DBDatabase")] public string DBDatabase { get; set; } = "database";
    [JsonPropertyName("DBPort")] public int DBPort { get; set; } = 3306;
}
