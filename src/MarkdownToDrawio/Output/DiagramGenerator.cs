using System.Text.RegularExpressions;
using System.Xml.Linq;
using MarkdownToDrawio.Model;

namespace MarkdownToDrawio.Output;

/// <summary>
/// Parsed quarter for sorting purposes.
/// </summary>
public sealed record ParsedQuarter(int Year, int QuarterNumber) : IComparable<ParsedQuarter>
{
    public int CompareTo(ParsedQuarter? other)
    {
        if (other is null) return 1;
        var yearCmp = Year.CompareTo(other.Year);
        return yearCmp != 0 ? yearCmp : QuarterNumber.CompareTo(other.QuarterNumber);
    }
}

/// <summary>
/// Generates Draw.IO XML from a validated RoadmapModel.
/// Produces mxGraphModel XML with swimlanes (one per category), quarter columns,
/// activity nodes placed at category/quarter intersections, and dependency edges.
/// </summary>
public class DiagramGenerator : IDiagramGenerator
{
    private static readonly Regex QuarterPattern = new(@"^Q(\d+)\s+(\d{4})$", RegexOptions.Compiled);

    // Layout constants
    private const int SwimlaneStartSize = 30;
    private const int SwimlaneWidth = 200;
    private const int SwimlaneHeight = 150;
    private const int QuarterColumnWidth = 200;
    private const int QuarterHeaderHeight = 40;
    private const int ActivityWidth = 120;
    private const int ActivityHeight = 40;
    private const int ActivityPaddingX = 40;
    private const int ActivityPaddingY = 20;
    private const int ActivityStackGap = 10;

    /// <summary>
    /// Generates Draw.IO-compatible XML from a validated RoadmapModel.
    /// </summary>
    public string Generate(RoadmapModel model)
    {
        var sortedQuarters = GetSortedQuarters(model);
        var sortedCategories = GetSortedCategories(model);

        var root = new XElement("root",
            new XElement("mxCell", new XAttribute("id", "0")),
            new XElement("mxCell", new XAttribute("id", "1"), new XAttribute("parent", "0"))
        );

        // Generate swimlanes (one per category)
        var swimlaneElements = GenerateSwimlanes(sortedCategories, sortedQuarters.Count);
        foreach (var el in swimlaneElements)
        {
            root.Add(el);
        }

        // Generate quarter column separators and labels
        var quarterElements = GenerateQuarterColumns(sortedQuarters, sortedCategories.Count);
        foreach (var el in quarterElements)
        {
            root.Add(el);
        }

        // Generate activity nodes
        var activityIdMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var activityElements = GenerateActivityNodes(model, sortedQuarters, sortedCategories, activityIdMap);
        foreach (var el in activityElements)
        {
            root.Add(el);
        }

        // Generate dependency edges
        var edgeElements = GenerateEdges(model, activityIdMap);
        foreach (var el in edgeElements)
        {
            root.Add(el);
        }

        var mxGraphModel = new XElement("mxGraphModel", root);
        var diagram = new XElement("diagram", new XAttribute("name", "Page-1"), mxGraphModel);
        var mxFile = new XElement("mxfile", diagram);

        var doc = new XDocument(new XDeclaration("1.0", "UTF-8", null), mxFile);
        return doc.ToString(SaveOptions.None);
    }

    /// <summary>
    /// Gets the sorted list of distinct quarter values from the model.
    /// Parseable quarters (Q{n} {year}) are sorted by year ascending, then quarter number ascending.
    /// Unparseable values are placed after valid quarters, sorted alphabetically.
    /// </summary>
    internal static List<string> GetSortedQuarters(RoadmapModel model)
    {
        var distinctQuarters = model.Activities
            .Select(a => a.Quarter)
            .Where(q => !string.IsNullOrWhiteSpace(q))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var parseable = new List<(string Original, ParsedQuarter Parsed)>();
        var unparseable = new List<string>();

        foreach (var q in distinctQuarters)
        {
            var parsed = TryParseQuarter(q!);
            if (parsed is not null)
            {
                parseable.Add((q!, parsed));
            }
            else
            {
                unparseable.Add(q!);
            }
        }

        parseable.Sort((a, b) => a.Parsed.CompareTo(b.Parsed));
        unparseable.Sort(StringComparer.OrdinalIgnoreCase);

        var result = new List<string>(parseable.Count + unparseable.Count);
        result.AddRange(parseable.Select(p => p.Original));
        result.AddRange(unparseable);

        return result;
    }

