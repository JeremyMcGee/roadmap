// Feature: drawio-to-markdown, Property 1: Parse/Print Round-Trip
using DrawioToMarkdown.Graph;
using DrawioToMarkdown.Output;
using DrawioToMarkdown.Parsing;
using DrawioToMarkdown.Tests.Generators;
using FsCheck;
using FsCheck.Xunit;

namespace DrawioToMarkdown.Tests.Parsing;

/// <summary>
/// Property 1: Parse/Print Round-Trip
/// Validates: Requirements 1.1, 1.2, 1.3, 2.1, 2.2, 5.1, 5.2
///
/// For any valid DependencyGraph (containing nodes with unique IDs and non-empty labels,
/// and edges referencing only existing node IDs), pretty-printing the graph to draw.io XML
/// and then parsing that XML back into a DependencyGraph SHALL produce a graph equivalent
/// to the original (same nodes with same IDs and labels, same dependency relationships).
/// </summary>
public class RoundTripPropertyTests
{
    private readonly XmlPrettyPrinter _printer = new();
    private readonly DrawioParser _parser = new();
    private readonly GraphBuilder _graphBuilder = new();

    /// <summary>
    /// Provides the custom Arbitrary for DependencyGraph to FsCheck.
    /// </summary>
    public static class Arbitraries
    {
        public static Arbitrary<DependencyGraph> DependencyGraphArbitrary() =>
            ArbitraryGraphs.ArbDependencyGraph();
    }

    /// <summary>
    /// **Validates: Requirements 1.1, 1.2, 1.3, 2.1, 2.2, 5.1, 5.2**
    ///
    /// Pretty-printing a DependencyGraph to draw.io XML and parsing it back produces
    /// a graph with the same set of node IDs, same labels, and same dependency relationships.
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(Arbitraries) })]
    public bool ParsePrintRoundTripPreservesGraph(DependencyGraph original)
    {
        // Step 1: Pretty-print the graph to XML
        var xml = _printer.Print(original);

        // Step 2: Parse the XML back into a ParsedDiagram
        var parsed = _parser.Parse(xml);

        // Step 3: Build a new DependencyGraph from the parsed result
        var roundTripped = _graphBuilder.Build(parsed);

        // Step 4: Compare - same set of node IDs
        if (original.Nodes.Count != roundTripped.Nodes.Count)
            return false;

        if (!original.Nodes.Keys.ToHashSet().SetEquals(roundTripped.Nodes.Keys.ToHashSet()))
            return false;

        // Step 5: Compare - same labels for each node
        foreach (var (id, node) in original.Nodes)
        {
            if (!roundTripped.Nodes.TryGetValue(id, out var rtNode))
                return false;
            if (node.Label != rtNode.Label)
                return false;
        }

        // Step 6: Compare - same dependency relationships
        if (original.Dependencies.Count != roundTripped.Dependencies.Count)
            return false;

        foreach (var (nodeId, antecedents) in original.Dependencies)
        {
            if (!roundTripped.Dependencies.TryGetValue(nodeId, out var rtAntecedents))
                return false;
            if (!antecedents.ToHashSet().SetEquals(rtAntecedents.ToHashSet()))
                return false;
        }

        return true;
    }
}
