namespace UtilityAi.Nexus.Abstractions;

public static class CommandLineCommonActions
{
    public const double DefaultHitScore = 0.85;
    public static readonly string[] ReadKeywords = ["read", "cat", "show", "view"];
    public static readonly string[] WriteKeywords = ["write", "create", "append", "save"];
    public static readonly string[] UpdateKeywords = ["update", "edit", "modify", "change"];
    private static readonly HashSet<string> ReadKeywordSet = [.. ReadKeywords];
    private static readonly HashSet<string> WriteKeywordSet = [.. WriteKeywords];
    private static readonly HashSet<string> UpdateKeywordSet = [.. UpdateKeywords];

    public static bool IsCommonAction(string? text)
    {
        return ContainsKeyword(text, ReadKeywords)
            || ContainsKeyword(text, WriteKeywords)
            || ContainsKeyword(text, UpdateKeywords);
    }

    public static double Score(string? text, string[] keywords, double hitScore = DefaultHitScore)
    {
        return ContainsKeyword(text, keywords) ? hitScore : 0.0;
    }

    public static bool ContainsKeyword(string? text, string[] keywords)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var normalized = text.ToLowerInvariant();
        var tokens = normalized.Split(
            [' ', '\t', '\r', '\n', '.', ',', ';', ':', '!', '?', '/', '\\', '-', '_', '(', ')', '[', ']', '{', '}', '"', '\''],
            StringSplitOptions.RemoveEmptyEntries);

        var keywordSet = GetKeywordSet(keywords);
        return tokens.Any(keywordSet.Contains);
    }

    private static HashSet<string> GetKeywordSet(string[] keywords)
    {
        if (ReferenceEquals(keywords, ReadKeywords)) return ReadKeywordSet;
        if (ReferenceEquals(keywords, WriteKeywords)) return WriteKeywordSet;
        if (ReferenceEquals(keywords, UpdateKeywords)) return UpdateKeywordSet;
        return [.. keywords];
    }
}
