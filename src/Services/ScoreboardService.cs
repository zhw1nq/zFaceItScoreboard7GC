using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;
using zFaceItScoreboard7GC.Configuration;
using zFaceItScoreboard7GC.Models;

namespace zFaceItScoreboard7GC.Services;

public sealed class ScoreboardService : IScoreboardService
{
    private readonly ISwiftlyCore _core;
    private readonly IOptionsMonitor<FaceitConfig> _configMonitor;
    private readonly IPlayerDataService _dataService;
    private readonly ILogger<ScoreboardService> _logger;

    private readonly ConcurrentDictionary<ulong, string> _runtimeFakeOverrides = new();

    public ScoreboardService(
        ISwiftlyCore core,
        IOptionsMonitor<FaceitConfig> configMonitor,
        IPlayerDataService dataService,
        ILogger<ScoreboardService> logger)
    {
        _core = core;
        _configMonitor = configMonitor;
        _dataService = dataService;
        _logger = logger;
    }

    public void SetRuntimeFake(ulong steamId, string fakeValue)
    {
        _runtimeFakeOverrides[steamId] = fakeValue.Trim().ToLowerInvariant();
    }

    public void ClearRuntimeFake(ulong steamId)
    {
        _runtimeFakeOverrides.TryRemove(steamId, out _);
    }

    public void ClearAllRuntimeFakes()
    {
        _runtimeFakeOverrides.Clear();
    }

    public string? GetEffectiveFake(ulong steamId)
    {
        if (_runtimeFakeOverrides.TryGetValue(steamId, out var runtimeFake))
            return runtimeFake;

        var config = _configMonitor.CurrentValue;
        if (config.FakeFaceit != null && config.FakeFaceit.TryGetValue(steamId.ToString(), out var configFake))
            return configFake.Trim().ToLowerInvariant();

        return null;
    }

    public int ResolveCoinId(PlayerFaceitRecord record)
    {
        var config = _configMonitor.CurrentValue;

        if (!record.ShowFaceitLevel)
            return 0;

        // Check if there is a fake FaceIT override (command or config)
        var fake = GetEffectiveFake(record.SteamId);
        if (!string.IsNullOrEmpty(fake))
        {
            if (fake == "ban" || fake == "banned")
                return config.BannedCoinId;

            if (fake == "challenger")
                return config.ChallengerCoinId;

            if (fake == "null" || fake == "unranked" || fake == "none")
                return config.NullCoinId;

            if (config.LevelCoins.TryGetValue(fake, out var fakeLevelCoin))
                return fakeLevelCoin;

            if (int.TryParse(fake, out var directCoinId) && directCoinId > 0)
                return directCoinId;

            return config.NullCoinId;
        }

        // Real API data resolution
        if (record.IsBanned)
            return config.BannedCoinId;

        if (record.IsChallenger)
            return config.ChallengerCoinId;

        if (record.HasFaceitAccount && record.FaceitLevel > 0)
        {
            if (config.LevelCoins.TryGetValue(record.FaceitLevel.ToString(), out var coinId))
                return coinId;

            return 0;
        }

        return config.NullCoinId;
    }

    public void ApplyCoin(IPlayer player, int coinId)
    {
        try
        {
            if (!player.IsValid || player.IsFakeClient)
                return;

            var controller = player.Controller;
            var inventoryServices = controller?.InventoryServices;
            if (inventoryServices == null)
                return;

            // Dirty Check: If slot 5 already has the target coin ID, DO NOTHING!
            var currentCoin = (uint)inventoryServices.Rank[5];
            if (currentCoin == (uint)coinId)
                return;

            for (var i = 0; i < 6; i++)
            {
                inventoryServices.Rank[i] = (MedalRank_t)0;
            }

            inventoryServices.Rank[5] = (MedalRank_t)(uint)coinId;
            inventoryServices.RankUpdated();
            controller!.InventoryServicesUpdated();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[FaceIT] Error applying coin {CoinId} to player {PlayerName}", coinId, player.Name);
        }
    }

    public void RemoveCoin(IPlayer player)
    {
        try
        {
            if (!player.IsValid || player.IsFakeClient)
                return;

            var controller = player.Controller;
            var inventoryServices = controller?.InventoryServices;
            if (inventoryServices == null)
                return;

            if ((uint)inventoryServices.Rank[5] == 0)
                return;

            inventoryServices.Rank[5] = (MedalRank_t)0;
            inventoryServices.RankUpdated();
            controller!.InventoryServicesUpdated();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[FaceIT] Error removing coin from player {PlayerName}", player.Name);
        }
    }

    public void RefreshPlayer(IPlayer player)
    {
        if (!player.IsValid || player.IsFakeClient)
            return;

        if (_dataService.TryGetPlayer(player.SteamID, out var record) && record != null)
        {
            var coinId = ResolveCoinId(record);
            ApplyCoin(player, coinId);
        }
    }

    public void RefreshAllPlayers()
    {
        foreach (var player in _core.PlayerManager.GetAllPlayers())
        {
            RefreshPlayer(player);
        }
    }
}
