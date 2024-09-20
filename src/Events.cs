using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
public partial class Plugin
{
    public HookResult EventPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
    {
        CCSPlayerController player = @event.Userid!;

        if (player == null || !player.IsValid)
            return HookResult.Continue;

        var client = player.Index;

        if (Database.CheckBan(player) == true)
        {
            var timeRemaining = DateTimeOffset.FromUnixTimeSeconds(Database.GetPlayerBanTime(player)) - DateTimeOffset.UtcNow;
            var nowtimeis = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var timeRemainingFormatted = $"{timeRemaining.Days}d {timeRemaining.Hours}:{timeRemaining.Minutes:D2}:{timeRemaining.Seconds:D2}";

            if (Database.GetPlayerBanTime(player) < nowtimeis)
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
                reason[client] = Database.GetPlayerBanReason(player);
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

    public void OnTick()
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
    }

    public HookResult OnPlayerChangeTeam(CCSPlayerController? player, CommandInfo command)
    {
        var client = player!.Index;

        if (!Int32.TryParse(command.ArgByIndex(1), out int team_switch))
            return HookResult.Continue;

        if (player == null || !player.IsValid)
            return HookResult.Continue;

        Database.CheckIfIsBanned(player);

        if (team_switch == 3)
        {
            if (banned[client] == true)
            {
                Showinfo[client] = 1;
                player.ExecuteClientCommand($"play {Config.TeamDenySound}");
                return HookResult.Stop;
            }
        }

        return HookResult.Continue;
    }
}