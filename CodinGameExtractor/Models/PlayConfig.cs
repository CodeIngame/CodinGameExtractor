namespace CodinGameLogExtractor.Models;

public class PlayConfig
{
    public string TestSessionHandle { get; set; } = "";
    public string CodeFilePath { get; set; } = "";
    public string ProgrammingLanguageId { get; set; } = "C#";
    public int NumberOfGames { get; set; } = 10;
    public string OutputDirectory { get; set; } = "./codingame_play_logs";
    public string? Cookie { get; set; }
    public PlayOpponentType OpponentType { get; set; } = PlayOpponentType.Boss;
    public long? SpecificPlayerId { get; set; }
    public string? ManualSeed { get; set; }
}

public enum PlayOpponentType
{
    Boss,
    SpecificPlayer
}
