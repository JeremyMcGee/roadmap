using FsCheck;
using FsCheck.Fluent;
using Xunit;
using Gen = FsCheck.Fluent.Gen;

namespace DrawioToMarkdown.Tests.Generators;

/// <summary>
/// Smoke tests to verify the FsCheck generators produce valid data.
/// </summary>
public class ArbitraryGraphsTests
{
    [Fact]
    public void GenDependencyGraph_ProducesValidGraph()
    {
        var samples = Gen.Sample(ArbitraryGraphs.GenDependencyGraph(), 5);

        foreach (var graph in samples)
        {
            // All nodes have non-empty IDs and labels
            Assert.NotEmpty(graph.Nodes);
            foreach (var (id, node) in graph.Nodes)
            {
                Assert.Equal(id, node.Id);
                Assert.False(string.IsNullOrEmpty(node.Label));
            }

            // All dependency references point to existing nodes
            foreach (var (nodeId, deps) in graph.Dependencies)
            {
                Assert.Contains(nodeId, graph.Nodes.Keys);
                foreach (var depId in deps)
                {
                    Assert.Contains(depId, graph.Nodes.Keys);
                }
            }
        }
    }

    [Fact]
    public void GenParsedDiagram_ProducesNodesAndEdges()
    {
        var samples = Gen.Sample(ArbitraryGraphs.GenParsedDiagram(), 5);

        foreach (var diagram in samples)
        {
            // Has at least one node
            Assert.NotEmpty(diagram.Nodes);

            // All nodes have non-empty IDs and labels
            foreach (var node in diagram.Nodes)
            {
                Assert.False(string.IsNullOrEmpty(node.Id));
                Assert.False(string.IsNullOrEmpty(node.Label));
            }

            // Has at least some edges (dangling ones are guaranteed)
            Assert.NotEmpty(diagram.Edges);

            // Verify some edges are dangling (reference non-existent node IDs)
            var nodeIds = diagram.Nodes.Select(n => n.Id).ToHashSet();
            var hasDangling = diagram.Edges.Any(e =>
                !nodeIds.Contains(e.SourceId) || !nodeIds.Contains(e.TargetId));
            Assert.True(hasDangling, "ParsedDiagram should contain at least one dangling edge");
        }
    }

    [Fact]
    public void GenFilePath_ProducesValidPaths()
    {
        var samples = Gen.Sample(ArbitraryGraphs.GenFilePath(), 10);

        foreach (var path in samples)
        {
            Assert.False(string.IsNullOrEmpty(path));
            // Must have an extension
            var lastDot = path.LastIndexOf('.');
            Assert.True(lastDot > 0, $"Path should have an extension: {path}");
            var ext = path[lastDot..];
            var validExtensions = new[] { ".drawio", ".xml", ".txt", ".md", ".json", ".csv", ".html", ".yaml" };
            Assert.Contains(ext, validExtensions);
        }
    }
}
