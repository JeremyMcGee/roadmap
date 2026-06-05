// Feature: markdown-to-drawio, Property 5: Swimlane Structure Matches Categories
using System.Xml.Linq;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using MarkdownToDrawio.Model;
using MarkdownToDrawio.Output;
using MarkdownToDrawio.Tests.Generators;

namespace MarkdownToDrawio.Tests.Output;

/// <summary>
/// Property-based tests verifying that swimlane structure matches categories.
/// **Validates: Requirements 3.2, 3.3**
/// </summary>
public class SwimlanePropertyTests
{
    public static class Arbitraries
    {
        public static Arbitrary<RoadmapModel> RoadmapModelArbitrary() =>
            ArbitraryRoadmaps.GenValidRoadmapModel().ToArbitrary();
    }

    [Property(MaxTest = 100, Arbitrary = new[] { typeof(Arbitraries) })]
    public bool SwimlaneCount_EqualsDistinctCategories(RoadmapModel model)
    {
        var generator = new DiagramGenerator();
        var xml = generator.Generate(model);
        var doc = XDocument.Parse(xml);

        var swimlanes = doc.Descendants("mxCell")
            .Where(e => e.Attribute("style")?.Value?.Contains("shape=swimlane") == true)
            .ToList();

        var distinctCategories = model.Activities
            .Select(a => a.Category)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return swimlanes.Count == distinctCategories.Count;
    }

    [Property(MaxTest = 100, Arbitrary = new[] { typeof(Arbitraries) })]
    public bool SwimlaneValues_MatchCategoryLabels(RoadmapModel model)
    {
        var generator = new DiagramGenerator();
        var xml = generator.Generate(model);
        var doc = XDocument.Parse(xml);

        var swimlanes = doc.Descendants("mxCell")
            .Where(e => e.Attribute("style")?.Value?.Contains("shape=swimlane") == true)
            .ToList();

        var distinctCategories = model.Activities
            .Select(a => a.Category)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var swimlaneValues = swimlanes
            .Select(e => e.Attribute("value")?.Value ?? "")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return swimlaneValues.SetEquals(distinctCategories);
    }

    [Property(MaxTest = 100, Arbitrary = new[] { typeof(Arbitraries) })]
    public bool SwimlaneStyles_ContainHorizontalZero(RoadmapModel model)
    {
        var generator = new DiagramGenerator();
        var xml = generator.Generate(model);
        var doc = XDocument.Parse(xml);

        var swimlanes = doc.Descendants("mxCell")
            .Where(e => e.Attribute("style")?.Value?.Contains("shape=swimlane") == true)
            .ToList();

        return swimlanes.All(e =>
            e.Attribute("style")?.Value?.Contains("horizontal=0") == true);
    }

    [Property(MaxTest = 100, Arbitrary = new[] { typeof(Arbitraries) })]
    public bool Swimlanes_OrderedAlphabeticallyByAscendingY(RoadmapModel model)
    {
        var generator = new DiagramGenerator();
        var xml = generator.Generate(model);
        var doc = XDocument.Parse(xml);

        var swimlanes = doc.Descendants("mxCell")
            .Where(e => e.Attribute("style")?.Value?.Contains("shape=swimlane") == true)
            .ToList();

        // Extract (value, y-coordinate) pairs
        var swimlaneData = swimlanes
            .Select(e => new
            {
                Value = e.Attribute("value")?.Value ?? "",
                Y = double.Parse(e.Element("mxGeometry")?.Attribute("y")?.Value ?? "0")
            })
            .OrderBy(s => s.Y)
            .ToList();

        // Swimlanes ordered by ascending y should match alphabetical order of category labels
        var orderedByY = swimlaneData.Select(s => s.Value).ToList();
        var orderedAlphabetically = swimlaneData
            .Select(s => s.Value)
            .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return orderedByY.SequenceEqual(orderedAlphabetically, StringComparer.OrdinalIgnoreCase);
    }
}
