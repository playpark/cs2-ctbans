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

            if (player == null || !player.IsValid || player.IsBot || player.IsHLTV)
                return HookResult.Continue;

            // Make sure we're not already tracking this player (could happen with respawns)
            if (isPlayerAlive[client] == true && aliveStartTime[client].HasValue)
            {
                // Update the time served before starting a new tracking session
                Utils.Debug($"Player {player.PlayerName} respawned while already being tracked. Updating time served first.");
                UpdateTimeServed(player);
            }

            // Start tracking time for this player
            isPlayerAlive[client] = true;
            aliveStartTime[client] = DateTime.UtcNow;
            Utils.Debug($"Started tracking time for player {player.PlayerName} at {aliveStartTime[client]}");
        }

        return HookResult.Continue;
    }

    public HookResult EventPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        CCSPlayerController player = @event.Userid!;

        if (player == null || !player.IsValid || player.IsBot || player.IsHLTV)
            return HookResult.Continue;

        var client = player.Index;

        if (player == null || !player.IsValid || player.IsBot || player.IsHLTV)
            return HookResult.Continue;

        Utils.Debug($"Player {player.PlayerName} died, updating time served.");

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

    private void UpdateTimeServed(CCSPlayerController player, bool periodicUpdate = false)
    {
        if (player == null || !player.IsValid)
        {
            Console.WriteLine("[CTBans] UpdateTimeServed called with invalid player");
            return;
        }

        // Additional check to ensure player is still connected and not a bot
        if (player.IsBot || player.IsHLTV || !player.Connected.Equals(PlayerConnectedState.PlayerConnected))
        {
            Utils.Debug($"Skipping player {player.PlayerName} - not a valid player");
            return;
        }

        var client = player.Index;

        // Check if the player index is valid
        if (client < 0 || client >= Server.MaxPlayers)
        {
            Utils.Debug($"UpdateTimeServed: Invalid player index {client}");
            return;
        }

        // Only update if player is alive and banned
        if (isPlayerAlive[client] == true && banned[client] == true && aliveStartTime[client].HasValue)
        {
            try
            {
                // Calculate time spent alive using timestamp difference
                TimeSpan aliveTime = DateTime.UtcNow - aliveStartTime[client]!.Value;
                int secondsAlive = (int)aliveTime.TotalSeconds;

                // Debug output to track time calculations
                string updateType = periodicUpdate ? "Periodic" : "Event";
                Utils.Debug($"{updateType} update for player {player.PlayerName}: Current alive time: {secondsAlive}s, Start time: {aliveStartTime[client]!.Value}");

                // Only update if there's meaningful time to add
                if (secondsAlive > 0)
                {
                    // Store the current timestamp for accurate tracking
                    DateTime currentTimestamp = DateTime.UtcNow;

                    // Get the latest time served from the database
                    int currentTimeServed = Database.GetPlayerTimeServed(player);

                    // Add the new time to the current time served
                    int newTimeServed = currentTimeServed + secondsAlive;

                    Utils.Debug($"{updateType} update: Player {player.PlayerName}: Adding {secondsAlive}s to current time served ({currentTimeServed}s) = {newTimeServed}s");

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

                    // For periodic updates (from OnTick), we want to update the start time but not reset tracking
                    if (periodicUpdate && banned[client] == true)
                    {
                        // Update the start time to now to avoid double-counting
                        aliveStartTime[client] = currentTimestamp;
                        Utils.Debug($"Periodic update: Updated start time for player {player.PlayerName} to {currentTimestamp}");
                    }
                    else
                    {
                        // For non-periodic updates (death, disconnect, etc.), reset tracking
                        isPlayerAlive[client] = false;
                        aliveStartTime[client] = null;
                        Utils.Debug($"Event update: Reset tracking for player {player.PlayerName}");
                    }
                }
                else if (!periodicUpdate)
                {
                    // For non-periodic updates with no time to add, still reset tracking
                    isPlayerAlive[client] = false;
                    aliveStartTime[client] = null;
                    Utils.Debug($"Event update: Reset tracking for player {player.PlayerName} (no time to add)");
                }
            }
            catch (Exception ex)
            {
                Utils.Debug($"Error in UpdateTimeServed for player {player.PlayerName}: {ex.Message}");
                // Reset tracking state to avoid getting stuck
                isPlayerAlive[client] = false;
                aliveStartTime[client] = null;
            }
        }
    }

    // Track the last time we performed an update
    private static int lastUpdateTime = -1;

    public void OnTick()
    {
        // Get the current time in seconds (as an integer)
        int currentTimeSeconds = (int)Math.Floor(Server.CurrentTime);

        // Check if it's time to update (every 10 seconds)
        // Only update if we're at a new 10-second mark
        if (currentTimeSeconds % 10 == 0 && currentTimeSeconds != lastUpdateTime)
        {
            // Update the last update time
            lastUpdateTime = currentTimeSeconds;

            // Add debug output to verify the timer is working
            Utils.Debug($"10-second timer triggered at {Server.CurrentTime}, next update at {currentTimeSeconds + 10}");

            // Process all players
            for (int i = 1; i < Server.MaxPlayers; i++)
            {
                var ent = NativeAPI.GetEntityFromIndex(i);
                if (ent == 0)
                    continue;

                var client = new CCSPlayerController(ent);
                if (client == null || !client.IsValid)
                    continue;

                // Call UpdateTimeServed with periodicUpdate=true to update time for this player
                UpdateTimeServed(client, true);
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