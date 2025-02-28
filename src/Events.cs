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

        // Initialize player tracking
        isPlayerAlive[client] = false;
        aliveStartTime[client] = null;
        timeServed[client] = 0;

        if (Database.CheckBan(player) == true)
        {
            // Load the player's time served from the database
            timeServed[client] = Database.GetPlayerTimeServed(player);

            // Check if the player is still banned
            Database.CheckIfIsBanned(player);
        }
        else
        {
            banned[client] = false;
            remaining[client] = null;
            reason[client] = null;
        }

        return HookResult.Continue;
    }

    public HookResult EventPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        CCSPlayerController player = @event.Userid!;

        if (player == null || !player.IsValid || player.IsBot || player.IsHLTV)
            return HookResult.Continue;

        var client = player.Index;

        // Only track time for banned players
        if (banned[client] == true)
        {
            isPlayerAlive[client] = true;
            aliveStartTime[client] = DateTime.UtcNow;
        }

        return HookResult.Continue;
    }

    public HookResult EventPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        CCSPlayerController player = @event.Userid!;

        if (player == null || !player.IsValid || player.IsBot || player.IsHLTV)
            return HookResult.Continue;

        var client = player.Index;

        // Update time served when player dies
        UpdateTimeServed(player);

        return HookResult.Continue;
    }

    public HookResult EventRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        // Update time served for all alive players at the end of the round
        for (int i = 1; i < Server.MaxPlayers; i++)
        {
            var ent = NativeAPI.GetEntityFromIndex(i);
            if (ent == 0)
                continue;

            var client = new CCSPlayerController(ent);
            if (client == null || !client.IsValid || client.IsBot || client.IsHLTV)
                continue;

            UpdateTimeServed(client);
        }

        return HookResult.Continue;
    }

    public HookResult EventPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        CCSPlayerController player = @event.Userid!;

        if (player == null || !player.IsValid || player.IsBot || player.IsHLTV)
            return HookResult.Continue;

        // Update time served when player disconnects
        UpdateTimeServed(player);

        return HookResult.Continue;
    }

    private void UpdateTimeServed(CCSPlayerController player)
    {
        if (player == null || !player.IsValid)
            return;

        var client = player.Index;

        // Only update if player is alive and banned
        if (isPlayerAlive[client] == true && banned[client] == true && aliveStartTime[client].HasValue)
        {
            // Calculate time spent alive
            TimeSpan aliveTime = DateTime.UtcNow - aliveStartTime[client]!.Value;
            int secondsAlive = (int)aliveTime.TotalSeconds;

            // Update the time served
            timeServed[client] += secondsAlive;

            // Update the database
            Database.UpdatePlayerTimeServed(player, timeServed[client]!.Value);

            // Check if the ban should be lifted
            Database.CheckIfIsBanned(player);

            // Reset alive tracking
            isPlayerAlive[client] = false;
            aliveStartTime[client] = null;
        }
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

            // Update time served for alive players periodically (every 10 seconds)
            if (Server.CurrentTime % 10 < 0.1 && isPlayerAlive[client.Index] == true && banned[client.Index] == true)
            {
                UpdateTimeServed(client);

                // If still banned, restart tracking
                if (banned[client.Index] == true)
                {
                    isPlayerAlive[client.Index] = true;
                    aliveStartTime[client.Index] = DateTime.UtcNow;
                }
            }

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

        // Update time served before checking ban status
        UpdateTimeServed(player);

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