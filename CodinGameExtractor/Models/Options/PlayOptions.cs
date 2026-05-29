using CommandLine;

namespace CodinGameLogExtractor.Models.Options;

[Verb("play", HelpText = "Jouer contre un boss ou un adversaire spécifique.")]
public class PlayOptions
{
    [Value(0, MetaName = "codeFile", Required = true, HelpText = "Chemin vers le fichier de code à soumettre.")]
    public string CodeFilePath { get; set; } = "";

    [Option('n', "games", Required = false, Default = 10, HelpText = "Nombre de parties à jouer.")]
    public int NumberOfGames { get; set; } = 10;

    [Option("boss", Required = false, Default = false, SetName = "opponent", HelpText = "Jouer contre le boss.")]
    public bool Boss { get; set; }

    [Option("player", Required = false, SetName = "opponent", HelpText = "Id du joueur adverse.")]
    public long? SpecificPlayerId { get; set; }

    [Option('s', "seed", Required = false, HelpText = "Seed manuel (sinon automatique).")]
    public string? ManualSeed { get; set; }

    [Option('o', "output", Required = false, Default = "./codingame_play_logs", HelpText = "Dossier de sortie.")]
    public string OutputDirectory { get; set; } = "./codingame_play_logs";

    [Option('l', "lang", Required = false, HelpText = "Langage de programmation (auto-détecté via l'extension du fichier si non spécifié).")]
    public string? ProgrammingLanguageId { get; set; }

    /// <summary>
    /// Session handle lu depuis la variable d'environnement CODINGAME_SESSION.
    /// </summary>
    public string TestSessionHandle => Environment.GetEnvironmentVariable("CODINGAME_SESSION") ?? "";

    /// <summary>
    /// Cookie lu depuis la variable d'environnement CODINGAME_COOKIE.
    /// </summary>
    public string? Cookie => Environment.GetEnvironmentVariable("CODINGAME_COOKIE");

    /// <summary>
    /// Type d'opposant déterminé à partir des options.
    /// </summary>
    public PlayOpponentType OpponentType => SpecificPlayerId.HasValue ? PlayOpponentType.SpecificPlayer : PlayOpponentType.Boss;

    /// <summary>
    /// Résout le langage de programmation : option CLI > auto-détection via extension.
    /// </summary>
    public string ResolvedLanguageId => ProgrammingLanguageId ?? LanguageDetector.DetectFromFile(CodeFilePath);

    public PlayConfig ToPlayConfig() => new()
    {
        TestSessionHandle = TestSessionHandle,
        CodeFilePath = CodeFilePath,
        ProgrammingLanguageId = ResolvedLanguageId,
        NumberOfGames = NumberOfGames,
        OutputDirectory = OutputDirectory,
        Cookie = Cookie,
        OpponentType = OpponentType,
        SpecificPlayerId = SpecificPlayerId,
        ManualSeed = ManualSeed,
    };
}
