using CounterStrikeSharp.API.Core;

public class Config : BasePluginConfig
{
    public Config_Database Database { get; set; } = new Config_Database();
    public Config_Commands Commands { get; set; } = new Config_Commands();
    public string TeamDenySound { get; set; } = "sounds/ui/counter_beep.vsnd";
}

public class Config_Database
{
    public string Table { get; set; } = "ctbans";
    public string Host { get; set; } = "localhost";
    public string Username { get; set; } = "user";
    public string Password { get; set; } = "password";
    public string Name { get; set; } = "database";
    public int Port { get; set; } = 3306;
}

public class Config_Commands
{
    public string Permission { get; set; } = "@css/ban";
    public string CTBan { get; set; } = "ctban,banct";
    public string CTUnban { get; set; } = "ctunban,unctban,unbanct";
    public string CTBanInfo { get; set; } = "ctbaninfo,infoctban,ctbancheck,checkctban,isctban,isctbanned";
    public string AddCTBan { get; set; } = "addctban,offlinebanct";
}