    /// <summary>
    /// Gets the sorted list of distinct categories from the model, sorted alphabetically.
    /// </summary>
    internal static List<string> GetSortedCategories(RoadmapModel model)
    {
        return model.Activities
            .Select(a => a.Category)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToList()!;
    }

    /// <summary>
    /// Tries to parse a quarter string in the format "Q{n} {year}".
    /// Returns null if the string does not match the expected format.
    /// </summary>
    internal static ParsedQuarter? TryParseQuarter(string quarterValue)
    {
        var match = QuarterPattern.Match(quarterValue.Trim());
        if (!match.Success) return null;

        var quarterNumber = int.Parse(match.Groups[1].Value);
        var year = int.Parse(match.Groups[2].Value);
        return new ParsedQuarter(year, quarterNumber);
    }

    /// <summary>
    /// Gets the column index (0-based) for a given quarter value in the sorted list.
    /// Returns -1 if not found.
    /// </summary>
    internal static int GetQuarterIndex(List<string> sortedQuarters, string quarter)
    {
        for (int i = 0; i < sortedQuarters.Count; i++)
        {
            if (string.Equals(sortedQuarters[i], quarter, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    /// <summary>
    /// Gets the row index (0-based) for a given category in the sorted list.
    /// Returns -1 if not found.
    /// </summary>
    internal static int GetCategoryIndex(List<string> sortedCategories, string category)
    {
        for (int i = 0; i < sortedCategories.Count; i++)
        {
            if (string.Equals(sortedCategories[i], category, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    private static List<XElement> GenerateSwimlanes(List<string> sortedCategories, int quarterCount)
    {
        var elements = new List<XElement>();
        int totalWidth = SwimlaneStartSize + (quarterCount * QuarterColumnWidth);

        for (int i = 0; i < sortedCategories.Count; i++)
        {
            var category = sortedCategories[i];
            int yPos = QuarterHeaderHeight + (i * SwimlaneHeight);

            var swimlane = new XElement("mxCell",
                new XAttribute("id", $"cat_{SanitizeId(category)}"),
                new XAttribute("value", category),
                new XAttribute("style", "shape=swimlane;horizontal=0;startSize=30;swimlaneHead=0;swimlaneBody=0;fillColor=none;collapsible=0;"),
                new XAttribute("vertex", "1"),
                new XAttribute("parent", "1"),
                new XElement("mxGeometry",
                    new XAttribute("x", "0"),
                    new XAttribute("y", yPos),
                    new XAttribute("width", totalWidth),
                    new XAttribute("height", SwimlaneHeight),
                    new XAttribute("as", "geometry"))
            );

            elements.Add(swimlane);
        }

        return elements;
    }

    private static List<XElement> GenerateQuarterColumns(List<string> sortedQuarters, int categoryCount)
    {
        var elements = new List<XElement>();
        int totalHeight = QuarterHeaderHeight + (categoryCount * SwimlaneHeight);

        for (int i = 0; i < sortedQuarters.Count; i++)
        {
            var quarter = sortedQuarters[i];
            int xPos = SwimlaneStartSize + (i * QuarterColumnWidth);

            // Quarter label at the top
            var label = new XElement("mxCell",
                new XAttribute("id", $"qlabel_{i}"),
                new XAttribute("value", quarter),
                new XAttribute("style", "text;html=1;align=center;verticalAlign=middle;resizable=0;points=[];autosize=1;"),
                new XAttribute("vertex", "1"),
                new XAttribute("parent", "1"),
                new XElement("mxGeometry",
                    new XAttribute("x", xPos),
                    new XAttribute("y", "0"),
                    new XAttribute("width", QuarterColumnWidth),
                    new XAttribute("height", QuarterHeaderHeight),
                    new XAttribute("as", "geometry"))
            );
            elements.Add(label);

            // Vertical separator line (after each column except potentially the last)
            if (i > 0)
            {
                int separatorX = xPos;
                var separator = new XElement("mxCell",
                    new XAttribute("id", $"qsep_{i}"),
                    new XAttribute("value", ""),
                    new XAttribute("style", "line;strokeWidth=1;dashed=1;"),
                    new XAttribute("vertex", "1"),
                    new XAttribute("parent", "1"),
                    new XElement("mxGeometry",
                        new XAttribute("x", separatorX),
                        new XAttribute("y", "0"),
                        new XAttribute("width", "1"),
                        new XAttribute("height", totalHeight),
                        new XAttribute("as", "geometry"))
                );
                elements.Add(separator);
            }
        }

        return elements;
    }

    private static List<XElement> GenerateActivityNodes(
        RoadmapModel model,
        List<string> sortedQuarters,
        List<string> sortedCategories,
        Dictionary<string, string> activityIdMap)
    {
        var elements = new List<XElement>();

        // Group activities by (category, quarter) for stacking
        var cellGroups = new Dictionary<(int catIdx, int qIdx), int>();

        for (int actIdx = 0; actIdx < model.Activities.Count; actIdx++)
        {
            var activity = model.Activities[actIdx];
            if (string.IsNullOrWhiteSpace(activity.Quarter) || string.IsNullOrWhiteSpace(activity.Category))
                continue;

            int catIndex = GetCategoryIndex(sortedCategories, activity.Category!);
            int qIndex = GetQuarterIndex(sortedQuarters, activity.Quarter!);
            if (catIndex < 0 || qIndex < 0) continue;

            var cellKey = (catIndex, qIndex);
            if (!cellGroups.TryGetValue(cellKey, out int stackCount))
                stackCount = 0;

            // Position within the swimlane (relative to swimlane geometry)
            int xInSwimlane = SwimlaneStartSize + (qIndex * QuarterColumnWidth) + ActivityPaddingX;
            int yInSwimlane = ActivityPaddingY + (stackCount * (ActivityHeight + ActivityStackGap));

            string actId = $"act_{actIdx}";
            activityIdMap[activity.Label] = actId;

            var parentId = $"cat_{SanitizeId(sortedCategories[catIndex])}";

            var node = new XElement("mxCell",
                new XAttribute("id", actId),
                new XAttribute("value", activity.Label),
                new XAttribute("style", "rounded=1;whiteSpace=wrap;html=1;"),
                new XAttribute("vertex", "1"),
                new XAttribute("parent", parentId),
                new XElement("mxGeometry",
                    new XAttribute("x", xInSwimlane),
                    new XAttribute("y", yInSwimlane),
                    new XAttribute("width", ActivityWidth),
                    new XAttribute("height", ActivityHeight),
                    new XAttribute("as", "geometry"))
            );

            elements.Add(node);
            cellGroups[cellKey] = stackCount + 1;
        }

        return elements;
    }

    private static List<XElement> GenerateEdges(RoadmapModel model, Dictionary<string, string> activityIdMap)
    {
        var elements = new List<XElement>();
        int edgeIdx = 0;

        foreach (var activity in model.Activities)
        {
            if (!activityIdMap.TryGetValue(activity.Label, out var targetId))
                continue;

            foreach (var depLabel in activity.DependencyLabels)
            {
                if (!activityIdMap.TryGetValue(depLabel, out var sourceId))
                    continue;

                var edge = new XElement("mxCell",
                    new XAttribute("id", $"edge_{edgeIdx}"),
                    new XAttribute("value", ""),
                    new XAttribute("style", "edgeStyle=orthogonalEdgeStyle;"),
                    new XAttribute("edge", "1"),
                    new XAttribute("source", sourceId),
                    new XAttribute("target", targetId),
                    new XAttribute("parent", "1")
                );

                elements.Add(edge);
                edgeIdx++;
            }
        }

        return elements;
    }

    private static string SanitizeId(string value)
    {
        // Replace non-alphanumeric characters with underscores for use in XML id attributes
        return Regex.Replace(value, @"[^a-zA-Z0-9]", "_");
    }
}
