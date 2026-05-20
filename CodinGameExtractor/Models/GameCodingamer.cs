using System.Text.Json.Serialization;

namespace CodinGameLogExtractor.Models;

public class GameCodingamer
{
    [JsonPropertyName("userId")]
    public long UserId { get; set; }

    [JsonPropertyName("pseudo")]
    public string? Pseudo { get; set; }

    [JsonPropertyName("avatar")]
    public long Avatar { get; set; }
}
