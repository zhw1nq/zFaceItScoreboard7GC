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
}

