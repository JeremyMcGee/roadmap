using System.Xml.Linq;
using MarkdownToDrawio.Model;
using MarkdownToDrawio.Output;
using Xunit;

namespace MarkdownToDrawio.Tests.Output;

public class DiagramGeneratorTests
{
    private readonly DiagramGenerator _generator = new();

    /// <summary>
    /// A single activity produces valid XML with one swimlane and one activity node.
    /// Validates: Requirements 3.1, 3.2
    /// </summary>
    [Fact]
    public void SingleActivity_ProducesValidXmlWithOneSwimlaneAndOneNode()
    {
        var model = new RoadmapModel(new[]
        {
            new Activity("Design API", "Q1 2025", "Infrastructure", Array.Empty<string>())
        });

        var xml = _generator.Generate(model);

        var doc = XDocument.Parse(xml);
        var root = doc.Descendants("root").Single();

        // Should have exactly one swimlane
        var swimlanes = root.Elements("mxCell")
            .Where(e => (e.Attribute("style")?.Value ?? "").Contains("shape=swimlane"))
            .ToList();
        Assert.Single(swimlanes);
        Assert.Equal("Infrastructure", swimlanes[0].Attribute("value")!.Value);

        // Should have exactly one activity node (vertex with rounded style)
        var activityNodes = root.Elements("mxCell")
            .Where(e => (e.Attribute("style")?.Value ?? "").Contains("rounded=1"))
            .ToList();
        Assert.Single(activityNodes);
        Assert.Equal("Design API", activityNodes[0].Attribute("value")!.Value);
    }

    /// <summary>
    /// Two activities in different categories produce two swimlanes.
    /// Validates: Requirements 3.2
    /// </summary>
    [Fact]
    public void TwoActivitiesInDifferentCategories_ProduceTwoSwimlanes()
    {
        var model = new RoadmapModel(new[]
        {
            new Activity("Design API", "Q1 2025", "Infrastructure", Array.Empty<string>()),
            new Activity("Write Tests", "Q1 2025", "Quality", Array.Empty<string>())
        });

        var xml = _generator.Generate(model);

        var doc = XDocument.Parse(xml);
        var root = doc.Descendants("root").Single();

        var swimlanes = root.Elements("mxCell")
            .Where(e => (e.Attribute("style")?.Value ?? "").Contains("shape=swimlane"))
            .ToList();

        Assert.Equal(2, swimlanes.Count);

        var swimlaneValues = swimlanes.Select(s => s.Attribute("value")!.Value).OrderBy(v => v).ToList();
        Assert.Equal("Infrastructure", swimlaneValues[0]);
        Assert.Equal("Quality", swimlaneValues[1]);
    }

    /// <summary>
    /// A dependency between two activities produces an edge element.
    /// Validates: Requirements 3.8
    /// </summary>
    [Fact]
    public void DependencyBetweenActivities_ProducesEdgeElement()
    {
        var model = new RoadmapModel(new[]
        {
            new Activity("Design API", "Q1 2025", "Infrastructure", Array.Empty<string>()),
            new Activity("Implement API", "Q2 2025", "Infrastructure", new[] { "Design API" })
        });

        var xml = _generator.Generate(model);

        var doc = XDocument.Parse(xml);
        var root = doc.Descendants("root").Single();

        var edges = root.Elements("mxCell")
            .Where(e => e.Attribute("edge")?.Value == "1")
            .ToList();

        Assert.Single(edges);

        var edge = edges[0];
        // The source should be the antecedent (Design API) and target the dependent (Implement API)
        var sourceId = edge.Attribute("source")!.Value;
        var targetId = edge.Attribute("target")!.Value;

        // Verify source points to Design API node and target points to Implement API node
        var sourceNode = root.Descendants("mxCell")
            .First(e => e.Attribute("id")?.Value == sourceId);
        var targetNode = root.Descendants("mxCell")
            .First(e => e.Attribute("id")?.Value == targetId);

        Assert.Equal("Design API", sourceNode.Attribute("value")!.Value);
        Assert.Equal("Implement API", targetNode.Attribute("value")!.Value);
    }

    /// <summary>
    /// Multiple activities in the same category and quarter are stacked vertically (distinct y-coordinates).
    /// Validates: Requirements 3.7
    /// </summary>
    [Fact]
    public void MultipleActivitiesInSameCategoryAndQuarter_AreStackedVertically()
    {
        var model = new RoadmapModel(new[]
        {
            new Activity("Task A", "Q1 2025", "Infrastructure", Array.Empty<string>()),
            new Activity("Task B", "Q1 2025", "Infrastructure", Array.Empty<string>()),
            new Activity("Task C", "Q1 2025", "Infrastructure", Array.Empty<string>())
        });

        var xml = _generator.Generate(model);

        var doc = XDocument.Parse(xml);
        var root = doc.Descendants("root").Single();

        var activityNodes = root.Descendants("mxCell")
            .Where(e => (e.Attribute("style")?.Value ?? "").Contains("rounded=1"))
            .ToList();

        Assert.Equal(3, activityNodes.Count);

        // Extract y-coordinates from geometry
        var yValues = activityNodes
            .Select(n => int.Parse(n.Element("mxGeometry")!.Attribute("y")!.Value))
            .OrderBy(y => y)
            .ToList();

        // All y-values should be distinct (no overlapping)
        Assert.Equal(yValues.Distinct().Count(), yValues.Count);

        // Verify they are in ascending order (stacked top to bottom)
        for (int i = 1; i < yValues.Count; i++)
        {
            Assert.True(yValues[i] > yValues[i - 1],
                $"Activity at index {i} should have larger y than index {i - 1}");
        }
    }

    /// <summary>
    /// An unparseable quarter value is placed after valid quarters.
    /// Validates: Requirements 3.10
    /// </summary>
    [Fact]
    public void UnparseableQuarterValue_IsPlacedAfterValidQuarters()
    {
        var model = new RoadmapModel(new[]
        {
            new Activity("Task A", "Q1 2025", "Infrastructure", Array.Empty<string>()),
            new Activity("Task B", "Future", "Infrastructure", Array.Empty<string>())
        });

        var xml = _generator.Generate(model);

        var doc = XDocument.Parse(xml);
        var root = doc.Descendants("root").Single();

        var activityNodes = root.Descendants("mxCell")
            .Where(e => (e.Attribute("style")?.Value ?? "").Contains("rounded=1"))
            .ToList();

        Assert.Equal(2, activityNodes.Count);

        // Task A (Q1 2025 - parseable) should have a smaller x than Task B (Future - unparseable)
        var taskA = activityNodes.First(n => n.Attribute("value")!.Value == "Task A");
        var taskB = activityNodes.First(n => n.Attribute("value")!.Value == "Task B");

        var xA = int.Parse(taskA.Element("mxGeometry")!.Attribute("x")!.Value);
        var xB = int.Parse(taskB.Element("mxGeometry")!.Attribute("x")!.Value);

        Assert.True(xA < xB,
            $"Parseable quarter (x={xA}) should be positioned before unparseable quarter (x={xB})");
    }
}
