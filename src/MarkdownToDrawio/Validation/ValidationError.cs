namespace MarkdownToDrawio.Validation;

/// <summary>
/// A validation error with contextual information.
/// </summary>
public sealed record ValidationError(
    string ActivityLabel,
    string Message
);
