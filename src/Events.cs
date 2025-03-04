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
            // Calculate time spent alive using timestamp difference
            TimeSpan aliveTime = DateTime.UtcNow - aliveStartTime[client]!.Value;
            int secondsAlive = (int)aliveTime.TotalSeconds;

            // Only update if there's meaningful time to add
            if (secondsAlive > 0)
            {
                // Get the latest time served from the database
                int currentTimeServed = Database.GetPlayerTimeServed(player);

                // Add the new time to the current time served
                int newTimeServed = currentTimeServed + secondsAlive;

                // Update local tracking
                timeServed[client] = newTimeServed;

                // Update the database
                Database.UpdatePlayerTimeServed(player, newTimeServed);

                // Check if the ban should be lifted
                Database.CheckIfIsBanned(player);

                // If still banned, update the remaining time display
                if (banned[client] == true)
                {
                    // Recalculate remaining time
                    int banDuration = Database.GetPlayerBanDuration(player);
                    if (banDuration > 0) // Not a permanent ban
                    {
                        int secondsRemaining = banDuration - timeServed[client]!.Value;
                        TimeSpan timeRemaining = TimeSpan.FromSeconds(secondsRemaining);
                        remaining[client] = $"{timeRemaining.Days}d {timeRemaining.Hours}:{timeRemaining.Minutes:D2}:{timeRemaining.Seconds:D2}";
                    }
                }
            }

            // Reset alive tracking - only do this when player is no longer alive
            // This is important for events like death, disconnect, etc.
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
                    // Calculate time spent alive since the original spawn timestamp
                    TimeSpan aliveTime = DateTime.UtcNow - aliveStartTime[client.Index]!.Value;
                    int secondsAlive = (int)aliveTime.TotalSeconds;

                    // Only update if there's meaningful time to add
                    if (secondsAlive > 0)
                    {
                        // Store the current timestamp for accurate tracking
                        DateTime currentTimestamp = DateTime.UtcNow;

                        // Get the latest time served from the database
                        int currentTimeServed = Database.GetPlayerTimeServed(client);

                        // Add the new time to the current time served
                        int newTimeServed = currentTimeServed + secondsAlive;

                        // Update local tracking
                        timeServed[client.Index] = newTimeServed;

                        // Update the database
                        Database.UpdatePlayerTimeServed(client, newTimeServed);

                        // Check if the ban should be lifted
                        Database.CheckIfIsBanned(client);

                        // Update the start time to now to avoid double-counting
                        // This is crucial - we're resetting the timestamp after counting the time
                        aliveStartTime[client.Index] = currentTimestamp;

                        // If ban has been lifted, stop tracking
                        if (banned[client.Index] != true)
                        {
                            isPlayerAlive[client.Index] = false;
                            aliveStartTime[client.Index] = null;
                        }
                        // If still banned, update the remaining time display
                        else
                        {
                            // Recalculate remaining time
                            int banDuration = Database.GetPlayerBanDuration(client);
                            if (banDuration > 0) // Not a permanent ban
                            {
                                int secondsRemaining = banDuration - timeServed[client.Index]!.Value;
                                TimeSpan timeRemaining = TimeSpan.FromSeconds(secondsRemaining);
                                remaining[client.Index] = $"{timeRemaining.Days}d {timeRemaining.Hours}:{timeRemaining.Minutes:D2}:{timeRemaining.Seconds:D2}";
                            }
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