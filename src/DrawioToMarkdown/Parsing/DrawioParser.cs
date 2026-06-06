using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace DrawioToMarkdown.Parsing;

/// <summary>
/// Parses draw.io XML content into a <see cref="ParsedDiagram"/>.
/// Uses XDocument.Parse() to load the XML and navigates through
/// mxfile/diagram/mxGraphModel/root to find mxCell elements.
/// </summary>
public sealed class DrawioParser : IDrawioParser
{
    public ParsedDiagram Parse(string xmlContent)
    {
        // XDocument.Parse will throw XmlException on malformed XML
        var doc = XDocument.Parse(xmlContent);

        var root = doc.Element("mxfile")
            ?.Element("diagram")
            ?.Element("mxGraphModel")
            ?.Element("root");

        if (root is null)
        {
            throw new InvalidOperationException(
                "Not a recognized draw.io diagram: missing mxfile/diagram/mxGraphModel/root structure.");
        }

        var swimlanes = new List<SwimlaneDef>();
        var quarterColumns = new List<QuarterColumnDef>();
        var activityNodes = new List<ActivityNodeDef>();
        var edges = new List<DependencyEdgeDef>();

        foreach (var cell in root.Elements("mxCell"))
        {
            var id = cell.Attribute("id")?.Value;

            // Skip standard draw.io infrastructure cells (id="0" and id="1")
            if (id is null || id == "0" || id == "1")
            {
                continue;
            }

            var isVertex = cell.Attribute("vertex")?.Value == "1";
            var isEdge = cell.Attribute("edge")?.Value == "1";

            if (isVertex)
            {
                var style = cell.Attribute("style")?.Value ?? string.Empty;
                var rawLabel = cell.Attribute("value")?.Value ?? string.Empty;
                var geometry = cell.Element("mxGeometry");
                var x = double.TryParse(geometry?.Attribute("x")?.Value, out var px) ? px : 0.0;
                var y = double.TryParse(geometry?.Attribute("y")?.Value, out var py) ? py : 0.0;
                var width = double.TryParse(geometry?.Attribute("width")?.Value, out var pw) ? pw : 0.0;
                var height = double.TryParse(geometry?.Attribute("height")?.Value, out var ph) ? ph : 0.0;

                // Swimlane detection: style contains "shape=swimlane;horizontal=0"
                if (style.Contains("shape=swimlane") && style.Contains("horizontal=0"))
                {
                    var label = StripHtml(rawLabel);
                    swimlanes.Add(new SwimlaneDef(id, label, x, y, width, height));
                }
                // Quarter column label detection: id starts with "qlabel_"
                else if (id.StartsWith("qlabel_", StringComparison.Ordinal))
                {
                    var label = StripHtml(rawLabel);
                    quarterColumns.Add(new QuarterColumnDef(id, label, x, width));
                }
                // Activity node detection: style contains "rounded=1;whiteSpace=wrap;html=1"
                else if (style.Contains("rounded=1") && style.Contains("whiteSpace=wrap") && style.Contains("html=1"))
                {
                    var label = StripHtml(rawLabel);

                    // Skip activities with blank labels
                    if (!string.IsNullOrWhiteSpace(label))
                    {
                        activityNodes.Add(new ActivityNodeDef(id, label, x, y, width, height));
                    }
                }
            }
            else if (isEdge)
            {
                var source = cell.Attribute("source")?.Value;
                var target = cell.Attribute("target")?.Value;

                if (source is not null && target is not null)
                {
                    edges.Add(new DependencyEdgeDef(source, target));
                }
            }
        }

        // --- Edge validation (post-processing) ---

        // 1. Discard self-referencing edges (Req 3.5)
        var validEdges = edges.Where(e => e.SourceId != e.TargetId);

        // 2. Deduplicate edges with same source-target pair (Req 3.3)
        validEdges = validEdges.Distinct();

        // 3. Discard edges referencing non-activity node IDs (Req 3.2)
        var activityNodeIds = new HashSet<string>(activityNodes.Select(n => n.Id));
        var filteredEdges = validEdges
            .Where(e => activityNodeIds.Contains(e.SourceId) && activityNodeIds.Contains(e.TargetId))
            .ToList();

        // 4. Handle duplicate activity labels by merging dependency sets (Req 6.5)
        //    Group nodes by normalized label; keep one representative per label
        //    and rewrite edges to use the representative node's ID.
        var labelGroups = activityNodes
            .GroupBy(n => n.Label, StringComparer.Ordinal)
            .ToList();

        if (labelGroups.Any(g => g.Count() > 1))
        {
            // Build a mapping from every duplicate node ID to the representative ID (first in group)
            var idToRepresentative = new Dictionary<string, string>();
            var mergedNodes = new List<ActivityNodeDef>();

            foreach (var group in labelGroups)
            {
                var representative = group.First();
                mergedNodes.Add(representative);

                foreach (var node in group)
                {
                    idToRepresentative[node.Id] = representative.Id;
                }
            }

            // Rewrite edges to use representative IDs, then re-filter and deduplicate
            filteredEdges = filteredEdges
                .Select(e => new DependencyEdgeDef(
                    idToRepresentative.GetValueOrDefault(e.SourceId, e.SourceId),
                    idToRepresentative.GetValueOrDefault(e.TargetId, e.TargetId)))
                .Where(e => e.SourceId != e.TargetId) // Merging may introduce self-refs
                .Distinct()
                .ToList();

            activityNodes = mergedNodes;
        }

        return new ParsedDiagram(swimlanes, quarterColumns, activityNodes, filteredEdges);
    }

    /// <summary>
    /// Strips HTML tags from a string and decodes HTML entities.
    /// Draw.io often stores labels with HTML formatting (e.g., &lt;b&gt;Task&lt;/b&gt;).
    /// </summary>
    private static string StripHtml(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        // Remove HTML tags
        var withoutTags = Regex.Replace(input, "<[^>]+>", string.Empty);

        // Decode HTML entities (&amp; → &, &lt; → <, etc.)
        var decoded = WebUtility.HtmlDecode(withoutTags);

        // Collapse whitespace and trim
        return Regex.Replace(decoded, @"\s+", " ").Trim();
    }
}
