using System.Xml.Linq;
using DrawioToMarkdown.Graph;

namespace DrawioToMarkdown.Output;

/// <summary>
/// Converts a DependencyGraph back to draw.io-compatible XML.
/// Produces output that is parseable by DrawioParser for round-trip testing.
/// </summary>
public sealed class XmlPrettyPrinter : IXmlPrettyPrinter
{
    public string Print(DependencyGraph graph)
    {
        var root = new XElement("root");

        // Standard draw.io infrastructure cells
        root.Add(new XElement("mxCell", new XAttribute("id", "0")));
        root.Add(new XElement("mxCell", new XAttribute("id", "1"), new XAttribute("parent", "0")));

        // Emit each node as a vertex mxCell
        foreach (var node in graph.Nodes.Values)
        {
            root.Add(new XElement("mxCell",
                new XAttribute("id", node.Id),
                new XAttribute("value", node.Label),
                new XAttribute("vertex", "1"),
                new XAttribute("parent", "1")));
        }

        // Emit each edge as an edge mxCell
        foreach (var (nodeId, antecedents) in graph.Dependencies)
        {
            foreach (var antecedentId in antecedents)
            {
                root.Add(new XElement("mxCell",
                    new XAttribute("id", $"e_{antecedentId}_{nodeId}"),
                    new XAttribute("edge", "1"),
                    new XAttribute("source", antecedentId),
                    new XAttribute("target", nodeId),
                    new XAttribute("parent", "1")));
            }
        }

        var doc = new XDocument(
            new XElement("mxfile",
                new XElement("diagram",
                    new XAttribute("name", "Page-1"),
                    new XElement("mxGraphModel", root))));

        return doc.ToString();
    }
}
