using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;

namespace CTBans;

public partial class CTBans : BasePlugin, IPluginConfig<ConfigBan>
{
    public override string ModuleName => "CTBans";
    public override string ModuleAuthor => "DeadSwim, modified by exkludera";
    public override string ModuleDescription => "Banning players to join in CT.";
    public override string ModuleVersion => "V. 1.0.2";

    private static readonly bool?[] banned = new bool?[64];
    private static readonly string?[] remaining = new string?[64];
    private static readonly string?[] reason = new string?[64];
    private static readonly int?[] Showinfo = new int?[64];

    public required ConfigBan Config { get; set; }

    public void OnConfigParsed(ConfigBan config)
    {
        Config = config;
    }

    public override void Load(bool hotReload)
    {
        WriteColor("CT BANS - Plugins has been [*LOADED*]", ConsoleColor.Green);
        CreateDatabase();

        AddCommandListener("jointeam", OnPlayerChangeTeam);
        RegisterListener<Listeners.OnTick>(() =>
        {
            for (int i = 1; i < Server.MaxPlayers; i++)
            {
                var ent = NativeAPI.GetEntityFromIndex(i);
                if (ent == 0)
                    continue;

                var client = new CCSPlayerController(ent);
                if (client == null || !client.IsValid)
                    continue;

                if (Showinfo[client.Index] == 1)
                {
                    client.PrintToCenterHtml
                    (
                        Localizer["hud_content_1"] +
                        Localizer["hud_content_2"] +
                        Localizer["hud_content_3", remaining[client.Index]!] +
                        Localizer["hud_content_4", reason[client.Index]!]
                    );
                    AddTimer(10.0f, () => { Showinfo[client.Index] = null; });
                }
            }
        });

        Dictionary<IEnumerable<string>, (string description, CommandInfo.CommandCallback handler)> commands = new()
        {
            {Config.CommandsCTBan, ("ban player from ct", BanCT)},
            {Config.CommandsCTUnban, ("unban player from ct", UnbanCT)},
            {Config.CommandsCTBanInfo, ("info about a ctban", InfobanCT)}
        };

        foreach (KeyValuePair<IEnumerable<string>, (string description, CommandInfo.CommandCallback handler)> commandPair in commands)
        {
            foreach (string command in commandPair.Key)
                AddCommand($"css_{command}", commandPair.Value.description, commandPair.Value.handler);
        }

    }

    [GameEventHandler]
    public HookResult OnPlayerConnect(EventPlayerConnectFull @event, GameEventInfo info)
    {
        CCSPlayerController player = @event.Userid!;

        if (player == null || !player.IsValid)
            return HookResult.Continue;

        var client = player.Index;

        if (CheckBan(player) == true)
        {
            var timeRemaining = DateTimeOffset.FromUnixTimeSeconds(GetPlayerBanTime(player)) - DateTimeOffset.UtcNow;
            var nowtimeis = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var timeRemainingFormatted = $"{timeRemaining.Days}d {timeRemaining.Hours}:{timeRemaining.Minutes:D2}:{timeRemaining.Seconds:D2}";

            if (GetPlayerBanTime(player) < nowtimeis)
            {
                banned[client] = false;
                remaining[client] = null;
                reason[client] = null;
                Showinfo[client] = null;
                //MySqlDb MySql = new MySqlDb(Config.DBHost, Config.DBUser, Config.DBPassword, Config.DBDatabase);
                //var steamid = player.SteamID.ToString();
                //MySql.Table($"{Config.DBTable}").Where($"steamid = '{steamid}'").Delete();
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
            banned[client] = false;
            remaining[client] = null;
            reason[client] = null;
        }
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerChangeTeam(CCSPlayerController? player, CommandInfo command)
    {
        var client = player!.Index;

        if (!Int32.TryParse(command.ArgByIndex(1), out int team_switch))
            return HookResult.Continue;

        if (player == null || !player.IsValid)
            return HookResult.Continue;

        CheckIfIsBanned(player);

        if(team_switch == 3)
        {
            if (banned[client] == true)
            {
                Showinfo[client] = 1;
                player.ExecuteClientCommand($"play {Config.JoinDenySound}");
                return HookResult.Stop;
            }
        }

        return HookResult.Continue;
    }
}
