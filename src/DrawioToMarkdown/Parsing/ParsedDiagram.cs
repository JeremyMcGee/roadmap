using DrawioToMarkdown.Graph;

namespace DrawioToMarkdown.Parsing;

/// <summary>
/// Intermediate representation of raw parsed XML data.
/// </summary>
public sealed record ParsedDiagram(
    IReadOnlyList<ActivityNode> Nodes,
    IReadOnlyList<DependencyEdge> Edges
);
