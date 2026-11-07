using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.GameEvents;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Players;
using zFaceItScoreboard7GC.Configuration;
using zFaceItScoreboard7GC.Models;
using zFaceItScoreboard7GC.Services;

namespace zFaceItScoreboard7GC.Listeners;

public sealed class PlayerConnectionListener
{
    private readonly ISwiftlyCore _core;
    private readonly IOptionsMonitor<FaceitConfig> _configMonitor;
    private readonly IFaceitApiService _apiService;
    private readonly IPlayerDataService _dataService;
    private readonly IScoreboardService _scoreboardService;
    private readonly ILogger<PlayerConnectionListener> _logger;
    private readonly ConcurrentDictionary<ulong, byte> _fetchingPlayers = new();

    public PlayerConnectionListener(
        ISwiftlyCore core,
        IOptionsMonitor<FaceitConfig> configMonitor,
        IFaceitApiService apiService,
        IPlayerDataService dataService,
        IScoreboardService scoreboardService,
        ILogger<PlayerConnectionListener> logger)
    {
        _core = core;
        _configMonitor = configMonitor;
        _apiService = apiService;
        _dataService = dataService;
        _scoreboardService = scoreboardService;
        _logger = logger;
    }

    [GameEventHandler(HookMode.Post)]
    public HookResult OnPlayerConnectFull(EventPlayerConnectFull @event)
    {
        var player = @event.Accessor.GetPlayer("userid");
        if (player == null || !player.IsValid || player.IsFakeClient)
            return HookResult.Continue;

        var steamId = player.SteamID;
        var record = _dataService.GetOrCreate(steamId, defaultShowStatus: true);

        if (ShouldFetch(record))
        {
            _ = FetchAndApplyAsync(player, steamId, record.ShowFaceitLevel);
        }
        else
        {
            _scoreboardService.RefreshPlayer(player);
        }

        return HookResult.Continue;
    }

    [GameEventHandler(HookMode.Post)]
    public HookResult OnPlayerDisconnect(EventPlayerDisconnect @event)
    {
        var player = @event.Accessor.GetPlayer("userid");
        if (player != null && !player.IsFakeClient)
        {
            _fetchingPlayers.TryRemove(player.SteamID, out _);
            _scoreboardService.ClearRuntimeFake(player.SteamID);
            _dataService.MarkDirty();
        }

        return HookResult.Continue;
    }

    public bool ShouldFetch(PlayerFaceitRecord record)
    {
        if (record.LastFetch == DateTime.MinValue)
            return true;

        var config = _configMonitor.CurrentValue;
        return DateTime.UtcNow - record.LastFetch > TimeSpan.FromHours(Math.Max(1, config.CacheExpiryHours));
    }

    public async Task FetchAndApplyAsync(IPlayer player, ulong steamId, bool defaultShowStatus)
    {
        if (!_fetchingPlayers.TryAdd(steamId, 0))
            return;

        try
        {
            var updatedRecord = await _apiService.FetchPlayerDataAsync(steamId, defaultShowStatus).ConfigureAwait(false);
            if (updatedRecord != null)
            {
                // Successful fetch: persist fresh data to cache
                _dataService.UpdatePlayer(updatedRecord);
            }
            else
            {
                // Both APIs failed! Protect existing cached data from being overwritten
                if (_dataService.TryGetPlayer(steamId, out var existing) && existing != null && existing.LastFetch != DateTime.MinValue)
                {
                    _logger.LogWarning("[FaceIT] APIs unreachable for SteamID {SteamId}. Keeping existing cached record (Level: {Level}). Will retry in 5 minutes.", steamId, existing.FaceitLevel);
                    existing.LastFetch = DateTime.UtcNow.AddHours(-Math.Max(1, _configMonitor.CurrentValue.CacheExpiryHours)).AddMinutes(5);
                }
                else
                {
                    _logger.LogWarning("[FaceIT] APIs unreachable for SteamID {SteamId} and no prior cache exists. Displaying fallback pin. Will retry in 5 minutes.", steamId);
                    var temp = _dataService.GetOrCreate(steamId, defaultShowStatus);
                    temp.LastFetch = DateTime.UtcNow.AddHours(-Math.Max(1, _configMonitor.CurrentValue.CacheExpiryHours)).AddMinutes(5);
                }
            }

            // Crucial: Dispatch schema mutation to Main Thread safely
            _core.Scheduler.NextTick(() =>
            {
                if (player.IsValid)
                {
                    _scoreboardService.RefreshPlayer(player);
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[FaceIT] Failed to fetch and apply FaceIT data for SteamID {SteamId}", steamId);
        }
        finally
        {
            _fetchingPlayers.TryRemove(steamId, out _);
        }
    }
}

