using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Players;
using zFaceItScoreboard7GC.Configuration;
using zFaceItScoreboard7GC.Services;

namespace zFaceItScoreboard7GC.Commands;

public sealed class FaceitCommands
{
    private readonly ISwiftlyCore _core;
    private readonly IOptionsMonitor<FaceitConfig> _configMonitor;
    private readonly IPlayerDataService _dataService;
    private readonly IScoreboardService _scoreboardService;
    private readonly ITranslationHelper _translationHelper;

    public FaceitCommands(
        ISwiftlyCore core,
        IOptionsMonitor<FaceitConfig> configMonitor,
        IPlayerDataService dataService,
        IScoreboardService scoreboardService,
        ITranslationHelper translationHelper)
    {
        _core = core;
        _configMonitor = configMonitor;
        _dataService = dataService;
        _scoreboardService = scoreboardService;
        _translationHelper = translationHelper;
    }

    [Command("faceit")]
    [CommandAlias("fl")]
    public void OnFaceitCommand(ICommandContext context)
    {
        if (!context.IsSentByPlayer || context.Sender == null)
            return;

        var player = context.Sender;
        var config = _configMonitor.CurrentValue;

        if (!config.EnableToggleCommand)
        {
            _ = _translationHelper.SendPlayerMessageAsync(player, "command_disabled");
            return;
        }

        var record = _dataService.GetOrCreate(player.SteamID, defaultShowStatus: true);
        record.ShowFaceitLevel = !record.ShowFaceitLevel;
        _dataService.UpdatePlayer(record);

        if (record.ShowFaceitLevel)
        {
            _scoreboardService.RefreshPlayer(player);
            _ = _translationHelper.SendPlayerMessageAsync(player, "level_enabled");

            if (record.IsBanned)
            {
                _ = _translationHelper.SendPlayerMessageAsync(player, "banned_info");
            }
            else if (record.IsChallenger)
            {
                _ = _translationHelper.SendPlayerMessageAsync(player, "challenger_info");
            }
            else if (record.HasFaceitAccount && record.FaceitLevel > 0)
            {
                _ = _translationHelper.SendPlayerMessageAsync(player, "level_info", record.FaceitLevel);
            }
            else
            {
                _ = _translationHelper.SendPlayerMessageAsync(player, "not_found");
            }
        }
        else
        {
            _scoreboardService.RemoveCoin(player);
            _ = _translationHelper.SendPlayerMessageAsync(player, "level_disabled");
        }
    }

    [Command("fake-faceit")]
    [CommandAlias("fakefaceit")]
    [CommandAlias("ffl")]
    public void OnFakeFaceitCommand(ICommandContext context)
    {
        if (!HasFakePermission(context))
        {
            if (context.IsSentByPlayer && context.Sender != null)
            {
                _ = _translationHelper.SendPlayerMessageAsync(context.Sender, "no_permission");
            }
            return;
        }

        var args = context.Args;
        if (args.Length == 0)
        {
            ReplyToSender(context, "fake_usage");
            return;
        }

        // Check if clearing all: !fake-faceit clear-all or !fake-faceit all reset
        if (args[0].Equals("clear-all", StringComparison.OrdinalIgnoreCase) ||
            (args.Length > 1 && args[0].Equals("all", StringComparison.OrdinalIgnoreCase) &&
             (args[1].Equals("clear", StringComparison.OrdinalIgnoreCase) || args[1].Equals("reset", StringComparison.OrdinalIgnoreCase))))
        {
            _scoreboardService.ClearAllRuntimeFakes();
            _scoreboardService.RefreshAllPlayers();
            ReplyToSender(context, "fake_cleared_all");
            return;
        }

        IPlayer? targetPlayer = null;
        string statusOrLevel;

        if (args.Length == 1)
        {
            if (context.IsSentByPlayer && context.Sender != null)
            {
                targetPlayer = context.Sender;
                statusOrLevel = args[0];
            }
            else
            {
                context.Reply("[FaceIT] Console must specify a target player. Usage: fake-faceit <player|steamid> <status>");
                return;
            }
        }
        else
        {
            var targetStr = args[0];
            statusOrLevel = args[1];
            targetPlayer = FindPlayer(targetStr);

            if (targetPlayer == null)
            {
                if (ulong.TryParse(targetStr, out var rawSteamId))
                {
                    if (statusOrLevel.Equals("reset", StringComparison.OrdinalIgnoreCase) ||
                        statusOrLevel.Equals("clear", StringComparison.OrdinalIgnoreCase))
                    {
                        _scoreboardService.ClearRuntimeFake(rawSteamId);
                        ReplyToSender(context, "fake_reset", rawSteamId.ToString());
                    }
                    else
                    {
                        _scoreboardService.SetRuntimeFake(rawSteamId, statusOrLevel);
                        var displayLabel = _scoreboardService.GetFakeDisplayLabel(statusOrLevel);
                        ReplyToSender(context, "fake_success", rawSteamId.ToString(), displayLabel);
                    }
                    return;
                }

                ReplyToSender(context, "player_not_found", targetStr);
                return;
            }
        }

        if (statusOrLevel.Equals("reset", StringComparison.OrdinalIgnoreCase) ||
            statusOrLevel.Equals("clear", StringComparison.OrdinalIgnoreCase))
        {
            _scoreboardService.ClearRuntimeFake(targetPlayer.SteamID);
            _scoreboardService.RefreshPlayer(targetPlayer);
            ReplyToSender(context, "fake_reset", targetPlayer.Name);
        }
        else
        {
            _scoreboardService.SetRuntimeFake(targetPlayer.SteamID, statusOrLevel);
            _scoreboardService.RefreshPlayer(targetPlayer);
            var displayLabel = _scoreboardService.GetFakeDisplayLabel(statusOrLevel);
            ReplyToSender(context, "fake_success", targetPlayer.Name, displayLabel);
        }
    }

