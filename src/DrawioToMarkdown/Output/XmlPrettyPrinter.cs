using System.Xml.Linq;
using DrawioToMarkdown.Model;

namespace DrawioToMarkdown.Output;

/// <summary>
/// Converts a RoadmapModel to draw.io-compatible XML with swimlanes and quarter columns.
/// The generated XML can be parsed back by DrawioParser + PositionResolver to yield
/// an equivalent RoadmapModel (round-trip support for Property 3).
///
/// Layout strategy:
/// - One swimlane per unique category (sorted alphabetically), stacked vertically (200px each)
/// - One quarter column per unique quarter (sorted by Q{n} {year}), placed side-by-side (300px each)
/// - Activity nodes centered at the intersection of their swimlane row and quarter column
/// - Multiple activities in the same cell are stacked vertically with small offsets
/// </summary>
public sealed class XmlPrettyPrinter : IXmlPrettyPrinter
{
    // Layout constants
    private const double SwimlaneStartY = 60.0; // Leave room for quarter labels at the top
    private const double SwimlaneStartX = 0.0;
    private const double QuarterColumnWidth = 300.0;
    private const double QuarterLabelX = 30.0; // Offset from left edge (swimlane header width)
    private const double QuarterLabelY = 0.0;
    private const double QuarterLabelHeight = 40.0;
    private const double ActivityWidth = 120.0;
    private const double ActivityHeight = 40.0;
    private const double ActivityStackGap = 50.0;
    private const double SwimlaneMinHeight = 200.0;
    private const double SwimlanePadding = 20.0; // Vertical padding at top and bottom of swimlane

    public string Print(RoadmapModel model)
    {
        var root = new XElement("root");

        // Standard draw.io infrastructure cells
        root.Add(new XElement("mxCell", new XAttribute("id", "0")));
        root.Add(new XElement("mxCell", new XAttribute("id", "1"), new XAttribute("parent", "0")));

        // Determine unique categories and quarters
        var categories = model.Activities
            .Select(a => a.Category)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();

        var quarters = model.Activities
            .Select(a => a.Quarter)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(q => q, QuarterComparer.Instance)
            .ToList();

        // Calculate total diagram width based on quarter columns
        var totalWidth = QuarterLabelX + (quarters.Count * QuarterColumnWidth);

        // Group activities by (category, quarter) for stacking - needed early to compute swimlane heights
        var activityGroups = model.Activities
            .GroupBy(a => (a.Category, a.Quarter))
            .ToDictionary(g => g.Key, g => g.ToList());

        // Compute dynamic swimlane height per category based on the max stack in any quarter
        var swimlaneHeights = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var category in categories)
        {
            var maxStackInCategory = quarters
                .Select(q => activityGroups.TryGetValue((category, q), out var grp) ? grp.Count : 0)
                .DefaultIfEmpty(0)
                .Max();

            // Height needed: padding + stacked activities + padding
            var neededHeight = (maxStackInCategory * ActivityHeight)
                + (Math.Max(0, maxStackInCategory - 1) * ActivityStackGap)
                + (2 * SwimlanePadding);

            swimlaneHeights[category] = Math.Max(SwimlaneMinHeight, neededHeight);
        }

        // Emit swimlane mxCells
        var swimlaneYPositions = new Dictionary<string, double>(StringComparer.Ordinal);
        var currentY = SwimlaneStartY;
        for (var i = 0; i < categories.Count; i++)
        {
            var category = categories[i];
            var height = swimlaneHeights[category];
            swimlaneYPositions[category] = currentY;

            var swimlaneId = $"swimlane_{i}";
            var swimlaneCell = new XElement("mxCell",
                new XAttribute("id", swimlaneId),
                new XAttribute("value", category),
                new XAttribute("style", "shape=swimlane;horizontal=0;startSize=30;"),
                new XAttribute("vertex", "1"),
                new XAttribute("parent", "1"));

            swimlaneCell.Add(new XElement("mxGeometry",
                new XAttribute("x", FormatDouble(SwimlaneStartX)),
                new XAttribute("y", FormatDouble(currentY)),
                new XAttribute("width", FormatDouble(totalWidth)),
                new XAttribute("height", FormatDouble(height)),
                new XAttribute("as", "geometry")));

            root.Add(swimlaneCell);
            currentY += height;
        }

        // Emit quarter label mxCells
        var quarterXPositions = new Dictionary<string, double>(StringComparer.Ordinal);
        for (var i = 0; i < quarters.Count; i++)
        {
            var quarter = quarters[i];
            var x = QuarterLabelX + (i * QuarterColumnWidth);
            quarterXPositions[quarter] = x;

            var qlabelId = $"qlabel_{i}";
            var qlabelCell = new XElement("mxCell",
                new XAttribute("id", qlabelId),
                new XAttribute("value", quarter),
                new XAttribute("style", "text;html=1;align=center;verticalAlign=middle;"),
                new XAttribute("vertex", "1"),
                new XAttribute("parent", "1"));

            qlabelCell.Add(new XElement("mxGeometry",
                new XAttribute("x", FormatDouble(x)),
                new XAttribute("y", FormatDouble(QuarterLabelY)),
                new XAttribute("width", FormatDouble(QuarterColumnWidth)),
                new XAttribute("height", FormatDouble(QuarterLabelHeight)),
                new XAttribute("as", "geometry")));

            root.Add(qlabelCell);
        }

