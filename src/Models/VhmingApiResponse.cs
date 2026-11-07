using System.Text.Json.Serialization;

namespace zFaceItScoreboard7GC.Models;

public sealed class VhmingApiResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("statusCode")]
    public int StatusCode { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("data")]
    public VhmingPlayerData? Data { get; set; }
}

public sealed class VhmingPlayerData
{
    [JsonPropertyName("steamId")]
    public string? SteamId { get; set; }

    [JsonPropertyName("faceitId")]
    public string? FaceitId { get; set; }

    [JsonPropertyName("nickname")]
    public string? Nickname { get; set; }

    [JsonPropertyName("level")]
    public int? Level { get; set; }

    [JsonPropertyName("elo")]
    public int? Elo { get; set; }

    [JsonPropertyName("banned")]
    public bool Banned { get; set; }

    [JsonPropertyName("challenger")]
    public bool Challenger { get; set; }

    [JsonPropertyName("kd")]
    public double? Kd { get; set; }

    [JsonPropertyName("winRate")]
    public double? WinRate { get; set; }

    [JsonPropertyName("matches")]
    public int? Matches { get; set; }
}

