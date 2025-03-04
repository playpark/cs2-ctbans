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
        banned[client] = false;
        remaining[client] = null;
        reason[client] = null;

        // Check if player is banned
        if (Database.CheckBan(player) == true)
        {
            // Load the player's time served from the database
            timeServed[client] = Database.GetPlayerTimeServed(player);

            // Check if the player is still banned
            Database.CheckIfIsBanned(player);
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
            // Make sure we're not already tracking this player (could happen with respawns)
            if (isPlayerAlive[client] == true && aliveStartTime[client].HasValue)
            {
                // Update the time served before starting a new tracking session
                UpdateTimeServed(player);
            }

            // Start tracking time for this player
            isPlayerAlive[client] = true;
            aliveStartTime[client] = DateTime.UtcNow;

            // Make sure we have the latest time served value from the database
            if (timeServed[client] == null || timeServed[client] == 0)
            {
                timeServed[client] = Database.GetPlayerTimeServed(player);
            }
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

            // Only update if there's meaningful time to add
            if (secondsAlive > 0)
            {
                // Update the time served
                timeServed[client] += secondsAlive;

                // Update the database
                Database.UpdatePlayerTimeServed(player, timeServed[client]!.Value);

                // Check if the ban should be lifted
                Database.CheckIfIsBanned(player);
            }

            // Reset alive tracking
            isPlayerAlive[client] = false;
            aliveStartTime[client] = null;
        }
    }

    public void OnTick()
    {
        // Check if it's time to update (every 10 seconds)
        if (Server.CurrentTime % 10 < 0.1)
        {
            for (int i = 1; i < Server.MaxPlayers; i++)
            {
                var ent = NativeAPI.GetEntityFromIndex(i);
                if (ent == 0)
                    continue;

                var client = new CCSPlayerController(ent);
                if (client == null || !client.IsValid)
                    continue;

                // Update time served for alive players
                if (isPlayerAlive[client.Index] == true && banned[client.Index] == true && aliveStartTime[client.Index].HasValue)
                {
                    // Calculate time spent alive since last update
                    TimeSpan aliveTime = DateTime.UtcNow - aliveStartTime[client.Index]!.Value;
                    int secondsAlive = (int)aliveTime.TotalSeconds;

                    // Only update if there's meaningful time to add
                    if (secondsAlive > 0)
                    {
                        // Update the time served
                        timeServed[client.Index] += secondsAlive;

                        // Update the database
                        Database.UpdatePlayerTimeServed(client, timeServed[client.Index]!.Value);

                        // Check if the ban should be lifted
                        Database.CheckIfIsBanned(client);

                        // Reset the start time to now to avoid double-counting
                        aliveStartTime[client.Index] = DateTime.UtcNow;

                        // If ban has been lifted, stop tracking
                        if (banned[client.Index] != true)
                        {
                            isPlayerAlive[client.Index] = false;
                            aliveStartTime[client.Index] = null;
                        }
                    }
                }
            }
        }

        // Handle showing info to players (separate from the time update logic)
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

        // Update time served before checking ban status
        UpdateTimeServed(player);

        Database.CheckIfIsBanned(player);

        if (team_switch == 3)
        {
            if (IsPlayerCTBanned(player))
            {
                Showinfo[client] = 1;
                player.ExecuteClientCommand($"play {Config.TeamDenySound}");

                // Use our API method to notify the player
                CheckAndNotifyPlayerCTBan(player);

                return HookResult.Stop;
            }
        }

        return HookResult.Continue;
    }
}