    [Command("unfake-faceit")]
    [CommandAlias("unfakefaceit")]
    [CommandAlias("clearfake")]
    [CommandAlias("unffl")]
    public void OnUnfakeFaceitCommand(ICommandContext context)
    {
        if (!HasFakePermission(context))
        {
            if (context.IsSentByPlayer && context.Sender != null)
            {
                _ = _translationHelper.SendPlayerMessageAsync(context.Sender, "no_permission");
            }
            return;
        }

        var args = context.Args;
        if (args.Length == 0)
        {
            if (context.IsSentByPlayer && context.Sender != null)
            {
                _scoreboardService.ClearRuntimeFake(context.Sender.SteamID);
                _scoreboardService.RefreshPlayer(context.Sender);
                ReplyToSender(context, "fake_reset", context.Sender.Name);
            }
            else
            {
                context.Reply("[FaceIT] Usage: unfake-faceit <player|steamid|all>");
            }
            return;
        }

        var targetStr = args[0];
        if (targetStr.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            _scoreboardService.ClearAllRuntimeFakes();
            _scoreboardService.RefreshAllPlayers();
            ReplyToSender(context, "fake_cleared_all");
            return;
        }

        var targetPlayer = FindPlayer(targetStr);
        if (targetPlayer != null)
        {
            _scoreboardService.ClearRuntimeFake(targetPlayer.SteamID);
            _scoreboardService.RefreshPlayer(targetPlayer);
            ReplyToSender(context, "fake_reset", targetPlayer.Name);
            return;
        }

        if (ulong.TryParse(targetStr, out var rawSteamId))
        {
            _scoreboardService.ClearRuntimeFake(rawSteamId);
            ReplyToSender(context, "fake_reset", rawSteamId.ToString());
            return;
        }

        ReplyToSender(context, "player_not_found", targetStr);
    }

    private bool HasFakePermission(ICommandContext context)
    {
        if (!context.IsSentByPlayer || context.Sender == null)
            return true; // Server console always has permission

        var config = _configMonitor.CurrentValue;
        var perm = config.FakeFaceitPermission;
        if (string.IsNullOrWhiteSpace(perm))
            return true; // No permission specified = permitted

        return _core.Permission.PlayerHasPermission(context.Sender.SteamID, perm);
    }

    private void ReplyToSender(ICommandContext context, string key, params object[] args)
    {
        if (context.IsSentByPlayer && context.Sender != null)
        {
            _ = _translationHelper.SendPlayerMessageAsync(context.Sender, key, args);
        }
        else
        {
            var msg = args.Length > 0 ? _core.Localizer[key, args] : _core.Localizer[key];
            context.Reply($"[FaceIT] {msg}");
        }
    }

    private IPlayer? FindPlayer(string target)
    {
        if (ulong.TryParse(target, out var steamId))
        {
            var bySteam = _core.PlayerManager.GetPlayerFromSteamId(steamId);
            if (bySteam != null && bySteam.IsValid)
                return bySteam;
        }

        foreach (var player in _core.PlayerManager.GetAllPlayers())
        {
            if (!player.IsValid || player.IsFakeClient)
                continue;

            if (player.Name.Contains(target, StringComparison.OrdinalIgnoreCase))
                return player;
        }

        return null;
    }
}
