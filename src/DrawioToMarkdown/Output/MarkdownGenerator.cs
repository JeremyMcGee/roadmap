using System.Text;
using System.Text.RegularExpressions;
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
                    var anchor = ToAnchor(label);
                    sb.AppendLine($"- [{label}](#{anchor})");
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

    /// <summary>
    /// Converts a heading text to a GitHub/CommonMark-style anchor slug.
    /// Lowercase, spaces become hyphens, non-alphanumeric/hyphen characters removed.
    /// </summary>
    private static string ToAnchor(string heading)
    {
        var lower = heading.ToLowerInvariant();
        var slug = Regex.Replace(lower, @"[^\w\s-]", string.Empty);
        slug = Regex.Replace(slug, @"\s+", "-");
        return slug.Trim('-');
    }
}
