using DrawioToMarkdown.Model;
using DrawioToMarkdown.Parsing;

namespace DrawioToMarkdown.Resolution;

/// <summary>
/// Assigns each activity a category (from swimlane containment) and quarter
/// (from quarter column containment) based on geometric position, and resolves
/// dependency edges into antecedent label lists.
/// </summary>
public sealed class PositionResolver : IPositionResolver
{
    /// <inheritdoc />
    public RoadmapModel Resolve(ParsedDiagram diagram)
    {
        if (diagram.Swimlanes.Count == 0)
        {
            throw new InvalidOperationException("No category swimlanes detected.");
        }

        if (diagram.QuarterColumns.Count == 0)
        {
            throw new InvalidOperationException("No quarter columns detected.");
        }

        // Build an Id → Label lookup for activities (used for dependency resolution)
        var idToLabel = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var node in diagram.ActivityNodes)
        {
            idToLabel[node.Id] = node.Label;
        }

        // Build incoming dependency map: targetId → list of source labels
        var incomingDependencies = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var edge in diagram.Edges)
        {
            if (!idToLabel.TryGetValue(edge.SourceId, out var sourceLabel))
                continue;
            if (!idToLabel.ContainsKey(edge.TargetId))
                continue;

            if (!incomingDependencies.TryGetValue(edge.TargetId, out var deps))
            {
                deps = new List<string>();
                incomingDependencies[edge.TargetId] = deps;
            }

            if (!deps.Contains(sourceLabel, StringComparer.Ordinal))
            {
                deps.Add(sourceLabel);
            }
        }

        var activities = new List<RoadmapActivity>();

        foreach (var node in diagram.ActivityNodes)
        {
            var category = ResolveCategory(node, diagram.Swimlanes);
            var quarter = ResolveQuarter(node, diagram.QuarterColumns);

            var dependencyLabels = incomingDependencies.TryGetValue(node.Id, out var labels)
                ? (IReadOnlyList<string>)labels
                : Array.Empty<string>();

            activities.Add(new RoadmapActivity(node.Label, quarter, category, dependencyLabels));
        }

        return new RoadmapModel(activities);
    }

    /// <summary>
    /// Assigns a category label to an activity based on vertical center containment within swimlanes.
    /// </summary>
    private static string ResolveCategory(ActivityNodeDef node, IReadOnlyList<SwimlaneDef> swimlanes)
    {
        var verticalCenter = node.Y + node.Height / 2.0;

        // Find all swimlanes whose y-range contains the vertical center
        var containing = new List<SwimlaneDef>();
        for (var i = 0; i < swimlanes.Count; i++)
        {
            var lane = swimlanes[i];
            if (verticalCenter >= lane.Y && verticalCenter <= lane.Y + lane.Height)
            {
                containing.Add(lane);
            }
        }

        if (containing.Count == 1)
        {
            return containing[0].Label;
        }

        // Zero or multiple matches: find closest by vertical midpoint distance,
        // breaking ties by document order (first in the original list)
        return FindClosestByVerticalMidpoint(verticalCenter, swimlanes);
    }

    /// <summary>
    /// Assigns a quarter label to an activity based on horizontal center containment within quarter columns.
    /// </summary>
    private static string ResolveQuarter(ActivityNodeDef node, IReadOnlyList<QuarterColumnDef> columns)
    {
        var horizontalCenter = node.X + node.Width / 2.0;

        // Find all quarter columns whose x-range contains the horizontal center
        var containing = new List<QuarterColumnDef>();
        for (var i = 0; i < columns.Count; i++)
        {
            var col = columns[i];
            if (horizontalCenter >= col.X && horizontalCenter <= col.X + col.Width)
            {
                containing.Add(col);
            }
        }

        if (containing.Count == 1)
        {
            return containing[0].Label;
        }

        // Zero or multiple matches: find closest by horizontal midpoint distance,
        // breaking ties by document order (first in the original list)
        return FindClosestByHorizontalMidpoint(horizontalCenter, columns);
    }

    /// <summary>
    /// Finds the swimlane whose vertical midpoint is closest to the given vertical center.
    /// Ties are broken by document order (first in list wins).
    /// </summary>
    private static string FindClosestByVerticalMidpoint(double verticalCenter, IReadOnlyList<SwimlaneDef> swimlanes)
    {
        var bestLabel = swimlanes[0].Label;
        var bestDistance = Math.Abs((swimlanes[0].Y + swimlanes[0].Height / 2.0) - verticalCenter);

        for (var i = 1; i < swimlanes.Count; i++)
        {
            var lane = swimlanes[i];
            var midpoint = lane.Y + lane.Height / 2.0;
            var distance = Math.Abs(midpoint - verticalCenter);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestLabel = lane.Label;
            }
            // On tie (distance == bestDistance), first in list wins — no update needed
        }

        return bestLabel;
    }

    /// <summary>
    /// Finds the quarter column whose horizontal midpoint is closest to the given horizontal center.
    /// Ties are broken by document order (first in list wins).
    /// </summary>
    private static string FindClosestByHorizontalMidpoint(double horizontalCenter, IReadOnlyList<QuarterColumnDef> columns)
    {
        var bestLabel = columns[0].Label;
        var bestDistance = Math.Abs((columns[0].X + columns[0].Width / 2.0) - horizontalCenter);

        for (var i = 1; i < columns.Count; i++)
        {
            var col = columns[i];
            var midpoint = col.X + col.Width / 2.0;
            var distance = Math.Abs(midpoint - horizontalCenter);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestLabel = col.Label;
            }
            // On tie (distance == bestDistance), first in list wins — no update needed
        }

        return bestLabel;
    }
}
