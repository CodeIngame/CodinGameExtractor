using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CodinGameLogExtractor.Models;

namespace CodinGameLogExtractor.Services;

public class PlayService
{
    private const string PlayUrl = "https://www.codingame.com/services/TestSession/play";
    private readonly HttpClient _httpClient;
    private readonly PlayConfig _config;
    private long _userId;

    public PlayService(PlayConfig config)
    {
        _config = config;

        var cookieContainer = new System.Net.CookieContainer();
        var handler = new HttpClientHandler { CookieContainer = cookieContainer, UseCookies = true };
        _httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(120) };
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "fr-FR,fr;q=0.8");
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

        if (!string.IsNullOrEmpty(config.Cookie))
        {
            var uri = new Uri("https://www.codingame.com");
            foreach (var part in config.Cookie.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                var eqIndex = part.IndexOf('=');
                if (eqIndex > 0)
                {
                    var name = part[..eqIndex].Trim();
                    var value = part[(eqIndex + 1)..].Trim();
                    cookieContainer.Add(uri, new System.Net.Cookie(name, value));
                }
            }
        }
    }

    public async Task RunAsync()
    {
        Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║       🎮 CodinGame Play Mode                                   ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        if (!File.Exists(_config.CodeFilePath))
        {
            Console.WriteLine($"❌ Fichier code introuvable: {_config.CodeFilePath}");
            return;
        }

        var code = await File.ReadAllTextAsync(_config.CodeFilePath);
        Console.WriteLine($"📄 Code chargé depuis: {_config.CodeFilePath} ({code.Length} caractères)");
        Console.WriteLine($"🎯 Opposant: {(_config.OpponentType == PlayOpponentType.Boss ? "BOSS" : $"Joueur {_config.SpecificPlayerId}")}");
        Console.WriteLine($"🎲 Seed: {(_config.ManualSeed ?? "automatique")}");
        Console.WriteLine($"🔢 Nombre de parties: {_config.NumberOfGames}");
        Console.WriteLine($"📁 Output: {Path.GetFullPath(_config.OutputDirectory)}");
        Console.WriteLine();

        _userId = DetectUserId();

        if (Directory.Exists(_config.OutputDirectory))
        {
            foreach (var dir in Directory.GetDirectories(_config.OutputDirectory))
                try { Directory.Delete(dir, true); } catch { }
            foreach (var file in Directory.GetFiles(_config.OutputDirectory))
                try { File.Delete(file); } catch { }
        }
        else
        {
            Directory.CreateDirectory(_config.OutputDirectory);
        }

        var results = new List<PlayResult>();

        for (int i = 1; i <= _config.NumberOfGames; i++)
        {
            Console.Write($"  [{i}/{_config.NumberOfGames}] Playing... ");
            try
            {
                var result = await PlayOneGameAsync(code);
                if (result != null)
                {
                    results.Add(result);
                    var icon = result.IsWin ? "✅" : "❌";
                    Console.WriteLine($"{icon} {(result.IsWin ? "WIN" : "LOSS")} score={result.ScoreDiff:+#;-#;0} seed={result.Seed}");
                }
                else
                {
                    Console.WriteLine("⚠️ Pas de résultat");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"💥 Erreur: {ex.Message}");
            }

            if (i < _config.NumberOfGames)
                await Task.Delay(500);
        }

        Console.WriteLine();
        SaveResults(results);
        PrintSummary(results);
    }

    private async Task<PlayResult?> PlayOneGameAsync(string code)
    {
        var body = BuildPlayBody(code);
        var content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(PlayUrl, content);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            var preview = responseContent.Length > 200 ? responseContent[..200] : responseContent;
            throw new Exception($"HTTP {response.StatusCode}: {preview}");
        }

        using var doc = JsonDocument.Parse(responseContent);
        var root = doc.RootElement;

        var gameResult = JsonSerializer.Deserialize<GameResult>(responseContent);
        if (gameResult == null) return null;

        var seed = ExtractSeed(gameResult.RefereeInput);
        var isWin = DetermineWin(gameResult);
        var scoreDiff = GetScoreDiff(gameResult);
        var summary = GetFinalSummary(gameResult);

        return new PlayResult
        {
            GameResult = gameResult,
            Seed = seed,
            IsWin = isWin,
            ScoreDiff = scoreDiff,
            Summary = summary
        };
    }

    private string BuildPlayBody(string code)
    {
        var multi = new Dictionary<string, object?>();

        if (_config.OpponentType == PlayOpponentType.Boss)
        {
            multi["agentsIds"] = new object[] { -1, -2 };
            multi["playType"] = new[] { "IDE_CODE", "BOSS" };
        }
        else
        {
            multi["agentsIds"] = new object[] { -1, _config.SpecificPlayerId! };
            multi["playType"] = new[] { "IDE_CODE", "OTHER_PLAYER" };
        }

        if (!string.IsNullOrEmpty(_config.ManualSeed))
        {
            multi["gameOptions"] = $"seed={_config.ManualSeed}\n";
        }
        else
        {
            multi["gameOptions"] = null;
        }

        multi["isSoloLeague"] = false;

        var payload = new object[]
        {
            _config.TestSessionHandle,
            new Dictionary<string, object?>
            {
                ["code"] = code,
                ["programmingLanguageId"] = _config.ProgrammingLanguageId,
                ["multi"] = multi
            }
        };

        return JsonSerializer.Serialize(payload);
    }

    private bool DetermineWin(GameResult gameResult)
    {
        // In play mode, our agent is always index 0 (agentsIds[0] = -1 = IDE_CODE)
        if (gameResult.Ranks is { Count: >= 2 })
            return gameResult.Ranks[0] == 0;
        return false;
    }

    private int GetScoreDiff(GameResult gameResult)
    {
        if (gameResult.Scores is not { Count: >= 2 })
            return 0;
        return (int)(gameResult.Scores[0] - gameResult.Scores[1]);
    }

    private static string ExtractSeed(string? refereeInput)
    {
        if (string.IsNullOrEmpty(refereeInput))
            return "Unknown";
        var match = Regex.Match(refereeInput, @"seed=(-?\d+)");
        return match.Success ? match.Groups[1].Value : "Unknown";
    }

    private static string GetFinalSummary(GameResult gameResult)
    {
        if (gameResult.Frames is not { Count: > 0 })
            return "";

        for (int i = gameResult.Frames.Count - 1; i >= 0; i--)
        {
            var frame = gameResult.Frames[i];
            if (!string.IsNullOrEmpty(frame.Summary))
                return frame.Summary;
        }
        return "";
    }

    private void SaveResults(List<PlayResult> results)
    {
        Console.WriteLine($"💾 Sauvegarde dans {Path.GetFullPath(_config.OutputDirectory)}...\n");

        var opponentLabel = _config.OpponentType == PlayOpponentType.Boss ? "boss" : $"player_{_config.SpecificPlayerId}";

        foreach (var result in results)
        {
            var category = result.IsWin ? "win" : "loss";
            var categoryDir = Path.Combine(_config.OutputDirectory, category, opponentLabel);

            if (!Directory.Exists(categoryDir))
                Directory.CreateDirectory(categoryDir);

            var fileName = $"{category.ToUpper()}_seed={result.Seed}_gameId={result.GameResult.GameId}.txt";
            var filePath = Path.Combine(categoryDir, fileName);

            var fileContent = FormatGameLog(result);
            File.WriteAllText(filePath, fileContent);
        }
    }

    private string FormatGameLog(PlayResult result)
    {
        var gameResult = result.GameResult;
        var lines = new List<string>
        {
            $"GAME_ID: {gameResult.GameId}",
            $"SEED: {result.Seed}",
            $"RESULT: {(result.IsWin ? "WIN" : "LOSS")}",
            $"SCORE_DIFF: {result.ScoreDiff:+#;-#;0}",
            $"SCORES: {string.Join(", ", gameResult.Scores ?? [])}",
        };

        var turnCount = gameResult.Frames?.Count(f => f.AgentId == 0) ?? 0;
        lines.Add($"TURNS: {turnCount}");

        if (!string.IsNullOrEmpty(result.Summary))
            lines.Add($"FINAL_SUMMARY: {result.Summary}");

        if (gameResult.Frames is { Count: > 0 })
        {
            int turn = 0;
            foreach (var frame in gameResult.Frames)
            {
                if (frame.AgentId < 0)
                {
                    lines.Add("");
                    lines.Add("--- INIT ---");
                    AppendFrameContent(lines, frame);
                    continue;
                }

                if (frame.AgentId == 0)
                {
                    turn++;
                    lines.Add("");
                    lines.Add($"--- TURN {turn} ---");
                }

                AppendFrameContent(lines, frame);
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static void AppendFrameContent(List<string> lines, GameFrame frame)
    {
        if (!string.IsNullOrEmpty(frame.Stderr))
        {
            lines.Add($"[STDERR agent={frame.AgentId}]");
            lines.Add(frame.Stderr.TrimEnd());
        }

        if (!string.IsNullOrEmpty(frame.Stdout))
        {
            lines.Add($"[STDOUT agent={frame.AgentId}]");
            lines.Add(frame.Stdout.TrimEnd());
        }

        if (!string.IsNullOrEmpty(frame.Summary))
            lines.Add($"[SUMMARY] {frame.Summary.TrimEnd()}");

        if (!string.IsNullOrEmpty(frame.GameInformation))
            lines.Add($"[INFO] {frame.GameInformation.TrimEnd()}");
    }

    private void PrintSummary(List<PlayResult> results)
    {
        var wins = results.Count(r => r.IsWin);
        var losses = results.Count - wins;
        var total = results.Count;
        var winrate = total > 0 ? (double)wins / total * 100 : 0;

        Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
        Console.WriteLine($"║  📊 Résultats: {wins} WIN / {losses} LOSS ({winrate:F1}%)");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");

        // Generate summary.md
        var mdLines = new List<string>
        {
            "# 🎮 Play Mode Summary",
            "",
            $"| Metric | Value |",
            $"|--------|-------|",
            $"| Opponent | {(_config.OpponentType == PlayOpponentType.Boss ? "Boss" : $"Player {_config.SpecificPlayerId}")} |",
            $"| Seed | {(_config.ManualSeed ?? "auto")} |",
            $"| Games | {total} |",
            $"| Wins | {wins} |",
            $"| Losses | {losses} |",
            $"| **Winrate** | **{winrate:F1}%** |",
            "",
            "## Détails",
            "",
            "| # | Result | Score | Seed | GameId |",
            "|---|--------|-------|------|--------|"
        };

        for (int i = 0; i < results.Count; i++)
        {
            var r = results[i];
            var icon = r.IsWin ? "✅" : "❌";
            mdLines.Add($"| {i + 1} | {icon} {(r.IsWin ? "WIN" : "LOSS")} | {r.ScoreDiff:+#;-#;0} | {r.Seed} | {r.GameResult.GameId} |");
        }

        var summaryPath = Path.Combine(_config.OutputDirectory, "summary.md");
        File.WriteAllText(summaryPath, string.Join(Environment.NewLine, mdLines));
        Console.WriteLine($"\n📝 Summary: {Path.GetFullPath(summaryPath)}");
    }

    private long DetectUserId()
    {
        if (string.IsNullOrEmpty(_config.Cookie))
            return 0;

        var match = Regex.Match(_config.Cookie, @"rememberMe=([a-fA-F0-9]+)");
        if (match.Success)
        {
            var value = match.Groups[1].Value;
            if (value.Length > 32)
            {
                var userIdPart = value[..^32];
                if (long.TryParse(userIdPart, out var id))
                    return id;
            }
        }
        return 0;
    }
}

public class PlayResult
{
    public GameResult GameResult { get; set; } = null!;
    public string Seed { get; set; } = "";
    public bool IsWin { get; set; }
    public int ScoreDiff { get; set; }
    public string Summary { get; set; } = "";
}
