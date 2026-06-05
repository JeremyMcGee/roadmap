namespace MarkdownToDrawio.Model;

/// <summary>
/// The complete roadmap model containing all activities and their relationships.
/// </summary>
public sealed record RoadmapModel(
    IReadOnlyList<Activity> Activities
);
