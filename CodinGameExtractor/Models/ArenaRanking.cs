using System.Text.Json.Serialization;

namespace CodinGameLogExtractor.Models;

public class ArenaRanking
{
    [JsonPropertyName("pseudo")]
    public string? Pseudo { get; set; }

    [JsonPropertyName("rank")]
    public int Rank { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("score")]
    public double Score { get; set; }

    [JsonPropertyName("programmingLanguage")]
    public string? ProgrammingLanguage { get; set; }

    [JsonPropertyName("league")]
    public ArenaLeague? League { get; set; }

    [JsonPropertyName("percentage")]
    public int Percentage { get; set; }

    [JsonPropertyName("codingamer")]
    public GameCodingamer? Codingamer { get; set; }
}

public class ArenaLeague
{
    [JsonPropertyName("divisionIndex")]
    public int DivisionIndex { get; set; }

    [JsonPropertyName("divisionCount")]
    public int DivisionCount { get; set; }

    [JsonPropertyName("openingLeaguesCount")]
    public int OpeningLeaguesCount { get; set; }
}
