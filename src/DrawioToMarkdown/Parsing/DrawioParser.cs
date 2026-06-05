using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DrawioToMarkdown.Graph;

namespace DrawioToMarkdown.Parsing;

/// <summary>
/// Parses draw.io XML content into an intermediate representation.
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

        var nodes = new List<ActivityNode>();
        var edges = new List<DependencyEdge>();

        if (root is null)
        {
            return new ParsedDiagram(nodes, edges);
        }

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
                var rawLabel = cell.Attribute("value")?.Value ?? string.Empty;
                var label = StripHtml(rawLabel);

                // Skip activities with blank labels
                if (string.IsNullOrWhiteSpace(label))
                {
                    continue;
                }

                nodes.Add(new ActivityNode(id, label));
            }
            else if (isEdge)
            {
                var source = cell.Attribute("source")?.Value;
                var target = cell.Attribute("target")?.Value;

                if (source is not null && target is not null)
                {
                    edges.Add(new DependencyEdge(source, target));
                }
            }
        }

        return new ParsedDiagram(nodes, edges);
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
