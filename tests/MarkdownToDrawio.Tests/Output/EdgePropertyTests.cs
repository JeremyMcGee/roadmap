// Feature: markdown-to-drawio, Property 8: Dependency Edges Match Model
using System.Xml.Linq;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using MarkdownToDrawio.Model;
using MarkdownToDrawio.Output;
using MarkdownToDrawio.Tests.Generators;

namespace MarkdownToDrawio.Tests.Output;

/// <summary>
/// Property-based tests verifying that dependency edges match the model.
/// **Validates: Requirements 3.8**
/// </summary>
public class EdgePropertyTests
{
    public static class Arbitraries
    {
        public static Arbitrary<RoadmapModel> RoadmapModelArbitrary() =>
            ArbitraryRoadmaps.GenValidRoadmapModel().ToArbitrary();
    }

    [Property(MaxTest = 100, Arbitrary = new[] { typeof(Arbitraries) })]
    public bool EdgeCount_EqualsExpectedDependencyCount(RoadmapModel model)
    {
        var generator = new DiagramGenerator();
        var xml = generator.Generate(model);
        var doc = XDocument.Parse(xml);

        // Find all edge mxCell elements (those with edge="1" attribute)
        var edges = doc.Descendants("mxCell")
            .Where(e => e.Attribute("edge")?.Value == "1")
            .ToList();

        // Build a mapping from activity label to element ID
        // Activity nodes have id starting with "act_" and a "value" attribute
        var activityLabelToId = doc.Descendants("mxCell")
            .Where(e => e.Attribute("id")?.Value?.StartsWith("act_") == true)
            .ToDictionary(
                e => e.Attribute("value")!.Value,
                e => e.Attribute("id")!.Value,
                StringComparer.OrdinalIgnoreCase);

        // Compute expected edge count from the model:
        // For each activity, for each dependency label, expect an edge
        // only if both the activity and its dependency have element IDs in the XML
        var expectedEdgeCount = model.Activities
            .Where(a => activityLabelToId.ContainsKey(a.Label))
            .SelectMany(a => a.DependencyLabels
                .Where(dep => activityLabelToId.ContainsKey(dep)))
            .Count();

        return edges.Count == expectedEdgeCount;
    }

    [Property(MaxTest = 100, Arbitrary = new[] { typeof(Arbitraries) })]
    public bool EachExpectedEdge_ExistsInXml(RoadmapModel model)
    {
        var generator = new DiagramGenerator();
        var xml = generator.Generate(model);
        var doc = XDocument.Parse(xml);

        // Find all edge mxCell elements (those with edge="1" attribute)
        var edges = doc.Descendants("mxCell")
            .Where(e => e.Attribute("edge")?.Value == "1")
            .ToList();

        // Build a mapping from activity label to element ID
        var activityLabelToId = doc.Descendants("mxCell")
            .Where(e => e.Attribute("id")?.Value?.StartsWith("act_") == true)
            .ToDictionary(
                e => e.Attribute("value")!.Value,
                e => e.Attribute("id")!.Value,
                StringComparer.OrdinalIgnoreCase);

        // Compute expected edges from the model:
        // For each activity, for each dependency label, expect an edge from
        // antecedent's element ID (source) to dependent's element ID (target).
        // If activity "B" depends on "A", edge has source=A's ID, target=B's ID.
        var expectedEdges = model.Activities
            .Where(a => activityLabelToId.ContainsKey(a.Label))
            .SelectMany(a => a.DependencyLabels
                .Where(dep => activityLabelToId.ContainsKey(dep))
                .Select(dep => (Source: activityLabelToId[dep], Target: activityLabelToId[a.Label])))
            .ToList();

        // Build a set of actual edges from XML (source, target) pairs
        var actualEdges = edges
            .Select(e => (
                Source: e.Attribute("source")?.Value ?? "",
                Target: e.Attribute("target")?.Value ?? ""))
            .ToHashSet();

        // Assert each expected edge exists in the XML
        return expectedEdges.All(expected => actualEdges.Contains(expected));
    }
}
