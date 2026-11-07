using System.Text.Json.Serialization;

namespace zFaceItScoreboard7GC.Models;

public sealed class PlayerFaceitRecord
{
    [JsonPropertyName("steamId")]
    public ulong SteamId { get; set; }

    [JsonPropertyName("show")]
    public bool ShowFaceitLevel { get; set; } = true;

    [JsonPropertyName("hasAccount")]
    public bool HasFaceitAccount { get; set; }

    [JsonPropertyName("level")]
    public int FaceitLevel { get; set; }

    [JsonPropertyName("elo")]
    public int FaceitElo { get; set; }

    [JsonPropertyName("banned")]
    public bool IsBanned { get; set; }

    [JsonPropertyName("challenger")]
    public bool IsChallenger { get; set; }

    [JsonPropertyName("nickname")]
    public string? Nickname { get; set; }

    [JsonPropertyName("lastFetch")]
    public DateTime LastFetch { get; set; } = DateTime.MinValue;
}

