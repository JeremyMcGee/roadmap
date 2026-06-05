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
                var label = cell.Attribute("value")?.Value ?? string.Empty;
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
}
