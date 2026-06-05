namespace DrawioToMarkdown.Graph;

/// <summary>
/// The validated dependency graph with resolved relationships.
/// </summary>
public sealed class DependencyGraph
{
    /// <summary>
    /// All activity nodes in the graph, keyed by ID.
    /// </summary>
    public IReadOnlyDictionary<string, ActivityNode> Nodes { get; }

    /// <summary>
    /// For each node ID, the set of antecedent (prerequisite) node IDs.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlySet<string>> Dependencies { get; }

    public DependencyGraph(
        IReadOnlyDictionary<string, ActivityNode> nodes,
        IReadOnlyDictionary<string, IReadOnlySet<string>> dependencies)
    {
        Nodes = nodes;
        Dependencies = dependencies;
    }
}
