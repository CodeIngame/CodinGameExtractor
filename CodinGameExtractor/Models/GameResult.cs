using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CodinGameLogExtractor.Models;

public class GameResult
{
    [JsonPropertyName("gameId")]
    public long GameId { get; set; }

    [JsonPropertyName("refereeInput")]
    public string? RefereeInput { get; set; }

    [JsonPropertyName("scores")]
    public List<double>? Scores { get; set; }

    [JsonPropertyName("ranks")]
    public List<int>? Ranks { get; set; }

    [JsonPropertyName("frames")]
    public List<GameFrame>? Frames { get; set; }

    [JsonPropertyName("agents")]
    public List<GameAgent>? Agents { get; set; }
}
