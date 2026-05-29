using System.Text.Json.Serialization;

namespace CodinGameLogExtractor.Models.Api;

public class GameFrame
{
    [JsonPropertyName("gameInformation")]
    public string? GameInformation { get; set; }

    [JsonPropertyName("stderr")]
    public string? Stderr { get; set; }

    [JsonPropertyName("stdout")]
    public string? Stdout { get; set; }

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("view")]
    public string? View { get; set; }

    [JsonPropertyName("keyframe")]
    public bool Keyframe { get; set; }

    [JsonPropertyName("agentId")]
    public int AgentId { get; set; }
}
