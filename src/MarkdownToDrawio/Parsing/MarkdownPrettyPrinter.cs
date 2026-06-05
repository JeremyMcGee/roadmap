using System.Text;
using MarkdownToDrawio.Model;

namespace MarkdownToDrawio.Parsing;

/// <summary>
/// Produces canonical Markdown from a RoadmapModel.
/// Designed so that Parse(Print(model)) reproduces the same model.
/// </summary>
public sealed class MarkdownPrettyPrinter
{
    /// <summary>
    /// Converts a RoadmapModel into a canonical Markdown string.
    /// Activities are sorted lexicographically by label.
    /// Sub-sections appear in order: Depends on, Quarter, Category.
    /// Dependencies are rendered as bullet links sorted lexicographically.
    /// </summary>
    public string Print(RoadmapModel model)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# Dependency Documentation");

        var sortedActivities = model.Activities
            .OrderBy(a => a.Label, StringComparer.Ordinal)
            .ToList();

        foreach (var activity in sortedActivities)
        {
            sb.AppendLine();
            sb.AppendLine($"## {activity.Label}");
            sb.AppendLine();
            sb.AppendLine("### Depends on");
            sb.AppendLine();

            if (activity.DependencyLabels.Count == 0)
            {
                sb.AppendLine("No dependencies");
            }
            else
            {
                var sortedDeps = activity.DependencyLabels
                    .OrderBy(d => d, StringComparer.Ordinal)
                    .ToList();

                foreach (var dep in sortedDeps)
                {
                    var anchor = GenerateAnchor(dep);
                    sb.AppendLine($"- [{dep}](#{anchor})");
                }
            }

            sb.AppendLine();
            sb.AppendLine("### Quarter");
            sb.AppendLine();
            sb.AppendLine(activity.Quarter ?? string.Empty);
            sb.AppendLine();
            sb.AppendLine("### Category");
            sb.AppendLine();
            sb.AppendLine(activity.Category ?? string.Empty);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generates a Markdown anchor from a label by lowercasing and replacing spaces with hyphens.
    /// </summary>
    private static string GenerateAnchor(string label)
    {
        return label.ToLowerInvariant().Replace(' ', '-');
    }
}
