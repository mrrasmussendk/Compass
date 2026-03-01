namespace UtilityAi.Compass.Abstractions.Facts;

/// <summary>
/// Represents a single turn in a conversation between the user and the AI.
/// </summary>
public sealed record ConversationTurn
{
    /// <summary>
    /// Optional conversation/session ID to group related turns together.
    /// </summary>
    public string? ConversationId { get; init; }

    /// <summary>
    /// The user's message.
    /// </summary>
    public required string UserMessage { get; init; }

    /// <summary>
    /// The AI's response.
    /// </summary>
    public required string AssistantResponse { get; init; }

    /// <summary>
    /// When this turn occurred.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
