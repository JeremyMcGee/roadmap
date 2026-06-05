namespace DrawioToMarkdown.Graph;

/// <summary>
/// A single activity node extracted from the diagram.
/// </summary>
public sealed record ActivityNode(string Id, string Label);

/// <summary>
/// A directed edge representing a dependency relationship.
/// Source is the antecedent (prerequisite), Target is the dependent.
/// </summary>
public sealed record DependencyEdge(string SourceId, string TargetId);
