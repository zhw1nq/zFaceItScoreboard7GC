using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using zFaceItScoreboard7GC.Configuration;
using zFaceItScoreboard7GC.Models;

namespace zFaceItScoreboard7GC.Services;

public sealed class FaceitApiService : IFaceitApiService
{
    private readonly IOptionsMonitor<FaceitConfig> _configMonitor;
    private readonly ILogger<FaceitApiService> _logger;
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _rateLimiter;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public FaceitApiService(
        IOptionsMonitor<FaceitConfig> configMonitor,
        ILogger<FaceitApiService> logger)
    {
        _configMonitor = configMonitor;
        _logger = logger;

        var config = _configMonitor.CurrentValue;
        var maxConcurrent = Math.Max(1, config.MaxConcurrentRequests);
        _rateLimiter = new SemaphoreSlim(maxConcurrent, maxConcurrent);

        _httpClient = new HttpClient(new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
            MaxConnectionsPerServer = 10
        })
        {
            Timeout = TimeSpan.FromSeconds(Math.Max(3, config.RequestTimeoutSeconds))
        };
    }

    public async Task<PlayerFaceitRecord?> FetchPlayerDataAsync(ulong steamId, bool defaultShowStatus, CancellationToken ct = default)
    {
        var record = new PlayerFaceitRecord
        {
            SteamId = steamId,
            ShowFaceitLevel = defaultShowStatus,
            LastFetch = DateTime.UtcNow
        };

        if (!await _rateLimiter.WaitAsync(TimeSpan.FromSeconds(2), ct).ConfigureAwait(false))
        {
            _logger.LogWarning("[FaceIT] Rate limit semaphore timeout for SteamID {SteamId}", steamId);
            return null;
        }

        try
        {
            var config = _configMonitor.CurrentValue;

            // 1. Try Primary API (api.vhming.dev)
            if (!string.IsNullOrWhiteSpace(config.VhmingApiKey))
            {
                var success = await TryFetchFromVhmingAsync(steamId, config.VhmingApiKey, record, ct).ConfigureAwait(false);
                if (success)
                {
                    return record;
                }
            }

            // 2. Fallback to Official FaceIT API v4 if configured
            if (!string.IsNullOrWhiteSpace(config.FaceitApiKey))
            {
                _logger.LogDebug("[FaceIT] Attempting fallback to Official FaceIT API for SteamID {SteamId}", steamId);
                var fallbackSuccess = await TryFetchFromOfficialFaceitAsync(steamId, config.FaceitApiKey, record, ct).ConfigureAwait(false);
                if (fallbackSuccess)
                {
                    return record;
                }
            }
            else if (string.IsNullOrWhiteSpace(config.VhmingApiKey))
            {
                _logger.LogWarning("[FaceIT] Neither VhmingApiKey nor FaceitApiKey is configured! Cannot fetch stats.");
            }

            // Both APIs failed or unreachable
            _logger.LogWarning("[FaceIT] Both FaceIT APIs failed to retrieve data for SteamID {SteamId}", steamId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[FaceIT] Unexpected error while fetching player data for SteamID {SteamId}", steamId);
            return null;
        }
        finally
        {
            _rateLimiter.Release();
        }
    }

    private async Task<bool> TryFetchFromVhmingAsync(
        ulong steamId,
        string apiKey,
        PlayerFaceitRecord record,
        CancellationToken ct)
    {
        try
        {
            var url = $"https://api.vhming.dev/v1/server/faceit/player/{steamId}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("X-API-SERVERKEY", apiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[FaceIT] Primary API returned status code {StatusCode} for SteamID {SteamId}", response.StatusCode, steamId);
                return false;
            }

            var contentStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            var apiResult = await JsonSerializer.DeserializeAsync<VhmingApiResponse>(contentStream, JsonOptions, ct).ConfigureAwait(false);

            if (apiResult?.Success != true || apiResult.Data == null)
            {
                return false;
            }

            var data = apiResult.Data;
            record.IsBanned = data.Banned;
            record.IsChallenger = data.Challenger;
            record.Nickname = data.Nickname;

            if (!string.IsNullOrEmpty(data.FaceitId) && data.Level.HasValue && data.Level.Value > 0)
            {
                record.HasFaceitAccount = true;
                record.FaceitLevel = data.Level.Value;
                record.FaceitElo = data.Elo ?? 0;
            }
            else
            {
                record.HasFaceitAccount = false;
                record.FaceitLevel = 0;
                record.FaceitElo = 0;
            }

            record.LastFetch = DateTime.UtcNow;
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning("[FaceIT] Primary API request failed for SteamID {SteamId}: {Message}", steamId, ex.Message);
            return false;
        }
    }

    private async Task<bool> TryFetchFromOfficialFaceitAsync(
        ulong steamId,
        string apiKey,
        PlayerFaceitRecord record,
        CancellationToken ct)
    {
        try
        {
            var url = $"https://open.faceit.com/data/v4/players?game=cs2&game_player_id={steamId}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                record.HasFaceitAccount = false;
                record.FaceitLevel = 0;
                record.LastFetch = DateTime.UtcNow;
                return true;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[FaceIT] Official API returned status code {StatusCode} for SteamID {SteamId}", response.StatusCode, steamId);
                return false;
            }

            var contentStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            var apiResult = await JsonSerializer.DeserializeAsync<OfficialFaceitResponse>(contentStream, JsonOptions, ct).ConfigureAwait(false);

            if (apiResult == null)
            {
                return false;
            }

            record.Nickname = apiResult.Nickname;

            if (apiResult.Games != null && apiResult.Games.TryGetValue("cs2", out var gameData))
            {
                record.HasFaceitAccount = true;
                record.FaceitLevel = gameData.SkillLevel;
                record.FaceitElo = gameData.FaceitElo;
            }
            else
            {
                record.HasFaceitAccount = false;
                record.FaceitLevel = 0;
            }

            record.LastFetch = DateTime.UtcNow;
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning("[FaceIT] Official API request failed for SteamID {SteamId}: {Message}", steamId, ex.Message);
            return false;
        }
    }

    public void Dispose()
    {
        _rateLimiter.Dispose();
        _httpClient.Dispose();
    }
}

