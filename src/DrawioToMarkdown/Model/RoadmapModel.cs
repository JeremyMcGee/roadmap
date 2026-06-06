namespace DrawioToMarkdown.Model;

public sealed record RoadmapActivity(
    string Label,
    string Quarter,
    string Category,
    IReadOnlyList<string> DependencyLabels
);

public sealed record RoadmapModel(
    IReadOnlyList<RoadmapActivity> Activities
);
