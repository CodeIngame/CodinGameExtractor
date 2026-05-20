using System;
using System.IO;
using System.Threading.Tasks;
using CodinGameLogExtractor.Services;

namespace CodinGameLogExtractor;

class Program
{
    static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        string testSessionHandle = ""; // Provide your test session handle here or as first argument
        long userId = 0; // Auto-détecté via l'API ranking
        string outputDirectory = "./codingame_logs";
        int maxConcurrentRequests = 5;

        if (args.Length > 0)
            testSessionHandle = args[0];
        if (args.Length > 1 && long.TryParse(args[1], out var id))
            userId = id;
        if (args.Length > 2)
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
