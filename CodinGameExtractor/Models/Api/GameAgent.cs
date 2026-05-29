using System.Text.Json.Serialization;

namespace CodinGameLogExtractor.Models.Api;

public class GameAgent
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("codingamer")]
    public GameCodingamer? Codingamer { get; set; }

    [JsonPropertyName("agentId")]
    public long AgentId { get; set; }

    [JsonPropertyName("score")]
    public double? Score { get; set; }

    [JsonPropertyName("rank")]
    public int? Rank { get; set; }

    [JsonPropertyName("valid")]
    public bool Valid { get; set; }
}
