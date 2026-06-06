using DrawioToMarkdown.Parsing;

namespace DrawioToMarkdown.Graph;

/// <summary>
/// Builds a DependencyGraph from a parsed diagram.
/// Filters dangling edges, deduplicates edges, and constructs per-node dependency sets.
/// </summary>
public sealed class GraphBuilder : IGraphBuilder
{
    public DependencyGraph Build(ParsedDiagram diagram)
    {
        // Index all activity nodes by ID, mapping to the Graph.ActivityNode type
        var nodes = new Dictionary<string, ActivityNode>();
        foreach (var node in diagram.ActivityNodes)
        {
            nodes[node.Id] = new ActivityNode(node.Id, node.Label);
        }

        // Initialize dependency sets for every node (even those with no dependencies)
        var dependencies = new Dictionary<string, HashSet<string>>();
        foreach (var node in diagram.ActivityNodes)
        {
            dependencies[node.Id] = new HashSet<string>();
        }

        // Process edges: filter dangling, deduplicate via HashSet
        foreach (var edge in diagram.Edges)
        {
            // Skip edges referencing non-existent nodes
            if (!nodes.ContainsKey(edge.SourceId) || !nodes.ContainsKey(edge.TargetId))
            {
                continue;
            }

            // Edge semantics: source is antecedent, target is dependent
            // So Dependencies[target] should contain source
            dependencies[edge.TargetId].Add(edge.SourceId);
        }

        // Convert to read-only types for the graph
        var readOnlyNodes = nodes.AsReadOnly();
        var readOnlyDependencies = dependencies.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlySet<string>)kvp.Value
        ).AsReadOnly();

        return new DependencyGraph(readOnlyNodes, readOnlyDependencies);
    }
}
