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
/// When activities in the same quarter have dependencies between them, they are
/// placed side-by-side (antecedent left, dependent right) and the column is widened.
/// </summary>
public class DiagramGenerator : IDiagramGenerator
{
    private static readonly Regex QuarterPattern = new(@"^Q(\d+)\s+(\d{4})$", RegexOptions.Compiled);

    // Layout constants
    private const int SwimlaneStartSize = 30;
    private const int MinQuarterColumnWidth = 250;
    private const int MinSwimlaneHeight = 150;
    private const int QuarterHeaderHeight = 40;
    private const int ActivityWidth = 120;
    private const int ActivityHeight = 40;
    private const int ActivityPaddingX = 40;
    private const int ActivityPaddingY = 50;
    private const int ActivityStackGap = 50;
    private const int ActivityHorizontalGap = 60;

    /// <summary>
    /// Generates Draw.IO-compatible XML from a validated RoadmapModel.
    /// </summary>
    public string Generate(RoadmapModel model)
    {
        var sortedQuarters = GetSortedQuarters(model);
        var sortedCategories = GetSortedCategories(model);

        // Compute how many horizontal slots each quarter column needs
        // (based on same-quarter intra-cell dependency chains)
        var columnSlots = ComputeColumnSlots(model, sortedQuarters, sortedCategories);
        var columnWidths = ComputeColumnWidths(model, sortedQuarters, sortedCategories, columnSlots);

        // Compute cumulative x-offsets for each column
        var columnXOffsets = new List<int>(sortedQuarters.Count);
        int xAccum = SwimlaneStartSize;
        for (int i = 0; i < sortedQuarters.Count; i++)
        {
            columnXOffsets.Add(xAccum);
            xAccum += columnWidths[i];
        }
        int totalWidth = xAccum;

        // Compute swimlane heights based on vertical stacking per cell
        var swimlaneHeights = ComputeSwimlaneHeights(model, sortedQuarters, sortedCategories, columnSlots);

        var root = new XElement("root",
            new XElement("mxCell", new XAttribute("id", "0")),
            new XElement("mxCell", new XAttribute("id", "1"), new XAttribute("parent", "0"))
        );

        // Generate quarter shading rectangles (behind everything)
        var shadingElements = GenerateQuarterShading(sortedQuarters, columnWidths, columnXOffsets, sortedCategories, swimlaneHeights);
        foreach (var el in shadingElements)
            root.Add(el);

        // Generate category separator lines
        var separatorElements = GenerateCategorySeparators(sortedCategories, totalWidth, swimlaneHeights);
        foreach (var el in separatorElements)
            root.Add(el);

        // Generate swimlanes
        var swimlaneElements = GenerateSwimlanes(sortedCategories, totalWidth, swimlaneHeights);
        foreach (var el in swimlaneElements)
            root.Add(el);

        // Generate quarter column separators and labels
        var quarterElements = GenerateQuarterColumns(sortedQuarters, columnWidths, columnXOffsets, sortedCategories, swimlaneHeights);
        foreach (var el in quarterElements)
            root.Add(el);

        // Generate activity nodes with side-by-side placement for same-quarter deps
        var activityIdMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var activityPositions = new Dictionary<string, (int x, int y, int w, int h)>();
        var activityElements = GenerateActivityNodes(model, sortedQuarters, sortedCategories, columnSlots, columnXOffsets, swimlaneHeights, activityIdMap, activityPositions);
        foreach (var el in activityElements)
            root.Add(el);

        // Generate dependency edges with obstacle-aware waypoint routing
        var edgeElements = GenerateEdges(model, activityIdMap, sortedQuarters, activityPositions);
        foreach (var el in edgeElements)
            root.Add(el);

        var mxGraphModel = new XElement("mxGraphModel", root);
        var diagram = new XElement("diagram", new XAttribute("name", "Page-1"), mxGraphModel);
        var mxFile = new XElement("mxfile", diagram);

        var doc = new XDocument(new XDeclaration("1.0", "UTF-8", null), mxFile);
        return doc.ToString(SaveOptions.None);
    }

