using System.IO;

namespace CodinGameLogExtractor.Models.Options;

public static class LanguageDetector
{
    public static string DetectFromFile(string filePath)
    {
        var extension = Path.GetExtension(filePath)?.ToLowerInvariant();
        return extension switch
        {
            ".cs" => "C#",
            ".java" => "Java",
            ".py" => "Python3",
            ".js" => "Javascript",
            ".ts" => "Typescript",
            ".rb" => "Ruby",
            ".go" => "Go",
            ".rs" => "Rust",
            ".cpp" or ".cc" or ".cxx" => "C++",
            ".c" => "C",
            ".scala" => "Scala",
            ".kt" or ".kts" => "Kotlin",
            ".swift" => "Swift",
            ".lua" => "Lua",
            ".pl" => "Perl",
            ".hs" => "Haskell",
            ".clj" => "Clojure",
            ".d" => "D",
            ".dart" => "Dart",
            ".php" => "PHP",
            ".bash" or ".sh" => "Bash",
            ".vb" => "VB.NET",
            ".fs" => "F#",
            ".groovy" => "Groovy",
            ".pas" => "Pascal",
            ".m" => "Objective-C",
            _ => "C#",
        };
    }
}
