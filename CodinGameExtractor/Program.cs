using System;
using System.IO;
using System.Threading.Tasks;
using CodinGameLogExtractor.Models;
using CodinGameLogExtractor.Services;

namespace CodinGameLogExtractor;

class Program
{
    static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        if (args.Length > 0 && args[0].Equals("play", StringComparison.OrdinalIgnoreCase))
        {
            await RunPlayMode(args[1..]);
            return;
        }

        await RunExtractMode(args);
    }

    static async Task RunPlayMode(string[] args)
    {
        var config = new PlayConfig
        {
            TestSessionHandle = Environment.GetEnvironmentVariable("CODINGAME_SESSION") ?? "76693558e8f09ec6d8bfd2119b8de33b6a225acf",
            Cookie = Environment.GetEnvironmentVariable("CODINGAME_COOKIE"),
            OutputDirectory = Environment.GetEnvironmentVariable("CODINGAME_OUTPUT") ?? "./codingame_play_logs",
        };

        // play <codeFile> [numberOfGames] [--boss|--player <id>] [--seed <seed>] [--session <handle>] [--output <dir>] [--lang <id>]
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: play <codeFile> [numberOfGames] [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --boss                  Jouer contre le boss (défaut)");
            Console.WriteLine("  --player <id>           Jouer contre un joueur spécifique");
            Console.WriteLine("  --seed <seed>           Seed manuel (sinon automatique)");
            Console.WriteLine("  --session <handle>      testSessionHandle");
            Console.WriteLine("  --output <dir>          Dossier de sortie");
            Console.WriteLine("  --lang <id>             Langage (défaut: C#)");
            Console.WriteLine("  --cookie <cookie>       Cookie d'auth (ou env CODINGAME_COOKIE)");
            return;
        }

        config.CodeFilePath = args[0];

        for (int i = 1; i < args.Length; i++)
        {
            if (int.TryParse(args[i], out var n) && !args[i].StartsWith("-"))
            {
                config.NumberOfGames = n;
            }
            else if (args[i] == "--boss")
            {
                config.OpponentType = PlayOpponentType.Boss;
            }
            else if (args[i] == "--player" && i + 1 < args.Length)
            {
                config.OpponentType = PlayOpponentType.SpecificPlayer;
                config.SpecificPlayerId = long.Parse(args[++i]);
            }
            else if (args[i] == "--seed" && i + 1 < args.Length)
            {
                config.ManualSeed = args[++i];
            }
            else if (args[i] == "--session" && i + 1 < args.Length)
            {
                config.TestSessionHandle = args[++i];
            }
            else if (args[i] == "--output" && i + 1 < args.Length)
            {
                config.OutputDirectory = args[++i];
            }
            else if (args[i] == "--lang" && i + 1 < args.Length)
            {
                config.ProgrammingLanguageId = args[++i];
            }
            else if (args[i] == "--cookie" && i + 1 < args.Length)
            {
                config.Cookie = args[++i];
            }
        }

        if (string.IsNullOrEmpty(config.Cookie))
        {
            Console.WriteLine("🔑 Cookie d'authentification requis.");
            Console.Write("   Collez votre cookie: ");
            config.Cookie = Console.ReadLine()?.Trim();
            Console.WriteLine();

            if (string.IsNullOrEmpty(config.Cookie))
            {
                Console.WriteLine("❌ Cookie obligatoire. Abandon.");
                return;
            }
        }

        var service = new PlayService(config);
        await service.RunAsync();
    }

    static async Task RunExtractMode(string[] args)
    {
        string testSessionHandle = "76693558e8f09ec6d8bfd2119b8de33b6a225acf";
        long userId = 0;
        string outputDirectory = Environment.GetEnvironmentVariable("CODINGAME_OUTPUT") ?? "./codingame_logs";
        int maxConcurrentRequests = 5;

        if (args.Length > 0 && !string.IsNullOrEmpty(args[0]))
            testSessionHandle = args[0];
        if (args.Length > 1 && long.TryParse(args[1], out var id))
            userId = id;
        if (args.Length > 2 && !string.IsNullOrEmpty(args[2]))
            outputDirectory = args[2];
        if (args.Length > 3 && int.TryParse(args[3], out var maxRequests))
            maxConcurrentRequests = maxRequests;

        string? cookie = Environment.GetEnvironmentVariable("CODINGAME_COOKIE");
        if (args.Length > 4)
            cookie = args[4];

        Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║       🚀 CodinGame Log Extractor                               ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        if (string.IsNullOrEmpty(testSessionHandle))
        {
            Console.WriteLine("📋 testSessionHandle requis.");
            Console.WriteLine("   (DevTools F12 → Network → findLastBattlesByTestSessionHandle → 1er paramètre du body)");
            Console.WriteLine();
            Console.Write("   Collez votre testSessionHandle: ");
            testSessionHandle = Console.ReadLine()?.Trim() ?? "";
            Console.WriteLine();

            if (string.IsNullOrEmpty(testSessionHandle))
            {
                Console.WriteLine("❌ testSessionHandle obligatoire. Abandon.");
                return;
            }
        }

        if (string.IsNullOrEmpty(cookie))
        {
            Console.WriteLine("🔑 Cookie d'authentification requis.");
            Console.WriteLine("   (DevTools F12 → Network → Clic sur une requête codingame → Headers → Cookie)");
            Console.WriteLine("   Ressemble à: intercom-id-xxx=...; rememberMe=...; cgSession=...; AWSALB=...");
            Console.WriteLine("   Vous pouvez coller le cookie complet ou juste rememberMe=...");
            Console.WriteLine();
            Console.Write("   Collez votre cookie: ");
            cookie = Console.ReadLine()?.Trim();
            Console.WriteLine();

            if (string.IsNullOrEmpty(cookie))
            {
                Console.WriteLine("❌ Cookie obligatoire pour récupérer les replays. Abandon.");
                return;
            }
        }

        Console.WriteLine("📋 Configuration:");
        Console.WriteLine($"   Session: {testSessionHandle[..Math.Min(12, testSessionHandle.Length)]}...");
        Console.WriteLine($"   User ID: {userId}");
        Console.WriteLine($"   Output:  {Path.GetFullPath(outputDirectory)}");
        Console.WriteLine($"   Threads: {maxConcurrentRequests}");
        Console.WriteLine($"   Auth:    {(string.IsNullOrEmpty(cookie) ? "❌ Aucun cookie" : "Cookie provided ✅")}");
        Console.WriteLine();

        var extractor = new CodinGameExtractor(testSessionHandle, userId, outputDirectory, maxConcurrentRequests, cookie);
        await extractor.RunAsync();
    }
}
