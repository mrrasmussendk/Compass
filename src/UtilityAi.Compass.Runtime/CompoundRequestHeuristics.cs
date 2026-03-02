namespace UtilityAi.Compass.Runtime;

internal static class CompoundRequestHeuristics
{
    private static readonly string[] AdvicePromptIndicators =
    [
        "what should i do",
        "what do i do",
        "what should we do",
        "what do we do"
    ];

    /// <summary>
    /// Detects narrative prompts that ask for guidance after describing past events,
    /// which should not be treated as executable compound requests.
    /// </summary>
    /// <param name="text">Input text in any casing.</param>
    /// <returns><see langword="true"/> when the text matches an advice-seeking prompt pattern.</returns>
    public static bool IsAdvicePrompt(string text)
    {
        foreach (var indicator in AdvicePromptIndicators)
        {
            if (text.Contains(indicator, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
