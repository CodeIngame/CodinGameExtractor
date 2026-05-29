using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CodinGameLogExtractor.Models;
using CodinGameLogExtractor.Models.Api;

namespace CodinGameLogExtractor.Services;

public class CodinGameExtractor
{
    private const string BaseUrl = "https://www.codingame.com/services";
    private readonly HttpClient _httpClient;
    private readonly string _testSessionHandle;
    private long _userId;
    private readonly string _outputDirectory;
    private readonly int _maxConcurrentRequests;
    private readonly string? _cookie;

    public CodinGameExtractor(
        string testSessionHandle,
        long userId,
        string outputDirectory = "./codingame_logs",
        int maxConcurrentRequests = 5,
        string? cookie = null)
    {
        _testSessionHandle = testSessionHandle ?? throw new ArgumentNullException(nameof(testSessionHandle));
        _userId = userId;
        _outputDirectory = outputDirectory;
        _maxConcurrentRequests = maxConcurrentRequests;
        _cookie = cookie;

        var cookieContainer = new System.Net.CookieContainer();
        var handler = new HttpClientHandler { CookieContainer = cookieContainer, UseCookies = true };
        _httpClient = new HttpClient(handler);
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "fr-FR,fr;q=0.5");
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

        if (!string.IsNullOrEmpty(cookie))
        {
            var uri = new Uri("https://www.codingame.com");
            foreach (var part in cookie.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
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

        if (Directory.Exists(_outputDirectory))
        {
            foreach (var dir in Directory.GetDirectories(_outputDirectory))
            {
                try { Directory.Delete(dir, true); }
                catch (IOException) { }
            }
            foreach (var file in Directory.GetFiles(_outputDirectory))
            {
                try { File.Delete(file); }
                catch (IOException) { }
            }
        }
        else
        {
            Directory.CreateDirectory(_outputDirectory);
        }
    }

    public async Task RunAsync()
    {
        try
        {
            var ranking = await GetArenaRankingAsync();

            if (ranking?.Codingamer != null && _userId == 0)
            {
                _userId = ranking.Codingamer.UserId;
                Console.WriteLine($"🔑 UserId auto-détecté via ranking: {_userId} ({ranking.Codingamer.Pseudo})\n");
            }

            if (_userId == 0)
            {
                var loggedIn = await GetLoggedInUserAsync();
                if (loggedIn != null)
                {
                    _userId = loggedIn.UserId;
                    Console.WriteLine($"🔑 UserId auto-détecté via session: {_userId} ({loggedIn.Pseudo})\n");
                }
            }

            if (_userId == 0)
            {
                _userId = ExtractUserIdFromCookie();
                if (_userId != 0)
                    Console.WriteLine($"🔑 UserId extrait du cookie rememberMe: {_userId}\n");
            }

            if (_userId == 0)
            {
                Console.WriteLine("❌ Impossible de détecter le userId. Spécifiez-le en argument.");
                return;
            }

            var gameResults = await ExtractAllLogsAsync();
            SaveLogsToFiles(gameResults, ranking);

            Console.WriteLine($"\n✅ Extraction terminée!");
            Console.WriteLine($"📁 Fichiers sauvegardés dans: {Path.GetFullPath(_outputDirectory)}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Erreur fatale: {ex.Message}");
            throw;
        }
    }

    public async Task<ArenaRanking?> GetArenaRankingAsync()
    {
        try
        {
            var url = $"{BaseUrl}/Leaderboards/getUserArenaDivisionRoomRankingByTestSessionHandle";
            var json = JsonSerializer.Serialize(new object[] { _testSessionHandle, "global" });
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            Console.WriteLine("🏆 Récupération du ranking...");
            var response = await _httpClient.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"   ⚠️ Ranking indisponible: {response.StatusCode}");
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var ranking = JsonSerializer.Deserialize<ArenaRanking>(responseContent);
            if (ranking != null)
            {
                Console.WriteLine($"   📍 {ranking.Pseudo} - Rank {ranking.Rank}/{ranking.Total} (score: {ranking.Score}) - {ranking.ProgrammingLanguage}\n");
            }
            return ranking;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ⚠️ Ranking: {ex.Message}");
            return null;
        }
    }

    public async Task<GameCodingamer?> GetLoggedInUserAsync()
    {
        try
        {
            var url = $"{BaseUrl}/CodinGamer/getMyProperties";
            var content = new StringContent("[null]", System.Text.Encoding.UTF8, "application/json");

            Console.WriteLine("🔍 Détection de l'utilisateur connecté...");
            var response = await _httpClient.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"   ⚠️ Session invalide: {response.StatusCode}");
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseContent);
            var root = doc.RootElement;

