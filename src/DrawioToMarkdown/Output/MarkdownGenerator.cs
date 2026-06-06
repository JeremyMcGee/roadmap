using System.Text;
using DrawioToMarkdown.Model;

namespace DrawioToMarkdown.Output;

/// <summary>
/// Generates roadmap Markdown documentation from a RoadmapModel.
/// Output is byte-for-byte identical to MarkdownToDrawio's MarkdownPrettyPrinter.Print().
/// </summary>
public sealed class MarkdownGenerator : IMarkdownGenerator
{
    public string Generate(RoadmapModel model)
    {
        // Validate: no activity may have null/empty quarter or category
        foreach (var activity in model.Activities)
        {
            if (string.IsNullOrEmpty(activity.Quarter))
            {
                throw new InvalidOperationException(
                    $"Error: Activity \"{activity.Label}\" has no resolved quarter");
            }

            if (string.IsNullOrEmpty(activity.Category))
            {
                throw new InvalidOperationException(
                    $"Error: Activity \"{activity.Label}\" has no resolved category");
            }
        }

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
    /// Uses the same algorithm as MarkdownToDrawio's MarkdownPrettyPrinter for byte-for-byte compatibility.
    /// </summary>
    private static string GenerateAnchor(string label)
    {
        return label.ToLowerInvariant().Replace(' ', '-');
    }
}
