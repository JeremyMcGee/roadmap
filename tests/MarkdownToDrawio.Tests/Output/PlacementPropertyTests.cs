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
/// within their category swimlane at the correct quarter column x-position,
/// and that activities sharing the same cell have distinct y-coordinates.
/// **Validates: Requirements 3.7**
/// </summary>
public class PlacementPropertyTests
{
    public static class Arbitraries
    {
        public static Arbitrary<RoadmapModel> RoadmapModelArbitrary() =>
            ArbitraryRoadmaps.GenValidRoadmapModel().ToArbitrary();
    }

    [Property(MaxTest = 100, Arbitrary = new[] { typeof(Arbitraries) })]
    public bool ActivityNodes_ParentMatchesCategorySwimlane(RoadmapModel model)
    {
        var generator = new DiagramGenerator();
        var xml = generator.Generate(model);
        var doc = XDocument.Parse(xml);

        // Get all activity mxCell elements (id starting with "act_")
        var activityNodes = doc.Descendants("mxCell")
            .Where(e => e.Attribute("id")?.Value?.StartsWith("act_") == true)
            .ToList();

        // Build a map from activity index to its category
        var sortedCategories = DiagramGenerator.GetSortedCategories(model);

        foreach (var actNode in activityNodes)
        {
            var actId = actNode.Attribute("id")!.Value;
            var actIndex = int.Parse(actId.Replace("act_", ""));
            var activity = model.Activities[actIndex];

            if (string.IsNullOrWhiteSpace(activity.Category))
                continue;

            var expectedParent = $"cat_{SanitizeId(activity.Category!)}";
            var actualParent = actNode.Attribute("parent")?.Value;

            if (actualParent != expectedParent)
                return false;
        }

        return true;
    }

    [Property(MaxTest = 100, Arbitrary = new[] { typeof(Arbitraries) })]
    public bool ActivityNodes_XPositionMatchesQuarterColumn(RoadmapModel model)
    {
        var generator = new DiagramGenerator();
        var xml = generator.Generate(model);
        var doc = XDocument.Parse(xml);

        var activityNodes = doc.Descendants("mxCell")
            .Where(e => e.Attribute("id")?.Value?.StartsWith("act_") == true)
            .ToList();

        var sortedQuarters = DiagramGenerator.GetSortedQuarters(model);

        // Layout constants from DiagramGenerator
        const int SwimlaneStartSize = 30;
        const int QuarterColumnWidth = 200;
        const int ActivityPaddingX = 40;

        foreach (var actNode in activityNodes)
        {
            var actId = actNode.Attribute("id")!.Value;
            var actIndex = int.Parse(actId.Replace("act_", ""));
            var activity = model.Activities[actIndex];

            if (string.IsNullOrWhiteSpace(activity.Quarter))
                continue;

            var qIndex = DiagramGenerator.GetQuarterIndex(sortedQuarters, activity.Quarter!);
            if (qIndex < 0)
                continue;

            var expectedX = SwimlaneStartSize + (qIndex * QuarterColumnWidth) + ActivityPaddingX;
            var geometry = actNode.Element("mxGeometry");
            var actualX = int.Parse(geometry!.Attribute("x")!.Value);

            if (actualX != expectedX)
                return false;
        }

        return true;
    }

    [Property(MaxTest = 100, Arbitrary = new[] { typeof(Arbitraries) })]
    public bool ActivitiesInSameCell_HaveDistinctYCoordinates(RoadmapModel model)
    {
        var generator = new DiagramGenerator();
        var xml = generator.Generate(model);
        var doc = XDocument.Parse(xml);

        var activityNodes = doc.Descendants("mxCell")
            .Where(e => e.Attribute("id")?.Value?.StartsWith("act_") == true)
            .ToList();

        // Group activities by (parent swimlane, x-position) — those in the same cell
        var cellGroups = activityNodes
            .Select(e => new
            {
                Parent = e.Attribute("parent")?.Value ?? "",
                X = int.Parse(e.Element("mxGeometry")!.Attribute("x")!.Value),
                Y = int.Parse(e.Element("mxGeometry")!.Attribute("y")!.Value)
            })
            .GroupBy(a => (a.Parent, a.X));

        // Assert all activities in the same cell have distinct y-coordinates
        foreach (var group in cellGroups)
        {
            var yValues = group.Select(a => a.Y).ToList();
            if (yValues.Count != yValues.Distinct().Count())
                return false;
        }

        return true;
    }

    /// <summary>
    /// Replicates the SanitizeId logic from DiagramGenerator for test verification.
    /// </summary>
    private static string SanitizeId(string value)
    {
        return System.Text.RegularExpressions.Regex.Replace(value, @"[^a-zA-Z0-9]", "_");
    }
}
