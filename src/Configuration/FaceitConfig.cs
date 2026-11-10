using System.Text.Json.Serialization;

namespace zFaceItScoreboard7GC.Configuration;

public sealed class FaceitConfig
{
    [JsonPropertyName("VhmingApiKey")]
    public string VhmingApiKey { get; set; } = "";

    [JsonPropertyName("FaceitApiKey")]
    public string FaceitApiKey { get; set; } = "";

    [JsonPropertyName("RequestTimeoutSeconds")]
    public int RequestTimeoutSeconds { get; set; } = 8;

    [JsonPropertyName("MaxConcurrentRequests")]
    public int MaxConcurrentRequests { get; set; } = 4;

    [JsonPropertyName("LevelCoins")]
    public Dictionary<string, int> LevelCoins { get; set; } = new()
    {
        ["1"] = 1061,
        ["2"] = 1032,
        ["3"] = 1041,
        ["4"] = 1074,
        ["5"] = 1067,
        ["6"] = 1088,
        ["7"] = 1039,
        ["8"] = 1055,
        ["9"] = 1087,
        ["10"] = 1017
    };

    [JsonPropertyName("ChallengerCoinId")]
    public int ChallengerCoinId { get; set; } = 1089;

    [JsonPropertyName("BannedCoinId")]
    public int BannedCoinId { get; set; } = 1018;

    [JsonPropertyName("NullCoinId")]
    public int NullCoinId { get; set; } = 5138;

    [JsonPropertyName("CustomTokens")]
    public Dictionary<string, CustomTokenConfig> CustomTokens { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["lycaon"] = new CustomTokenConfig { Id = 1016, Name = "Lycaon PIN" },
        ["ellen"] = new CustomTokenConfig { Id = 1069, Name = "Ellen PIN" },
        ["thuys"] = new CustomTokenConfig { Id = 1083, Name = "Thuys PIN" },
        ["tnim"] = new CustomTokenConfig { Id = 1070, Name = "tniM PIN" }
    };

    [JsonPropertyName("EnableToggleCommand")]
    public bool EnableToggleCommand { get; set; } = false;

    [JsonPropertyName("FakeFaceitPermission")]
    public string FakeFaceitPermission { get; set; } = "admin.faceit.fake.scoreboard";

    [JsonPropertyName("FakeFaceit")]
    public Dictionary<string, string> FakeFaceit { get; set; } = new();

    [JsonPropertyName("CacheExpiryHours")]
    public int CacheExpiryHours { get; set; } = 24;

    [JsonPropertyName("AutoSaveIntervalSeconds")]
    public int AutoSaveIntervalSeconds { get; set; } = 120;

    [JsonPropertyName("SafetyCheckIntervalSeconds")]
    public float SafetyCheckIntervalSeconds { get; set; } = 3.0f;
}

