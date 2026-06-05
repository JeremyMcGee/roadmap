// Feature: drawio-to-markdown, Property 2: Dangling Edge Filtering
using DrawioToMarkdown.Graph;
using DrawioToMarkdown.Parsing;
using DrawioToMarkdown.Tests.Generators;
using FsCheck;
using FsCheck.Xunit;

namespace DrawioToMarkdown.Tests.Graph;

/// <summary>
/// Property 2: Dangling Edge Filtering
/// Validates: Requirements 2.3
///
/// For any ParsedDiagram containing nodes and edges (where some edges reference source or
/// target IDs that do not correspond to any node in the diagram), building the DependencyGraph
/// SHALL include only those edges where both source and target IDs correspond to existing nodes,
/// and all valid edges SHALL still be present.
/// </summary>
public class DanglingEdgePropertyTests
{
    private readonly GraphBuilder _builder = new();

    /// <summary>
    /// Provides the custom Arbitrary for ParsedDiagram to FsCheck.
    /// </summary>
    public static class Arbitraries
    {
        public static Arbitrary<ParsedDiagram> ParsedDiagramArbitrary() =>
            ArbitraryGraphs.ArbParsedDiagram();
    }

    /// <summary>
    /// **Validates: Requirements 2.3**
    ///
    /// After building a DependencyGraph from a ParsedDiagram that may contain dangling edges,
    /// no dependency in the resulting graph references a node ID that doesn't exist in graph.Nodes.
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(Arbitraries) })]
    public bool NoDanglingEdgesInBuiltGraph(ParsedDiagram diagram)
    {
        var graph = _builder.Build(diagram);

        // Every dependency reference must point to an existing node
        return graph.Dependencies.All(kvp =>
            graph.Nodes.ContainsKey(kvp.Key) &&
            kvp.Value.All(depId => graph.Nodes.ContainsKey(depId)));
    }

    /// <summary>
    /// **Validates: Requirements 2.3**
    ///
    /// All valid edges (where both source and target exist in the node set) from the input
    /// ParsedDiagram are present in the resulting DependencyGraph.
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(Arbitraries) })]
    public bool AllValidEdgesArePreserved(ParsedDiagram diagram)
    {
        var graph = _builder.Build(diagram);

        var nodeIds = new HashSet<string>(diagram.Nodes.Select(n => n.Id));

        // Identify valid edges: both source and target are in the node set
        var validEdges = diagram.Edges
            .Where(e => nodeIds.Contains(e.SourceId) && nodeIds.Contains(e.TargetId))
            .ToList();

        // Every valid edge should be present in the graph's dependencies
        return validEdges.All(edge =>
            graph.Dependencies.ContainsKey(edge.TargetId) &&
            graph.Dependencies[edge.TargetId].Contains(edge.SourceId));
    }
}
