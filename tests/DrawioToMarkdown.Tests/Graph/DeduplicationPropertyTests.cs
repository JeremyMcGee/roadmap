// Feature: drawio-to-markdown, Property 3: Edge Deduplication (Idempotence)
using DrawioToMarkdown.Graph;
using DrawioToMarkdown.Parsing;
using DrawioToMarkdown.Tests.Generators;
using FsCheck;
using FsCheck.Xunit;

namespace DrawioToMarkdown.Tests.Graph;

/// <summary>
/// Property 3: Edge Deduplication (Idempotence)
/// Validates: Requirements 2.4
///
/// For any ParsedDiagram containing duplicate edges (multiple edges with the same source and
/// target), the resulting DependencyGraph SHALL contain exactly one dependency relationship
/// between those nodes. Equivalently: adding duplicate edges to the input does not change the
/// resulting graph.
/// </summary>
public class DeduplicationPropertyTests
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
    /// **Validates: Requirements 2.4**
    ///
    /// For any ParsedDiagram, duplicating all edges (concatenating the edge list with itself)
    /// and building the graph produces the same dependency relationships as the original.
    /// This proves that duplicate edges are deduplicated and do not affect the result.
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(Arbitraries) })]
    public bool DuplicatingEdgesDoesNotChangeGraph(ParsedDiagram diagram)
    {
        // Build graph from original diagram
        var originalGraph = _builder.Build(diagram);

        // Create a modified diagram with all edges duplicated
        var duplicatedEdges = diagram.Edges.Concat(diagram.Edges).ToList();
        var duplicatedDiagram = new ParsedDiagram(diagram.Nodes, duplicatedEdges);

        // Build graph from duplicated diagram
        var duplicatedGraph = _builder.Build(duplicatedDiagram);

        // Both graphs should have the same nodes
        if (originalGraph.Nodes.Count != duplicatedGraph.Nodes.Count)
            return false;

        if (!originalGraph.Nodes.Keys.All(k => duplicatedGraph.Nodes.ContainsKey(k)))
            return false;

        // Both graphs should have the same dependency relationships
        if (originalGraph.Dependencies.Count != duplicatedGraph.Dependencies.Count)
            return false;

        return originalGraph.Dependencies.All(kvp =>
            duplicatedGraph.Dependencies.ContainsKey(kvp.Key) &&
            kvp.Value.SetEquals(duplicatedGraph.Dependencies[kvp.Key]));
    }
}
