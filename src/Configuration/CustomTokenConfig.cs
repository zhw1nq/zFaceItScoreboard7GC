using System.Text.Json.Serialization;

namespace zFaceItScoreboard7GC.Configuration;

public sealed class CustomTokenConfig
{
    [JsonPropertyName("Id")]
    public int Id { get; set; }

    [JsonPropertyName("Name")]
    public string Name { get; set; } = "";
}

