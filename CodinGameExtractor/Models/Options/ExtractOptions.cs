using CommandLine;

namespace CodinGameLogExtractor.Models.Options;

[Verb("extract", HelpText = "Extraire les résultats de l'Arena (batailles passées).")]
public class ExtractOptions
{
    [Option('u', "user", Required = false, HelpText = "UserId (auto-détecté si non spécifié).")]
    public long UserId { get; set; }

    [Option('o', "output", Required = false, Default = "./codingame_logs", HelpText = "Dossier de sortie.")]
    public string OutputDirectory { get; set; } = "./codingame_logs";

    [Option('c', "concurrency", Required = false, Default = 5, HelpText = "Nombre de requêtes parallèles.")]
    public int MaxConcurrentRequests { get; set; } = 5;

    /// <summary>
    /// Session handle lu depuis la variable d'environnement CODINGAME_SESSION.
    /// </summary>
    public string TestSessionHandle => Environment.GetEnvironmentVariable("CODINGAME_SESSION") ?? "";

    /// <summary>
    /// Cookie lu depuis la variable d'environnement CODINGAME_COOKIE.
    /// </summary>
    public string? Cookie => Environment.GetEnvironmentVariable("CODINGAME_COOKIE");
}