            long userId = 0;
            string? pseudo = null;

            if (root.TryGetProperty("userId", out var uid))
                userId = uid.GetInt64();
            if (root.TryGetProperty("pseudo", out var p))
                pseudo = p.GetString();

            if (userId != 0)
                return new GameCodingamer { UserId = userId, Pseudo = pseudo };

            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ⚠️ Session: {ex.Message}");
            return null;
        }
    }

    private long ExtractUserIdFromCookie()
    {
        if (string.IsNullOrEmpty(_cookie))
            return 0;

        var match = Regex.Match(_cookie, @"rememberMe=([a-fA-F0-9]+)");
        if (match.Success)
        {
            var value = match.Groups[1].Value;
            // Format: {userId (digits)}{32-char hex token}
            if (value.Length > 32)
            {
                var userIdPart = value[..^32];
                if (long.TryParse(userIdPart, out var id))
                    return id;
            }
        }

        return 0;
    }

    public async Task<List<long>> GetGameIdsFromTestSessionAsync()
    {
        try
        {
            var url = $"{BaseUrl}/gamesPlayersRanking/findLastBattlesByTestSessionHandle";
            var json = JsonSerializer.Serialize(new object?[] { _testSessionHandle, null });
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            Console.WriteLine("🔍 Récupération des battles...");
            Console.WriteLine($"   URL: {url}");
            Console.WriteLine($"   Payload: [{_testSessionHandle}..., null]\n");

            var response = await _httpClient.PostAsync(url, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"   ❌ Erreur: {response.StatusCode}");
                var preview = responseContent.Length > 500 ? responseContent[..500] : responseContent;
                Console.WriteLine($"   Response: {preview}\n");
                return [];
            }

            using var doc = JsonDocument.Parse(responseContent);
            var gameIds = new List<long>();

            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    if (item.TryGetProperty("gameId", out var gameId))
                    {
                        gameIds.Add(gameId.GetInt64());
                    }
                }
            }

            Console.WriteLine($"✅ {gameIds.Count} parties trouvées\n");
            return gameIds;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erreur lors de la récupération des parties: {ex.Message}\n");
            return [];
        }
    }

    public async Task<GameResult?> GetGameResultAsync(long gameId)
    {
        try
        {
            var url = $"{BaseUrl}/gameResult/findByGameId";
            var json = JsonSerializer.Serialize(new object[] { gameId, _userId });
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                var preview = errorBody.Length > 200 ? errorBody[..200] : errorBody;
                Console.WriteLine($"⚠️  Game {gameId}: Status {response.StatusCode} - {preview}");
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<GameResult>(responseContent);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Game {gameId}: {ex.Message}");
            return null;
        }
    }

    public async Task<List<GameResult>> ExtractAllLogsAsync()
    {
        Console.WriteLine("🔍 Récupération des parties...\n");
        var gameIds = await GetGameIdsFromTestSessionAsync();

        if (gameIds.Count == 0)
        {
            Console.WriteLine("⚠️  Aucune partie trouvée");
            Console.WriteLine($"   testSessionHandle utilisé: {_testSessionHandle}");
            return [];
        }

        Console.WriteLine($"📥 Téléchargement de {gameIds.Count} replays (max {_maxConcurrentRequests} en parallèle)...\n");

        var results = new List<GameResult>();
        var semaphore = new SemaphoreSlim(_maxConcurrentRequests);
        var tasks = new List<Task>();

        foreach (var gameId in gameIds)
        {
            await semaphore.WaitAsync();

            var task = Task.Run(async () =>
            {
                try
                {
                    var gameResult = await GetGameResultAsync(gameId);
                    if (gameResult != null)
                    {
                        lock (results)
                        {
                            results.Add(gameResult);
                        }
                        var seed = ExtractSeed(gameResult.RefereeInput);
                        var turnCount = gameResult.Frames?.Count(f => f.AgentId == 0) ?? 0;
                        var fallbacks = CountFallbackTimeouts(gameResult);
                        var fbInfo = fallbacks > 0 ? $" - ⚠️FallbackTO: {fallbacks}" : "";
                        Console.WriteLine($"✅ Game {gameResult.GameId} - Seed: {seed} - Turns: {turnCount}{fbInfo}");
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            });

            tasks.Add(task);
        }

        await Task.WhenAll(tasks);
        Console.WriteLine($"\n✅ {results.Count}/{gameIds.Count} parties récupérées");
        return results;
    }

    public void SaveLogsToFiles(List<GameResult> gameResults, ArenaRanking? ranking = null)
    {
        Console.WriteLine($"\n💾 Sauvegarde des logs dans {_outputDirectory}...\n");

        var sorted = gameResults
            .OrderBy(g => GetResult(g) == "WIN" ? 0 : 1)
            .ThenBy(g => GetOpponentRank(g))
            .ToList();
        int wins = 0, losses = 0, timeouts = 0, totalFallbacks = 0;
        var bucketStats = new Dictionary<string, (int Wins, int Losses, int Timeouts)>();
        var gameSummaries = new List<(string Result, int OppRank, string OppPseudo, int ScoreDiff, int Turns, int Fallbacks, long GameId, string Seed, bool IsTimeout)>();

        foreach (var gameResult in sorted)
        {
            try
            {
                var fileContent = FormatGameLog(gameResult);
                var seed = ExtractSeed(gameResult.RefereeInput);
                var result = GetResult(gameResult);
                var oppRank = GetOpponentRank(gameResult);
                var oppPseudo = GetOpponentPseudo(gameResult);
                var isTimeout = result == "LOSS" && HasMyTimeout(gameResult);
                var scoreDiff = GetScoreDiff(gameResult);
                var turnCount = gameResult.Frames?.Count(f => f.AgentId == 0) ?? 0;
                var fallbacks = CountFallbackTimeouts(gameResult);
                totalFallbacks += fallbacks;

                if (result == "WIN") wins++;
                else if (isTimeout) { losses++; timeouts++; }
                else losses++;

                var bucketStart = (oppRank - 1) / 100 * 100 + 1;
                var bucketEnd = bucketStart + 99;
                var bucketName = $"{bucketStart:D4}-{bucketEnd:D4}";

                if (!bucketStats.ContainsKey(bucketName))
                    bucketStats[bucketName] = (0, 0, 0);
                var bs = bucketStats[bucketName];
                if (result == "WIN") bucketStats[bucketName] = (bs.Wins + 1, bs.Losses, bs.Timeouts);
                else if (isTimeout) bucketStats[bucketName] = (bs.Wins, bs.Losses + 1, bs.Timeouts + 1);
                else bucketStats[bucketName] = (bs.Wins, bs.Losses + 1, bs.Timeouts);

                gameSummaries.Add((result, oppRank, oppPseudo, scoreDiff, turnCount, fallbacks, gameResult.GameId, seed, isTimeout));

                var categoryDir = isTimeout
                    ? Path.Combine(_outputDirectory, "loss", "timeout", bucketName)
                    : Path.Combine(_outputDirectory, result.ToLower(), bucketName);

                if (!Directory.Exists(categoryDir))
                    Directory.CreateDirectory(categoryDir);

                var safePseudo = Regex.Replace(oppPseudo, @"[^\w\-]", "_");
                var prefix = isTimeout ? "TIMEOUT" : result;
                var fileName = $"{prefix}_{oppRank:D4}_{safePseudo}_seed={seed}_gameId={gameResult.GameId}.txt";
                var filePath = Path.Combine(categoryDir, fileName);

                File.WriteAllText(filePath, fileContent);
                var icon = isTimeout ? "⏱️" : "📄";
                Console.WriteLine($"{icon} {prefix,-7} rank={oppRank,-4} {fileName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erreur sauvegarde Game {gameResult.GameId}: {ex.Message}");
            }
        }

        var total = wins + losses;
        var winrate = total > 0 ? (double)wins / total * 100 : 0;
        var fbInfo = totalFallbacks > 0 ? $", {totalFallbacks} fallbackTO" : "";
        Console.WriteLine($"\n📊 Résultats: {wins} WIN / {losses} LOSS ({winrate:F1}%) (dont {timeouts} timeout{fbInfo})");

        GenerateSummaryMarkdown(gameSummaries, bucketStats, wins, losses, timeouts, totalFallbacks, ranking);
    }

    private static string ExtractSeed(string? refereeInput)
    {
        if (string.IsNullOrEmpty(refereeInput))
            return "Unknown";

        var match = Regex.Match(refereeInput, @"seed=(-?\d+)");
        return match.Success ? match.Groups[1].Value : "Unknown";
    }

    private string GetResult(GameResult gameResult)
    {
        var myAgent = gameResult.Agents?.FirstOrDefault(a => a.Codingamer?.UserId == _userId);
        if (myAgent == null || gameResult.Ranks == null || myAgent.Index >= gameResult.Ranks.Count)
            return "UNKNOWN";

        return gameResult.Ranks[myAgent.Index] == 0 ? "WIN" : "LOSS";
    }

    private int GetOpponentRank(GameResult gameResult)
    {
        var opponent = gameResult.Agents?.FirstOrDefault(a => a.Codingamer?.UserId != _userId);
        return opponent?.Rank ?? 9999;
    }

    private string GetOpponentPseudo(GameResult gameResult)
    {
        var opponent = gameResult.Agents?.FirstOrDefault(a => a.Codingamer?.UserId != _userId);
        return opponent?.Codingamer?.Pseudo ?? "Unknown";
    }

    private int GetScoreDiff(GameResult gameResult)
    {
        var myAgent = gameResult.Agents?.FirstOrDefault(a => a.Codingamer?.UserId == _userId);
        if (myAgent == null || gameResult.Scores == null || myAgent.Index >= gameResult.Scores.Count)
            return 0;

        var myScore = gameResult.Scores[myAgent.Index];
        var oppScore = gameResult.Scores.Where((_, i) => i != myAgent.Index).FirstOrDefault();
        return (int)(myScore - oppScore);
    }

    private string FormatGameLog(GameResult gameResult)
    {
        var lines = new List<string>();
        var seed = ExtractSeed(gameResult.RefereeInput);

        lines.Add($"GAME_ID: {gameResult.GameId}");
        lines.Add($"REPLAY: https://www.codingame.com/replay/{gameResult.GameId}");
        lines.Add($"SEED: {seed}");
        lines.Add($"SCORES: {string.Join(", ", gameResult.Scores ?? [])}");
        lines.Add($"SCORE_DIFF: {GetScoreDiff(gameResult):+#;-#;0}");

        var turnCount = gameResult.Frames?.Count(f => f.AgentId == 0) ?? 0;
        lines.Add($"TURNS: {turnCount}");

        if (gameResult.Ranks is { Count: > 0 })
        {
            lines.Add($"RANKS: {string.Join(", ", gameResult.Ranks)}");
        }

        if (gameResult.Agents is { Count: > 0 })
        {
            foreach (var agent in gameResult.Agents)
            {
                var pseudo = agent.Codingamer?.Pseudo ?? "Unknown";
                var score = agent.Score?.ToString("F2") ?? "N/A";
                var rank = agent.Rank ?? -1;
                lines.Add($"AGENT_{agent.Index}: {pseudo} (score={score}, rank={rank}, valid={agent.Valid})");
            }
        }

        var fallbackCount = CountFallbackTimeouts(gameResult);
        if (fallbackCount > 0)
        {
            lines.Add($"FALLBACK_TIMEOUTS: {fallbackCount}");
        }

        if (gameResult.Frames is { Count: > 0 })
        {
            int turn = 0;
            for (int i = 0; i < gameResult.Frames.Count; i++)
            {
                var frame = gameResult.Frames[i];

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

        if (gameResult.Frames?.LastOrDefault()?.Summary is { Length: > 0 } summary)
        {
            lines.Add("");
            lines.Add($"SUMMARY: {summary}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private bool HasMyTimeout(GameResult gameResult)
    {
        var myAgent = gameResult.Agents?.FirstOrDefault(a => a.Codingamer?.UserId == _userId);
        if (myAgent == null || gameResult.Frames == null)
            return false;

        var myIndex = myAgent.Index;

        foreach (var frame in gameResult.Frames)
        {
            var summary = frame.Summary ?? "";
            var info = frame.GameInformation ?? "";

            if (summary.Contains($"${myIndex} has not provided", StringComparison.OrdinalIgnoreCase)
                || info.Contains($"${myIndex} has not provided", StringComparison.OrdinalIgnoreCase)
                || summary.Contains($"${myIndex}: timeout", StringComparison.OrdinalIgnoreCase)
                || info.Contains($"${myIndex}: timeout", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private int CountFallbackTimeouts(GameResult gameResult)
    {
        var myAgent = gameResult.Agents?.FirstOrDefault(a => a.Codingamer?.UserId == _userId);
        if (myAgent == null || gameResult.Frames == null)
            return 0;

        var myIndex = myAgent.Index;
        int count = 0;

        foreach (var frame in gameResult.Frames)
        {
            if (frame.AgentId == myIndex && !string.IsNullOrEmpty(frame.Stderr))
            {
                foreach (var line in frame.Stderr.Split('\n'))
                {
                    if (Regex.IsMatch(line, @"Bot \d+: TIMEOUT "))
                        count++;
                }
            }
        }

        return count;
    }

    private void GenerateSummaryMarkdown(
        List<(string Result, int OppRank, string OppPseudo, int ScoreDiff, int Turns, int Fallbacks, long GameId, string Seed, bool IsTimeout)> games,
        Dictionary<string, (int Wins, int Losses, int Timeouts)> bucketStats,
        int wins, int losses, int timeouts, int totalFallbacks,
        ArenaRanking? ranking)
    {
        var total = wins + losses;
        var winrate = total > 0 ? (double)wins / total * 100 : 0;
        var lines = new List<string>
        {
            "# 📊 CodinGame Battle Summary",
            ""
        };

        if (ranking != null)
        {
            var league = ranking.League != null
                ? $"Division {ranking.League.DivisionCount - ranking.League.DivisionIndex}/{ranking.League.DivisionCount}"
                : "";
            lines.AddRange([
                "## 🏆 Arena Ranking",
                "",
                $"| Metric | Value |",
                $"|--------|-------|",
                $"| Player | **{ranking.Pseudo}** |",
                $"| Rank | **{ranking.Rank}** / {ranking.Total} |",
                $"| Score | {ranking.Score} |",
                $"| League | {league} |",
                $"| Language | {ranking.ProgrammingLanguage} |",
                ""
            ]);
        }

        lines.AddRange([
            "## Global",
            "",
            $"| Metric | Value |",
            $"|--------|-------|",
            $"| Games | {total} |",
            $"| Wins | {wins} |",
            $"| Losses | {losses} |",
            $"| **Winrate** | **{winrate:F1}%** |",
            $"| Timeouts | {timeouts} |",
            $"| Fallback TOs | {totalFallbacks} |",
            "",
            "## Winrate par tranche de rank",
            "",
            "| Rank | W | L | TO | Winrate |",
            "|------|---|---|----|---------|"
        ]);

        foreach (var (bucket, stats) in bucketStats.OrderBy(kv => kv.Key))
        {
            var bTotal = stats.Wins + stats.Losses;
            var bWr = bTotal > 0 ? (double)stats.Wins / bTotal * 100 : 0;
            var toInfo = stats.Timeouts > 0 ? $"{stats.Timeouts}" : "-";
            lines.Add($"| {bucket} | {stats.Wins} | {stats.Losses} | {toInfo} | {bWr:F0}% |");
        }

        lines.Add("");
        lines.Add("## Losses");
        lines.Add("");
        lines.Add("| Rank | Opponent | Score | Turns | FB | Replay |");
        lines.Add("|------|----------|-------|-------|----|--------|");

        foreach (var g in games.Where(g => g.Result == "LOSS").OrderBy(g => g.OppRank))
        {
            var tag = g.IsTimeout ? " ⏱️" : "";
            var fb = g.Fallbacks > 0 ? $"{g.Fallbacks}" : "-";
            lines.Add($"| {g.OppRank} | {g.OppPseudo}{tag} | {g.ScoreDiff:+#;-#;0} | {g.Turns} | {fb} | [replay](https://www.codingame.com/replay/{g.GameId}) |");
        }

        lines.Add("");
        lines.Add("## Wins");
        lines.Add("");
        lines.Add("| Rank | Opponent | Score | Turns | FB | Replay |");
        lines.Add("|------|----------|-------|-------|----|--------|");

        foreach (var g in games.Where(g => g.Result == "WIN").OrderBy(g => g.OppRank))
        {
            var fb = g.Fallbacks > 0 ? $"{g.Fallbacks}" : "-";
            lines.Add($"| {g.OppRank} | {g.OppPseudo} | {g.ScoreDiff:+#;-#;0} | {g.Turns} | {fb} | [replay](https://www.codingame.com/replay/{g.GameId}) |");
        }

        var filePath = Path.Combine(_outputDirectory, "summary.md");
        File.WriteAllText(filePath, string.Join(Environment.NewLine, lines));
        Console.WriteLine($"\n📝 Summary: {Path.GetFullPath(filePath)}");
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
        {
            lines.Add($"[SUMMARY] {frame.Summary.TrimEnd()}");
        }

        if (!string.IsNullOrEmpty(frame.GameInformation))
        {
            lines.Add($"[INFO] {frame.GameInformation.TrimEnd()}");
        }
    }
}