    /// <summary>
    /// For each (category, quarter) cell, computes a topological ordering of activities
    /// based on same-quarter dependencies within that cell. Returns the horizontal slot
    /// index (0-based) for each activity, and the max slot count per quarter column.
    /// </summary>
    private static Dictionary<string, int> ComputeColumnSlots(
        RoadmapModel model,
        List<string> sortedQuarters,
        List<string> sortedCategories)
    {
        // Map activity label → (catIdx, qIdx)
        var activityCell = new Dictionary<string, (int catIdx, int qIdx)>(StringComparer.OrdinalIgnoreCase);
        foreach (var act in model.Activities)
        {
            if (string.IsNullOrWhiteSpace(act.Quarter) || string.IsNullOrWhiteSpace(act.Category))
                continue;
            int catIdx = GetCategoryIndex(sortedCategories, act.Category!);
            int qIdx = GetQuarterIndex(sortedQuarters, act.Quarter!);
            if (catIdx >= 0 && qIdx >= 0)
                activityCell[act.Label] = (catIdx, qIdx);
        }

        // Group activities by cell
        var cellActivities = new Dictionary<(int catIdx, int qIdx), List<string>>();
        foreach (var (label, cell) in activityCell)
        {
            if (!cellActivities.ContainsKey(cell))
                cellActivities[cell] = new List<string>();
            cellActivities[cell].Add(label);
        }

        // For each cell, compute horizontal slot via topological sort based on intra-cell deps
        var slotMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var (cell, labels) in cellActivities)
        {
            var labelsInCell = new HashSet<string>(labels, StringComparer.OrdinalIgnoreCase);

            // Build intra-cell dependency graph: dep edges where both source and target are in same cell
            var inDegree = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var dependents = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var label in labels)
            {
                inDegree[label] = 0;
                dependents[label] = new List<string>();
            }

            foreach (var act in model.Activities)
            {
                if (!labelsInCell.Contains(act.Label)) continue;
                foreach (var dep in act.DependencyLabels)
                {
                    if (labelsInCell.Contains(dep))
                    {
                        // dep (antecedent) → act.Label (dependent)
                        dependents[dep].Add(act.Label);
                        inDegree[act.Label]++;
                    }
                }
            }

            // Topological sort (Kahn's algorithm) to assign horizontal slots
            var queue = new Queue<string>();
            foreach (var label in labels)
            {
                if (inDegree[label] == 0)
                    queue.Enqueue(label);
            }

            var slotAssignment = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                // Slot = max slot of antecedents + 1, or 0 if no antecedents in cell
                int slot = 0;
                // Find all intra-cell antecedents of current
                var act = model.Activities.First(a => string.Equals(a.Label, current, StringComparison.OrdinalIgnoreCase));
                foreach (var dep in act.DependencyLabels)
                {
                    if (labelsInCell.Contains(dep) && slotAssignment.TryGetValue(dep, out int depSlot))
                    {
                        slot = Math.Max(slot, depSlot + 1);
                    }
                }
                slotAssignment[current] = slot;

