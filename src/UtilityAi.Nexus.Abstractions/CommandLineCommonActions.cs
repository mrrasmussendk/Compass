namespace UtilityAi.Nexus.Abstractions;

public static class CommandLineCommonActions
{
    public static readonly string[] ReadKeywords = ["read", "cat", "show", "view"];
    public static readonly string[] WriteKeywords = ["write", "create", "append", "save"];
    public static readonly string[] UpdateKeywords = ["update", "edit", "modify", "change"];

    public static bool IsCommonAction(string? text)
    {
        return ContainsKeyword(text, ReadKeywords)
            || ContainsKeyword(text, WriteKeywords)
            || ContainsKeyword(text, UpdateKeywords);
    }

    public static double Score(string? text, string[] keywords, double hitScore = 0.85)
    {
        return ContainsKeyword(text, keywords) ? hitScore : 0.0;
    }

    public static bool ContainsKeyword(string? text, string[] keywords)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var normalized = text.ToLowerInvariant();
        return keywords.Any(normalized.Contains);
    }
}
