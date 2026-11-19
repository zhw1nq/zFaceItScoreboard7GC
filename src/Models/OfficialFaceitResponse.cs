using System.Text.Json.Serialization;

namespace zFaceItScoreboard7GC.Models;

public sealed class OfficialFaceitResponse
{
    [JsonPropertyName("player_id")]
    public string? PlayerId { get; set; }

    [JsonPropertyName("nickname")]
    public string? Nickname { get; set; }

    [JsonPropertyName("games")]
    public Dictionary<string, OfficialFaceitGameData>? Games { get; set; }
}

public sealed class OfficialFaceitGameData
{
    [JsonPropertyName("skill_level")]
    public int SkillLevel { get; set; }

    [JsonPropertyName("faceit_elo")]
    public int FaceitElo { get; set; }

    [JsonPropertyName("region")]
    public string? Region { get; set; }
}

public sealed class OfficialFaceitBansResponse
{
    [JsonPropertyName("items")]
    public List<OfficialFaceitBanItem>? Items { get; set; }
}

public sealed class OfficialFaceitBanItem
{
    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    [JsonPropertyName("starts_at")]
    public string? StartsAt { get; set; }

    [JsonPropertyName("ends_at")]
    public string? EndsAt { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("game")]
    public string? Game { get; set; }

    public bool IsActive()
    {
        // If ends_at is empty or null, it represents a permanent ban
        if (string.IsNullOrWhiteSpace(EndsAt))
        {
            return true;
        }

        // Try parse as ISO 8601 string (e.g. 2026-12-31T23:59:59Z)
        if (DateTime.TryParse(EndsAt, null, System.Globalization.DateTimeStyles.RoundtripKind, out var endsAtDate))
        {
            return endsAtDate.ToUniversalTime() > DateTime.UtcNow;
        }

        // Try parse as epoch integer
        if (long.TryParse(EndsAt, out var epoch))
        {
            if (epoch == 0)
            {
                return true;
            }

            var expDate = DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime;
            return expDate > DateTime.UtcNow;
        }

        // Default to active if status is unparseable
        return true;
    }
}

public sealed class OfficialFaceitRankingResponse
{
    [JsonPropertyName("position")]
    public int Position { get; set; }
}

