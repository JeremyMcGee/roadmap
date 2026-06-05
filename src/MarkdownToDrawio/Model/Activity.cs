namespace MarkdownToDrawio.Model;

/// <summary>
/// A single activity with its metadata extracted from Markdown.
/// </summary>
public sealed record Activity(
    string Label,
    string? Quarter,
    string? Category,
    IReadOnlyList<string> DependencyLabels
);
