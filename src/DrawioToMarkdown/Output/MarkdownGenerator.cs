using System.Text;
using DrawioToMarkdown.Graph;

namespace DrawioToMarkdown.Output;

/// <summary>
/// Generates Markdown documentation from a DependencyGraph.
/// </summary>
public sealed class MarkdownGenerator : IMarkdownGenerator
{
    public string Generate(DependencyGraph graph)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# Dependency Documentation");
        sb.AppendLine();

        var sortedNodes = graph.Nodes.Values
            .OrderBy(n => n.Label, StringComparer.Ordinal)
            .ToList();

        foreach (var node in sortedNodes)
        {
            sb.AppendLine($"## {node.Label}");
            sb.AppendLine();
            sb.AppendLine("### Depends on");
            sb.AppendLine();

            if (graph.Dependencies.TryGetValue(node.Id, out var depIds) && depIds.Count > 0)
            {
                var labels = depIds
                    .Select(id => graph.Nodes[id].Label)
                    .OrderBy(label => label, StringComparer.Ordinal)
                    .ToList();

                foreach (var label in labels)
                {
                    sb.AppendLine($"- {label}");
                }
            }
            else
            {
                sb.AppendLine("No dependencies");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }
}
