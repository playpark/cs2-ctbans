using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using CTBans.Shared;

public partial class Plugin : ICTBansApi
{
    // API implementation
    public bool IsPlayerCTBanned(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid)
            return false;

        var client = player.Index;
        return banned[client] == true;
    }

    public string? GetPlayerCTBanTimeRemaining(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid)
            return null;

        var client = player.Index;
        return banned[client] == true ? remaining[client] : null;
    }

    public string? GetPlayerCTBanReason(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid)
            return null;

        var client = player.Index;
        return banned[client] == true ? reason[client] : null;
    }

    public bool CheckAndNotifyPlayerCTBan(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid)
            return false;

        var client = player.Index;

        if (banned[client] == true)
        {
            Showinfo[client] = 1;

            player.ExecuteClientCommand($"play {Config.TeamDenySound}");

            return true;
        }

        return false;
    }
}