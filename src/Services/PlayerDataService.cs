using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using zFaceItScoreboard7GC.Models;

namespace zFaceItScoreboard7GC.Services;

public sealed class PlayerDataService : IPlayerDataService
{
    private readonly string _dataPath;
    private readonly ILogger<PlayerDataService> _logger;
    private readonly ConcurrentDictionary<ulong, PlayerFaceitRecord> _playerCache = new();
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private volatile bool _hasDirtyData;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public PlayerDataService(string dataPath, ILogger<PlayerDataService> logger)
    {
        _dataPath = dataPath;
        _logger = logger;
    }

    public int Count => _playerCache.Count;

    public PlayerFaceitRecord GetOrCreate(ulong steamId, bool defaultShowStatus)
    {
        return _playerCache.GetOrAdd(steamId, id => new PlayerFaceitRecord
        {
            SteamId = id,
            ShowFaceitLevel = defaultShowStatus,
            LastFetch = DateTime.MinValue
        });
    }

    public bool TryGetPlayer(ulong steamId, out PlayerFaceitRecord? record)
    {
        return _playerCache.TryGetValue(steamId, out record);
    }

    public void UpdatePlayer(PlayerFaceitRecord record)
    {
        _playerCache[record.SteamId] = record;
        MarkDirty();
    }

    public void MarkDirty()
    {
        _hasDirtyData = true;
    }

    public void SaveIfDirty()
    {
        if (!_hasDirtyData)
            return;

        _hasDirtyData = false;
        _ = SaveAsync();
    }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_dataPath))
            return;

        try
        {
            var json = await File.ReadAllTextAsync(_dataPath, ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(json))
                return;

            var items = JsonSerializer.Deserialize<Dictionary<ulong, PlayerFaceitRecord>>(json, JsonOptions);
            if (items == null)
                return;

            foreach (var (steamId, record) in items)
            {
                _playerCache[steamId] = record;
            }

            _logger.LogInformation("[FaceIT] Successfully loaded {Count} player records from cache.", _playerCache.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[FaceIT] Failed to load local cache file from {Path}", _dataPath);
        }
    }

    public async Task SaveAsync(CancellationToken ct = default)
    {
        if (!await _fileLock.WaitAsync(TimeSpan.FromSeconds(2), ct).ConfigureAwait(false))
            return;

        try
        {
            var directory = Path.GetDirectoryName(_dataPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var snapshot = _playerCache.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            var json = JsonSerializer.Serialize(snapshot, JsonOptions);

            // Write to temporary file first, then atomically replace to prevent file corruption
            var tempPath = _dataPath + ".tmp";
            await File.WriteAllTextAsync(tempPath, json, ct).ConfigureAwait(false);
            File.Move(tempPath, _dataPath, overwrite: true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[FaceIT] Failed to save local cache file to {Path}", _dataPath);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public void Dispose()
    {
        SaveIfDirty();
        _fileLock.Dispose();
    }
}