        // Emit activity node mxCells — positioned at the intersection of swimlane and quarter column
        var activityIdMap = new Dictionary<string, string>(StringComparer.Ordinal);
        var activityIndex = 0;

        foreach (var group in activityGroups)
        {
            var (category, quarter) = group.Key;
            var activities = group.Value;

            var swimlaneY = swimlaneYPositions[category];
            var swimlaneHeight = swimlaneHeights[category];
            var quarterX = quarterXPositions[quarter];

            // Center of the cell (swimlane row × quarter column intersection)
            var cellCenterX = quarterX + (QuarterColumnWidth / 2.0);
            var cellCenterY = swimlaneY + (swimlaneHeight / 2.0);

            // Stack activities vertically within the cell
            var totalStackHeight = (activities.Count * ActivityHeight)
                + ((activities.Count - 1) * ActivityStackGap);
            var startY = cellCenterY - (totalStackHeight / 2.0);

            for (var i = 0; i < activities.Count; i++)
            {
                var activity = activities[i];
                var nodeX = cellCenterX - (ActivityWidth / 2.0);
                var nodeY = startY + (i * (ActivityHeight + ActivityStackGap));

                var nodeId = $"act_{activityIndex}";
                activityIdMap[activity.Label] = nodeId;

                var activityCell = new XElement("mxCell",
                    new XAttribute("id", nodeId),
                    new XAttribute("value", activity.Label),
                    new XAttribute("style", "rounded=1;whiteSpace=wrap;html=1"),
                    new XAttribute("vertex", "1"),
                    new XAttribute("parent", "1"));

                activityCell.Add(new XElement("mxGeometry",
                    new XAttribute("x", FormatDouble(nodeX)),
                    new XAttribute("y", FormatDouble(nodeY)),
                    new XAttribute("width", FormatDouble(ActivityWidth)),
                    new XAttribute("height", FormatDouble(ActivityHeight)),
                    new XAttribute("as", "geometry")));

                root.Add(activityCell);
                activityIndex++;
            }
        }

        // Emit dependency edge mxCells
        var edgeIndex = 0;
        foreach (var activity in model.Activities)
        {
            if (!activityIdMap.TryGetValue(activity.Label, out var targetId))
                continue;

            foreach (var depLabel in activity.DependencyLabels)
            {
                if (!activityIdMap.TryGetValue(depLabel, out var sourceId))
                    continue;

                var edgeId = $"edge_{edgeIndex}";
                var edgeCell = new XElement("mxCell",
                    new XAttribute("id", edgeId),
                    new XAttribute("style", "edgeStyle=orthogonalEdgeStyle;"),
                    new XAttribute("edge", "1"),
                    new XAttribute("source", sourceId),
                    new XAttribute("target", targetId),
                    new XAttribute("parent", "1"));

                root.Add(edgeCell);
                edgeIndex++;
            }
        }

        var doc = new XDocument(
            new XElement("mxfile",
                new XElement("diagram",
                    new XAttribute("name", "Page-1"),
                    new XElement("mxGraphModel", root))));

        return doc.ToString();
    }

    /// <summary>
    /// Formats a double value for XML output, removing unnecessary trailing zeros.
    /// </summary>
    private static string FormatDouble(double value)
    {
        // If the value is an integer, emit it without a decimal point
        if (value == Math.Floor(value))
            return ((long)value).ToString();
        return value.ToString("G");
    }

    /// <summary>
    /// Comparer that sorts quarter strings in chronological order.
    /// Expects format "Q{n} {year}" (e.g., "Q1 2025"). Falls back to ordinal comparison
    /// for non-matching strings.
    /// </summary>
    private sealed class QuarterComparer : IComparer<string>
    {
        public static readonly QuarterComparer Instance = new();

        public int Compare(string? x, string? y)
        {
            if (x is null && y is null) return 0;
            if (x is null) return -1;
            if (y is null) return 1;

            var parsedX = TryParseQuarter(x);
            var parsedY = TryParseQuarter(y);

            if (parsedX.HasValue && parsedY.HasValue)
            {
                var yearCmp = parsedX.Value.year.CompareTo(parsedY.Value.year);
                if (yearCmp != 0) return yearCmp;
                return parsedX.Value.quarter.CompareTo(parsedY.Value.quarter);
            }

            // If either doesn't parse, fall back to ordinal comparison
            return string.Compare(x, y, StringComparison.Ordinal);
        }

        private static (int year, int quarter)? TryParseQuarter(string s)
        {
            // Expected format: "Q{n} {year}"
            if (s.Length < 4 || s[0] != 'Q')
                return null;

            var spaceIndex = s.IndexOf(' ', 1);
            if (spaceIndex < 0)
                return null;

            if (int.TryParse(s.AsSpan(1, spaceIndex - 1), out var q) &&
                int.TryParse(s.AsSpan(spaceIndex + 1), out var year))
            {
                return (year, q);
            }

            return null;
        }
    }
}
