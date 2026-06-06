namespace DrawioToMarkdown.Parsing;

/// <summary>
/// A category swimlane extracted from the draw.io diagram with its bounding geometry.
/// </summary>
public sealed record SwimlaneDef(string Id, string Label, double X, double Y, double Width, double Height);

/// <summary>
/// A quarter column label extracted from the draw.io diagram with its horizontal extent.
/// </summary>
public sealed record QuarterColumnDef(string Id, string Label, double X, double Width);

/// <summary>
/// An activity node extracted from the draw.io diagram with its position and size.
/// </summary>
public sealed record ActivityNodeDef(string Id, string Label, double X, double Y, double Width, double Height);

/// <summary>
/// A directed dependency edge between two activity nodes.
/// Source is the antecedent (prerequisite), Target is the dependent.
/// </summary>
public sealed record DependencyEdgeDef(string SourceId, string TargetId);

/// <summary>
/// Intermediate representation of all structural elements parsed from a draw.io XML diagram.
/// </summary>
public sealed record ParsedDiagram(
    IReadOnlyList<SwimlaneDef> Swimlanes,
    IReadOnlyList<QuarterColumnDef> QuarterColumns,
    IReadOnlyList<ActivityNodeDef> ActivityNodes,
    IReadOnlyList<DependencyEdgeDef> Edges
);
