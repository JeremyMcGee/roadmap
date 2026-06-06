// Feature: markdown-to-drawio, Property 7: Activity Placement Correctness
using System.Xml.Linq;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using MarkdownToDrawio.Model;
using MarkdownToDrawio.Output;
using MarkdownToDrawio.Tests.Generators;

namespace MarkdownToDrawio.Tests.Output;

/// <summary>
/// Property-based tests verifying that activity nodes are correctly placed
/// within their category swimlane, and that activities at the same horizontal
/// slot within the same cell have distinct y-coordinates (no overlapping).
/// **Validates: Requirements 3.7**
/// </summary>
public class PlacementPropertyTests
{
    public static class Arbitraries
    {
        public static Arbitrary<RoadmapModel> RoadmapModelArbitrary() =>
            ArbitraryRoadmaps.GenValidRoadmapModel().ToArbitrary();
    }

    /// <summary>
    /// Each activity node's y-position must fall within the vertical bounds
    /// of the swimlane corresponding to its category.
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(Arbitraries) })]
    public bool ActivityNodes_YPositionWithinCategorySwimlane(RoadmapModel model)
    {
        var generator = new DiagramGenerator();
        var xml = generator.Generate(model);
        var doc = XDocument.Parse(xml);

        var activityNodes = doc.Descendants("mxCell")
            .Where(e => e.Attribute("id")?.Value?.StartsWith("act_") == true)
            .ToList();

        // Build swimlane bounds: category name → (yStart, yEnd)
        var swimlanes = doc.Descendants("mxCell")
            .Where(e => (e.Attribute("style")?.Value ?? "").Contains("shape=swimlane"))
            .Select(e =>
            {
                var geo = e.Element("mxGeometry")!;
                var y = int.Parse(geo.Attribute("y")!.Value);
                var h = int.Parse(geo.Attribute("height")!.Value);
                var cat = e.Attribute("value")!.Value;
                return (Category: cat, YStart: y, YEnd: y + h);
            })
            .ToDictionary(s => s.Category, s => s, StringComparer.OrdinalIgnoreCase);

        foreach (var actNode in activityNodes)
        {
            var actId = actNode.Attribute("id")!.Value;
            var actIndex = int.Parse(actId.Replace("act_", ""));
            var activity = model.Activities[actIndex];

            if (string.IsNullOrWhiteSpace(activity.Category))
                continue;

            if (!swimlanes.TryGetValue(activity.Category!, out var bounds))
                return false;

            var geo = actNode.Element("mxGeometry")!;
            var actY = int.Parse(geo.Attribute("y")!.Value);
            var actH = int.Parse(geo.Attribute("height")!.Value);

            // Activity must be within the swimlane's vertical bounds
            if (actY < bounds.YStart || actY + actH > bounds.YEnd)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Each activity node's x-position must be within the bounds of its quarter column.
    /// The quarter column bounds are determined by the quarter label elements' x and width.
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(Arbitraries) })]
    public bool ActivityNodes_XPositionWithinQuarterColumn(RoadmapModel model)
    {
        var generator = new DiagramGenerator();
        var xml = generator.Generate(model);
        var doc = XDocument.Parse(xml);

        var activityNodes = doc.Descendants("mxCell")
            .Where(e => e.Attribute("id")?.Value?.StartsWith("act_") == true)
            .ToList();

        // Get quarter column bounds from qlabel elements
        var quarterLabels = doc.Descendants("mxCell")
            .Where(e => e.Attribute("id")?.Value?.StartsWith("qlabel_") == true)
            .OrderBy(e => int.Parse(e.Attribute("id")!.Value.Replace("qlabel_", "")))
            .Select(e =>
            {
                var geo = e.Element("mxGeometry")!;
                var x = int.Parse(geo.Attribute("x")!.Value);
                var w = int.Parse(geo.Attribute("width")!.Value);
                return (X: x, Width: w);
            })
            .ToList();

        var sortedQuarters = DiagramGenerator.GetSortedQuarters(model);

        foreach (var actNode in activityNodes)
        {
            var actId = actNode.Attribute("id")!.Value;
            var actIndex = int.Parse(actId.Replace("act_", ""));
            var activity = model.Activities[actIndex];

            if (string.IsNullOrWhiteSpace(activity.Quarter))
                continue;

            var qIndex = DiagramGenerator.GetQuarterIndex(sortedQuarters, activity.Quarter!);
            if (qIndex < 0 || qIndex >= quarterLabels.Count)
                continue;

            var colBounds = quarterLabels[qIndex];
            var geo = actNode.Element("mxGeometry")!;
            var actX = int.Parse(geo.Attribute("x")!.Value);

            // Activity x should be >= column start x and < column start x + column width
            if (actX < colBounds.X || actX + 120 > colBounds.X + colBounds.Width + 20) // small tolerance
                return false;
        }

        return true;
    }

    /// <summary>
    /// Activities at the same x-position within the same parent (same slot in same cell)
    /// must have distinct y-coordinates (no overlapping).
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(Arbitraries) })]
    public bool ActivitiesInSameSlot_HaveDistinctYCoordinates(RoadmapModel model)
    {
        var generator = new DiagramGenerator();
        var xml = generator.Generate(model);
        var doc = XDocument.Parse(xml);

        var activityNodes = doc.Descendants("mxCell")
            .Where(e => e.Attribute("id")?.Value?.StartsWith("act_") == true)
            .ToList();

        // Group activities by (parent swimlane, x-position) — those in the same vertical stack
        var cellGroups = activityNodes
            .Select(e => new
            {
                Parent = e.Attribute("parent")?.Value ?? "",
                X = int.Parse(e.Element("mxGeometry")!.Attribute("x")!.Value),
                Y = int.Parse(e.Element("mxGeometry")!.Attribute("y")!.Value)
            })
            .GroupBy(a => (a.Parent, a.X));

        foreach (var group in cellGroups)
        {
            var yValues = group.Select(a => a.Y).ToList();
            if (yValues.Count != yValues.Distinct().Count())
                return false;
        }

        return true;
    }
}