                foreach (var next in dependents[current])
                {
                    inDegree[next]--;
                    if (inDegree[next] == 0)
                        queue.Enqueue(next);
                }
            }

            // Handle any remaining (cyclic) — just assign slot 0
            foreach (var label in labels)
            {
                if (!slotAssignment.ContainsKey(label))
                    slotAssignment[label] = 0;
            }

            foreach (var (label, slot) in slotAssignment)
                slotMap[label] = slot;
        }

        return slotMap;
    }

    /// <summary>
    /// Computes per-quarter-column widths based on the maximum number of horizontal slots
    /// needed in any cell within that column.
    /// </summary>
    private static List<int> ComputeColumnWidths(
        RoadmapModel model,
        List<string> sortedQuarters,
        List<string> sortedCategories,
        Dictionary<string, int> slotMap)
    {
        // Find max slot per quarter column
        var maxSlotPerColumn = new int[sortedQuarters.Count];
        for (int i = 0; i < sortedQuarters.Count; i++)
            maxSlotPerColumn[i] = 0;

        foreach (var act in model.Activities)
        {
            if (string.IsNullOrWhiteSpace(act.Quarter) || string.IsNullOrWhiteSpace(act.Category))
                continue;
            int qIdx = GetQuarterIndex(sortedQuarters, act.Quarter!);
            if (qIdx < 0) continue;

            if (slotMap.TryGetValue(act.Label, out int slot))
            {
                if (slot > maxSlotPerColumn[qIdx])
                    maxSlotPerColumn[qIdx] = slot;
            }
        }

        var widths = new List<int>(sortedQuarters.Count);
        for (int i = 0; i < sortedQuarters.Count; i++)
        {
            int slotsNeeded = maxSlotPerColumn[i] + 1; // 0-based, so +1
            int width = ActivityPaddingX + (slotsNeeded * ActivityWidth) + ((slotsNeeded - 1) * ActivityHorizontalGap) + ActivityPaddingX;
            widths.Add(Math.Max(MinQuarterColumnWidth, width));
        }

        return widths;
    }

    /// <summary>
    /// Computes swimlane heights. For each cell, activities at the same horizontal slot
    /// are stacked vertically. The height is based on the tallest stack in any cell of that row.
    /// </summary>
    private static Dictionary<int, int> ComputeSwimlaneHeights(
        RoadmapModel model,
        List<string> sortedQuarters,
        List<string> sortedCategories,
        Dictionary<string, int> slotMap)
    {
        // For each (catIdx, qIdx, slot) count how many activities stack
        var stackCounts = new Dictionary<(int catIdx, int qIdx, int slot), int>();

        foreach (var act in model.Activities)
        {
            if (string.IsNullOrWhiteSpace(act.Quarter) || string.IsNullOrWhiteSpace(act.Category))
                continue;
            int catIdx = GetCategoryIndex(sortedCategories, act.Category!);
            int qIdx = GetQuarterIndex(sortedQuarters, act.Quarter!);
            if (catIdx < 0 || qIdx < 0) continue;

            int slot = slotMap.GetValueOrDefault(act.Label, 0);
            var key = (catIdx, qIdx, slot);
            stackCounts[key] = stackCounts.GetValueOrDefault(key, 0) + 1;
        }

        var heights = new Dictionary<int, int>();
        for (int i = 0; i < sortedCategories.Count; i++)
        {
            int maxStack = 1;
            foreach (var kvp in stackCounts)
            {
                if (kvp.Key.catIdx == i && kvp.Value > maxStack)
                    maxStack = kvp.Value;
            }

            int computedHeight = ActivityPaddingY + (maxStack * ActivityHeight) + ((maxStack - 1) * ActivityStackGap) + ActivityPaddingY;
            heights[i] = Math.Max(MinSwimlaneHeight, computedHeight);
        }

        return heights;
    }

    /// <summary>
    /// Gets the sorted list of distinct quarter values from the model.
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
                parseable.Add((q!, parsed));
            else
                unparseable.Add(q!);
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

    // Pastel colors that cycle for quarter columns
    private static readonly string[] QuarterPastelColors = new[]
    {
        "#E8F4FD", // light blue
        "#FFF3E0", // light orange
        "#E8F5E9", // light green
        "#F3E5F5", // light purple
        "#FFF9C4", // light yellow
        "#E0F7FA", // light cyan
        "#FCE4EC", // light pink
        "#F1F8E9", // light lime
    };

    /// <summary>
    /// Generates pastel-colored rectangles behind each quarter column.
    /// </summary>
    private static List<XElement> GenerateQuarterShading(
        List<string> sortedQuarters,
        List<int> columnWidths,
        List<int> columnXOffsets,
        List<string> sortedCategories,
        Dictionary<int, int> swimlaneHeights)
    {
        var elements = new List<XElement>();
        int totalHeight = QuarterHeaderHeight;
        for (int i = 0; i < sortedCategories.Count; i++)
            totalHeight += swimlaneHeights.GetValueOrDefault(i, MinSwimlaneHeight);

        for (int i = 0; i < sortedQuarters.Count; i++)
        {
            var color = QuarterPastelColors[i % QuarterPastelColors.Length];

            var shading = new XElement("mxCell",
                new XAttribute("id", $"qshade_{i}"),
                new XAttribute("value", ""),
                new XAttribute("style", $"rounded=0;whiteSpace=wrap;html=1;fillColor={color};strokeColor=none;opacity=50;"),
                new XAttribute("vertex", "1"),
                new XAttribute("parent", "1"),
                new XElement("mxGeometry",
                    new XAttribute("x", columnXOffsets[i]),
                    new XAttribute("y", QuarterHeaderHeight),
                    new XAttribute("width", columnWidths[i]),
                    new XAttribute("height", totalHeight - QuarterHeaderHeight),
                    new XAttribute("as", "geometry"))
            );

            elements.Add(shading);
        }

        return elements;
    }

    /// <summary>
    /// Generates grey horizontal separator lines between category swimlanes.
    /// </summary>
    private static List<XElement> GenerateCategorySeparators(
        List<string> sortedCategories,
        int totalWidth,
        Dictionary<int, int> swimlaneHeights)
    {
        var elements = new List<XElement>();

        // Lines go between swimlanes (not above the first or below the last)
        int yPos = QuarterHeaderHeight;
        for (int i = 0; i < sortedCategories.Count; i++)
        {
            yPos += swimlaneHeights.GetValueOrDefault(i, MinSwimlaneHeight);

            // Add separator after each category except the last
            if (i < sortedCategories.Count - 1)
            {
                var separator = new XElement("mxCell",
                    new XAttribute("id", $"catsep_{i}"),
                    new XAttribute("value", ""),
                    new XAttribute("style", "line;strokeWidth=1;strokeColor=#999999;"),
                    new XAttribute("vertex", "1"),
                    new XAttribute("parent", "1"),
                    new XElement("mxGeometry",
                        new XAttribute("x", "0"),
                        new XAttribute("y", yPos),
                        new XAttribute("width", totalWidth),
                        new XAttribute("height", "1"),
                        new XAttribute("as", "geometry"))
                );
                elements.Add(separator);
            }
        }

        return elements;
    }

    private static List<XElement> GenerateSwimlanes(List<string> sortedCategories, int totalWidth, Dictionary<int, int> swimlaneHeights)
    {
        var elements = new List<XElement>();

        int yPos = QuarterHeaderHeight;
        for (int i = 0; i < sortedCategories.Count; i++)
        {
            var category = sortedCategories[i];
            int height = swimlaneHeights.GetValueOrDefault(i, MinSwimlaneHeight);

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
                    new XAttribute("height", height),
                    new XAttribute("as", "geometry"))
            );

            elements.Add(swimlane);
            yPos += height;
        }

        return elements;
    }

    private static List<XElement> GenerateQuarterColumns(
        List<string> sortedQuarters,
        List<int> columnWidths,
        List<int> columnXOffsets,
        List<string> sortedCategories,
        Dictionary<int, int> swimlaneHeights)
    {
        var elements = new List<XElement>();
        int totalHeight = QuarterHeaderHeight;
        for (int i = 0; i < sortedCategories.Count; i++)
            totalHeight += swimlaneHeights.GetValueOrDefault(i, MinSwimlaneHeight);

        for (int i = 0; i < sortedQuarters.Count; i++)
        {
            var quarter = sortedQuarters[i];
            int xPos = columnXOffsets[i];

            var label = new XElement("mxCell",
                new XAttribute("id", $"qlabel_{i}"),
                new XAttribute("value", quarter),
                new XAttribute("style", "text;html=1;align=center;verticalAlign=middle;resizable=0;points=[];autosize=1;"),
                new XAttribute("vertex", "1"),
                new XAttribute("parent", "1"),
                new XElement("mxGeometry",
                    new XAttribute("x", xPos),
                    new XAttribute("y", "0"),
                    new XAttribute("width", columnWidths[i]),
                    new XAttribute("height", QuarterHeaderHeight),
                    new XAttribute("as", "geometry"))
            );
            elements.Add(label);

            if (i > 0)
            {
                var separator = new XElement("mxCell",
                    new XAttribute("id", $"qsep_{i}"),
                    new XAttribute("value", ""),
                    new XAttribute("style", "line;strokeWidth=1;dashed=1;"),
                    new XAttribute("vertex", "1"),
                    new XAttribute("parent", "1"),
                    new XElement("mxGeometry",
                        new XAttribute("x", xPos),
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
        Dictionary<string, int> slotMap,
        List<int> columnXOffsets,
        Dictionary<int, int> swimlaneHeights,
        Dictionary<string, string> activityIdMap,
        Dictionary<string, (int x, int y, int w, int h)> activityPositions)
    {
        var elements = new List<XElement>();

        // Compute absolute y-offset for each category swimlane
        var categoryYOffsets = new Dictionary<int, int>();
        int yAccum = QuarterHeaderHeight;
        for (int i = 0; i < sortedCategories.Count; i++)
        {
            categoryYOffsets[i] = yAccum;
            yAccum += swimlaneHeights.GetValueOrDefault(i, MinSwimlaneHeight);
        }

        // Build lookup: label → catIndex
        var labelToCatIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var act in model.Activities)
        {
            if (!string.IsNullOrWhiteSpace(act.Category))
            {
                int catIdx = GetCategoryIndex(sortedCategories, act.Category!);
                if (catIdx >= 0) labelToCatIndex[act.Label] = catIdx;
            }
        }

        // Group activities by (catIdx, qIdx, slot) for vertical ordering
        var cellActivities = new Dictionary<(int catIdx, int qIdx, int slot), List<int>>();
        for (int actIdx = 0; actIdx < model.Activities.Count; actIdx++)
        {
            var activity = model.Activities[actIdx];
            if (string.IsNullOrWhiteSpace(activity.Quarter) || string.IsNullOrWhiteSpace(activity.Category))
                continue;
            int catIndex = GetCategoryIndex(sortedCategories, activity.Category!);
            int qIndex = GetQuarterIndex(sortedQuarters, activity.Quarter!);
            if (catIndex < 0 || qIndex < 0) continue;
            int slot = slotMap.GetValueOrDefault(activity.Label, 0);
            var key = (catIndex, qIndex, slot);
            if (!cellActivities.ContainsKey(key))
                cellActivities[key] = new List<int>();
            cellActivities[key].Add(actIdx);
        }

        // Sort activities within each cell by "gravity" — activities with edges to
        // swimlanes above should be placed higher (lower y), activities with edges
        // to swimlanes below should be placed lower (higher y).
        foreach (var (key, indices) in cellActivities)
        {
            var thisCatIdx = key.catIdx;

            indices.Sort((a, b) =>
            {
                var actA = model.Activities[a];
                var actB = model.Activities[b];
                float gravityA = ComputeGravity(actA, thisCatIdx, labelToCatIndex, model);
                float gravityB = ComputeGravity(actB, thisCatIdx, labelToCatIndex, model);
                return gravityA.CompareTo(gravityB);
            });
        }

        // Now place activities using the sorted order
        foreach (var (key, indices) in cellActivities)
        {
            var (catIndex, qIndex, slot) = key;
            for (int stackPos = 0; stackPos < indices.Count; stackPos++)
            {
                int actIdx = indices[stackPos];
                var activity = model.Activities[actIdx];

                int xAbsolute = columnXOffsets[qIndex] + ActivityPaddingX + (slot * (ActivityWidth + ActivityHorizontalGap));
                int yAbsolute = categoryYOffsets[catIndex] + ActivityPaddingY + (stackPos * (ActivityHeight + ActivityStackGap));

                string actId = $"act_{actIdx}";
                activityIdMap[activity.Label] = actId;
                activityPositions[actId] = (xAbsolute, yAbsolute, ActivityWidth, ActivityHeight);

                var node = new XElement("mxCell",
                    new XAttribute("id", actId),
                    new XAttribute("value", activity.Label),
                    new XAttribute("style", "rounded=1;whiteSpace=wrap;html=1;"),
                    new XAttribute("vertex", "1"),
                    new XAttribute("parent", "1"),
                    new XElement("mxGeometry",
                        new XAttribute("x", xAbsolute),
                        new XAttribute("y", yAbsolute),
                        new XAttribute("width", ActivityWidth),
                        new XAttribute("height", ActivityHeight),
                        new XAttribute("as", "geometry"))
                );

                elements.Add(node);
            }
        }

        return elements;
    }

    /// <summary>
    /// Computes a "gravity" score for an activity within its swimlane.
    /// Negative = edges pull it toward the top (connected to swimlanes above).
    /// Positive = edges pull it toward the bottom (connected to swimlanes below).
    /// Zero = no cross-swimlane connections or balanced.
    /// This sorts activities so that those with connections above sit higher in the cell,
    /// reducing the chance that edges to other swimlanes pass through sibling boxes.
    /// </summary>
    private static float ComputeGravity(
        Activity activity,
        int thisCatIdx,
        Dictionary<string, int> labelToCatIndex,
        RoadmapModel model)
    {
        float gravity = 0;

        // Outgoing: this activity's dependencies (antecedents)
        foreach (var dep in activity.DependencyLabels)
        {
            if (labelToCatIndex.TryGetValue(dep, out int depCat) && depCat != thisCatIdx)
            {
                gravity += (depCat - thisCatIdx); // negative if above, positive if below
            }
        }

        // Incoming: other activities that depend on this one
        foreach (var other in model.Activities)
        {
            if (other.DependencyLabels.Contains(activity.Label, StringComparer.OrdinalIgnoreCase))
            {
                if (labelToCatIndex.TryGetValue(other.Label, out int otherCat) && otherCat != thisCatIdx)
                {
                    gravity += (otherCat - thisCatIdx);
                }
            }
        }

        return gravity;
    }

    private static List<XElement> GenerateEdges(
        RoadmapModel model,
        Dictionary<string, string> activityIdMap,
        List<string> sortedQuarters,
        Dictionary<string, (int x, int y, int w, int h)> activityPositions)
    {
        var elements = new List<XElement>();
        int edgeIdx = 0;

        var labelToQuarterIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var act in model.Activities)
        {
            if (!string.IsNullOrWhiteSpace(act.Quarter))
            {
                var qIdx = GetQuarterIndex(sortedQuarters, act.Quarter!);
                if (qIdx >= 0)
                    labelToQuarterIndex[act.Label] = qIdx;
            }
        }

        // Collect all obstacle rectangles (with margin)
        const int ObstacleMargin = 10;
        var obstacles = activityPositions.ToDictionary(
            kvp => kvp.Key,
            kvp => (
                x: kvp.Value.x - ObstacleMargin,
                y: kvp.Value.y - ObstacleMargin,
                w: kvp.Value.w + ObstacleMargin * 2,
                h: kvp.Value.h + ObstacleMargin * 2
            ));

        foreach (var activity in model.Activities)
        {
            if (!activityIdMap.TryGetValue(activity.Label, out var targetId))
                continue;

            foreach (var depLabel in activity.DependencyLabels)
            {
                if (!activityIdMap.TryGetValue(depLabel, out var sourceId))
                    continue;

                // Red if antecedent is in a strictly later quarter
                bool isBackward = false;
                if (labelToQuarterIndex.TryGetValue(depLabel, out var sourceQIdx) &&
                    labelToQuarterIndex.TryGetValue(activity.Label, out var targetQIdx))
                {
                    isBackward = sourceQIdx > targetQIdx;
                }

                var style = isBackward
                    ? "edgeStyle=orthogonalEdgeStyle;rounded=1;orthogonalLoop=1;jettySize=auto;html=1;jumpStyle=arc;jumpSize=10;strokeColor=#FF0000;"
                    : "edgeStyle=orthogonalEdgeStyle;rounded=1;orthogonalLoop=1;jettySize=auto;html=1;jumpStyle=arc;jumpSize=10;";

                // Compute waypoints to route around obstacles
                var waypoints = ComputeWaypoints(sourceId, targetId, activityPositions, obstacles);

                var geometry = new XElement("mxGeometry",
                    new XAttribute("relative", "1"),
                    new XAttribute("as", "geometry"));

                if (waypoints.Count > 0)
                {
                    var pointsArray = new XElement("Array", new XAttribute("as", "points"));
                    foreach (var (wx, wy) in waypoints)
                    {
                        pointsArray.Add(new XElement("mxPoint",
                            new XAttribute("x", wx),
                            new XAttribute("y", wy)));
                    }
                    geometry.Add(pointsArray);
                }

                var edge = new XElement("mxCell",
                    new XAttribute("id", $"edge_{edgeIdx}"),
                    new XAttribute("value", ""),
                    new XAttribute("style", style),
                    new XAttribute("edge", "1"),
                    new XAttribute("source", sourceId),
                    new XAttribute("target", targetId),
                    new XAttribute("parent", "1"),
                    geometry
                );

                elements.Add(edge);
                edgeIdx++;
            }
        }

        return elements;
    }

    /// <summary>
    /// Computes waypoints to route an orthogonal edge from source to target
    /// while avoiding obstacle rectangles (other activity boxes).
    /// Returns a list of intermediate (x, y) points the edge should pass through.
    /// </summary>
    private static List<(int x, int y)> ComputeWaypoints(
        string sourceId,
        string targetId,
        Dictionary<string, (int x, int y, int w, int h)> positions,
        Dictionary<string, (int x, int y, int w, int h)> obstacles)
    {
        var waypoints = new List<(int x, int y)>();

        if (!positions.TryGetValue(sourceId, out var srcPos) ||
            !positions.TryGetValue(targetId, out var tgtPos))
            return waypoints;

        // Source exits from the right-center, target enters from the left-center
        int srcExitX = srcPos.x + srcPos.w;
        int srcExitY = srcPos.y + srcPos.h / 2;
        int tgtEntryX = tgtPos.x;
        int tgtEntryY = tgtPos.y + tgtPos.h / 2;

        // For a simple orthogonal route: go right from source, then vertical, then right to target
        // Check if the straight horizontal/vertical segments would intersect any obstacle

        // Collect obstacles that are NOT the source or target
        var relevantObstacles = obstacles
            .Where(kvp => kvp.Key != sourceId && kvp.Key != targetId)
            .Select(kvp => kvp.Value)
            .ToList();

        if (relevantObstacles.Count == 0)
            return waypoints; // No obstacles, let Draw.io handle it

        // Determine routing direction
        bool goingRight = tgtEntryX > srcExitX;
        bool goingDown = tgtEntryY > srcExitY;

        if (srcExitY == tgtEntryY)
        {
            // Same horizontal level — check for obstacles in the horizontal path
            var blocked = relevantObstacles.Any(obs =>
                HSegmentIntersectsRect(srcExitX, tgtEntryX, srcExitY, obs));

            if (blocked)
            {
                // Route above or below the obstacle
                int midX = (srcExitX + tgtEntryX) / 2;

                // Find the obstacle blocking us
                var blockingObs = relevantObstacles
                    .Where(obs => HSegmentIntersectsRect(srcExitX, tgtEntryX, srcExitY, obs))
                    .ToList();

                // Route above or below — pick whichever is shorter
                int topRoute = blockingObs.Min(obs => obs.y) - 10;
                int bottomRoute = blockingObs.Max(obs => obs.y + obs.h) + 10;

                int routeY = (Math.Abs(srcExitY - topRoute) <= Math.Abs(srcExitY - bottomRoute))
                    ? topRoute : bottomRoute;

                waypoints.Add((midX, srcExitY));
                waypoints.Add((midX, routeY));
                waypoints.Add((midX + (tgtEntryX - srcExitX) / 2, routeY));
                waypoints.Add((midX + (tgtEntryX - srcExitX) / 2, tgtEntryY));
            }
        }
        else
        {
            // Different vertical levels — standard orthogonal route is:
            // Exit right → go to midX → turn vertical → go to tgtEntryY → turn right to target
            int midX = (srcExitX + tgtEntryX) / 2;

            // Check if the vertical segment at midX intersects any obstacle
            var verticalBlocked = relevantObstacles.Any(obs =>
                VSegmentIntersectsRect(midX, srcExitY, tgtEntryY, obs));

            if (verticalBlocked)
            {
                // Find a clear x-position for the vertical segment
                // Try routing through the gap between source column and target column
                var blockingObs = relevantObstacles
                    .Where(obs => VSegmentIntersectsRect(midX, srcExitY, tgtEntryY, obs))
                    .ToList();

                // Try shifting the vertical segment to the left of the blocking obstacles
                int clearX = blockingObs.Min(obs => obs.x) - 15;
                if (clearX <= srcExitX)
                {
                    // Or try to the right of them
                    clearX = blockingObs.Max(obs => obs.x + obs.w) + 15;
                }

                waypoints.Add((clearX, srcExitY));
                waypoints.Add((clearX, tgtEntryY));
            }
            // If not blocked, Draw.io's orthogonal router handles it fine with no waypoints
        }

        return waypoints;
    }

    /// <summary>
    /// Checks if a horizontal line segment from x1 to x2 at height y intersects a rectangle.
    /// </summary>
    private static bool HSegmentIntersectsRect(int x1, int x2, int y, (int x, int y, int w, int h) rect)
    {
        int minX = Math.Min(x1, x2);
        int maxX = Math.Max(x1, x2);

        // Check if y is within the rect's vertical range
        if (y < rect.y || y > rect.y + rect.h)
            return false;

        // Check if the horizontal span overlaps the rect
        return maxX > rect.x && minX < rect.x + rect.w;
    }

    /// <summary>
    /// Checks if a vertical line segment from y1 to y2 at x intersects a rectangle.
    /// </summary>
    private static bool VSegmentIntersectsRect(int x, int y1, int y2, (int x, int y, int w, int h) rect)
    {
        int minY = Math.Min(y1, y2);
        int maxY = Math.Max(y1, y2);

        // Check if x is within the rect's horizontal range
        if (x < rect.x || x > rect.x + rect.w)
            return false;

        // Check if the vertical span overlaps the rect
        return maxY > rect.y && minY < rect.y + rect.h;
    }

    private static string SanitizeId(string value)
    {
        return Regex.Replace(value, @"[^a-zA-Z0-9]", "_");
    }
}
