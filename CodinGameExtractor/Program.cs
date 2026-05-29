using System;
using System.IO;
using System.Threading.Tasks;
using CommandLine;
using CodinGameLogExtractor.Models;
using CodinGameLogExtractor.Models.Options;
using CodinGameLogExtractor.Services;

namespace CodinGameLogExtractor;

class Program
{
    static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        var result = await Parser.Default.ParseArguments<ExtractOptions, PlayOptions>(args)
            .MapResult(
                (ExtractOptions opts) => RunExtractMode(opts),
                (PlayOptions opts) => RunPlayMode(opts),
                _ => Task.FromResult(1));

        return result;
    }

    static async Task<int> RunPlayMode(PlayOptions opts)
    {
        if (string.IsNullOrEmpty(opts.Cookie))
        {
            Console.WriteLine("❌ Variable d'environnement CODINGAME_COOKIE requise.");
            return 1;
        }

        if (string.IsNullOrEmpty(opts.TestSessionHandle))
        {
            Console.WriteLine("❌ Variable d'environnement CODINGAME_SESSION requise.");
            return 1;
        }

        var config = opts.ToPlayConfig();
        Console.WriteLine($"🔤 Langage détecté: {config.ProgrammingLanguageId}");

        var service = new PlayService(config);
        await service.RunAsync();
        return 0;
    }

    static async Task<int> RunExtractMode(ExtractOptions opts)
    {
        if (string.IsNullOrEmpty(opts.Cookie))
        {
            Console.WriteLine("❌ Variable d'environnement CODINGAME_COOKIE requise.");
            return 1;
        }

        if (string.IsNullOrEmpty(opts.TestSessionHandle))
        {
            Console.WriteLine("❌ Variable d'environnement CODINGAME_SESSION requise.");
            return 1;
        }

        Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║       🚀 CodinGame Log Extractor                               ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine("📋 Configuration:");
        Console.WriteLine($"   Session: {opts.TestSessionHandle[..Math.Min(12, opts.TestSessionHandle.Length)]}...");
        Console.WriteLine($"   User ID: {(opts.UserId == 0 ? "auto-détecté" : opts.UserId)}");
        Console.WriteLine($"   Output:  {Path.GetFullPath(opts.OutputDirectory)}");
        Console.WriteLine($"   Threads: {opts.MaxConcurrentRequests}");
        Console.WriteLine($"   Auth:    Cookie provided ✅");
        Console.WriteLine();

        var extractor = new CodinGameExtractor(opts.TestSessionHandle, opts.UserId, opts.OutputDirectory, opts.MaxConcurrentRequests, opts.Cookie);
        await extractor.RunAsync();
        return 0;
    }
}
