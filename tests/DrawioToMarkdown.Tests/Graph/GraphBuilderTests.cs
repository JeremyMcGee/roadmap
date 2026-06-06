using DrawioToMarkdown.Graph;
using DrawioToMarkdown.Parsing;
using Xunit;

namespace DrawioToMarkdown.Tests.Graph;

public class GraphBuilderTests
{
    private readonly GraphBuilder _builder = new();

    [Fact]
    public void Build_ValidNodesAndEdges_ProducesCorrectGraph()
    {
        // Arrange — nodes A, B, C with edges A→B, B→C
        var diagram = new ParsedDiagram(
            Swimlanes: Array.Empty<SwimlaneDef>(),
            QuarterColumns: Array.Empty<QuarterColumnDef>(),
            ActivityNodes: new[]
            {
                new ActivityNodeDef("A", "Task A", 0, 0, 100, 40),
                new ActivityNodeDef("B", "Task B", 0, 50, 100, 40),
                new ActivityNodeDef("C", "Task C", 0, 100, 100, 40)
            },
            Edges: new[]
            {
                new DependencyEdgeDef("A", "B"),
                new DependencyEdgeDef("B", "C")
            }
        );

        // Act
        var graph = _builder.Build(diagram);

        // Assert — all nodes present
        Assert.Equal(3, graph.Nodes.Count);
        Assert.Contains("A", graph.Nodes.Keys);
        Assert.Contains("B", graph.Nodes.Keys);
        Assert.Contains("C", graph.Nodes.Keys);

        // Edge A→B means Dependencies[B] contains A
        Assert.Contains("A", graph.Dependencies["B"]);
        // Edge B→C means Dependencies[C] contains B
        Assert.Contains("B", graph.Dependencies["C"]);
        // A has no dependencies
        Assert.Empty(graph.Dependencies["A"]);
    }

    [Fact]
    public void Build_DanglingEdges_AreFilteredOut()
    {
        // Arrange — edges referencing non-existent node IDs
        var diagram = new ParsedDiagram(
            Swimlanes: Array.Empty<SwimlaneDef>(),
            QuarterColumns: Array.Empty<QuarterColumnDef>(),
            ActivityNodes: new[]
            {
                new ActivityNodeDef("1", "Node One", 0, 0, 100, 40),
                new ActivityNodeDef("2", "Node Two", 0, 50, 100, 40)
            },
            Edges: new[]
            {
                new DependencyEdgeDef("1", "2"),        // valid
                new DependencyEdgeDef("1", "999"),      // target doesn't exist
                new DependencyEdgeDef("888", "2"),      // source doesn't exist
                new DependencyEdgeDef("777", "999")     // both don't exist
            }
        );

        // Act
        var graph = _builder.Build(diagram);

        // Assert — only the valid edge is in the graph
        Assert.Equal(2, graph.Nodes.Count);
        Assert.Contains("1", graph.Dependencies["2"]);
        Assert.Empty(graph.Dependencies["1"]);

        // Only one dependency relationship exists (the valid edge)
        var totalDependencies = graph.Dependencies.Values.Sum(s => s.Count);
        Assert.Equal(1, totalDependencies);
    }

    [Fact]
    public void Build_DuplicateEdges_AreDeduplicated()
    {
        // Arrange — multiple identical edges (same source/target)
        var diagram = new ParsedDiagram(
            Swimlanes: Array.Empty<SwimlaneDef>(),
            QuarterColumns: Array.Empty<QuarterColumnDef>(),
            ActivityNodes: new[]
            {
                new ActivityNodeDef("X", "Node X", 0, 0, 100, 40),
                new ActivityNodeDef("Y", "Node Y", 0, 50, 100, 40)
            },
            Edges: new[]
            {
                new DependencyEdgeDef("X", "Y"),
                new DependencyEdgeDef("X", "Y"),
                new DependencyEdgeDef("X", "Y")
            }
        );

        // Act
        var graph = _builder.Build(diagram);

        // Assert — only one dependency relationship exists
        Assert.Single(graph.Dependencies["Y"]);
        Assert.Contains("X", graph.Dependencies["Y"]);
    }

    [Fact]
    public void Build_NoEdges_ProducesNodesWithEmptyDependencies()
    {
        // Arrange — nodes with no edges
        var diagram = new ParsedDiagram(
            Swimlanes: Array.Empty<SwimlaneDef>(),
            QuarterColumns: Array.Empty<QuarterColumnDef>(),
            ActivityNodes: new[]
            {
                new ActivityNodeDef("A", "Alpha", 0, 0, 100, 40),
                new ActivityNodeDef("B", "Beta", 0, 50, 100, 40),
                new ActivityNodeDef("C", "Gamma", 0, 100, 100, 40)
            },
            Edges: Array.Empty<DependencyEdgeDef>()
        );

        // Act
        var graph = _builder.Build(diagram);

        // Assert — all nodes exist with empty dependency sets
        Assert.Equal(3, graph.Nodes.Count);
        Assert.Empty(graph.Dependencies["A"]);
        Assert.Empty(graph.Dependencies["B"]);
        Assert.Empty(graph.Dependencies["C"]);
    }
}
