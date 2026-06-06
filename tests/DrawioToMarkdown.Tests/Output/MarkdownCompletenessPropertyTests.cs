// Feature: drawio-to-markdown, Property 4: Markdown Output Completeness
using DrawioToMarkdown.Model;
using DrawioToMarkdown.Output;
using DrawioToMarkdown.Tests.Generators;
using FsCheck;
using FsCheck.Xunit;

namespace DrawioToMarkdown.Tests.Output;

/// <summary>
/// Property 4: Markdown Output Completeness
/// Validates: Requirements 3.2, 3.3, 3.4, 3.5
///
/// For any valid RoadmapModel, the generated Markdown string SHALL contain:
/// (a) the heading "# Dependency Documentation",
/// (b) a "## {label}" section for every activity in the model,
/// (c) for each activity with dependencies, a "Depends on" sub-section listing each dependency label,
/// (d) for each activity without dependencies, an indication that it has no dependencies.
/// </summary>
public class MarkdownCompletenessPropertyTests
{
    private readonly MarkdownGenerator _generator = new();

    /// <summary>
    /// Provides the custom Arbitrary for RoadmapModel to FsCheck.
    /// </summary>
    public static class Arbitraries
    {
        public static Arbitrary<RoadmapModel> RoadmapModelArbitrary() =>
            ArbitraryGraphs.ArbRoadmapModel();
    }

    /// <summary>
    /// **Validates: Requirements 3.2, 3.3, 3.4, 3.5**
    ///
    /// For any valid RoadmapModel, the generated Markdown output contains:
    /// (a) the heading "# Dependency Documentation",
    /// (b) a "## {label}" section for every activity,
    /// (c) for each activity with dependencies, each dependency label appears in the output,
    /// (d) for each activity without dependencies, "No dependencies" appears in the output.
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(Arbitraries) })]
    public bool MarkdownOutputContainsAllRequiredSections(RoadmapModel model)
    {
        var markdown = _generator.Generate(model);

        // (a) Output contains the main heading
        if (!markdown.Contains("# Dependency Documentation"))
            return false;

        // Parse the markdown into per-activity sections for verification.
        var sections = ParseSections(markdown);

        // (b) There must be exactly as many sections as there are activities
        if (sections.Count != model.Activities.Count)
            return false;

        // (b) Every activity's label must appear as a section heading
        foreach (var activity in model.Activities)
        {
            if (!markdown.Contains($"## {activity.Label}"))
                return false;
        }

        // (c) For each activity with dependencies, each dependency label
        // must appear in the markdown output
        foreach (var activity in model.Activities)
        {
            if (activity.DependencyLabels.Count > 0)
            {
                foreach (var depLabel in activity.DependencyLabels)
                {
                    if (!markdown.Contains(depLabel))
                        return false;
                }
            }
        }

        // (d) For each activity without dependencies, verify "No dependencies" appears
        // in the section for that activity
        foreach (var activity in model.Activities)
        {
            if (activity.DependencyLabels.Count == 0)
            {
                var matchingSections = sections.Where(s => s.Label == activity.Label).ToList();
                if (!matchingSections.Any(s => s.Content.Contains("No dependencies")))
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Parses markdown output into ordered sections split by "## " headings.
    /// </summary>
    private static List<(string Label, string Content)> ParseSections(string markdown)
    {
        var sections = new List<(string Label, string Content)>();
        var lines = markdown.Split('\n');
        string? currentLabel = null;
        var currentContent = new List<string>();

        foreach (var line in lines)
        {
            var trimmed = line.TrimEnd('\r');
            if (trimmed.StartsWith("## "))
            {
                if (currentLabel != null)
                {
                    sections.Add((currentLabel, string.Join("\n", currentContent)));
                }
                currentLabel = trimmed.Substring(3);
                currentContent.Clear();
            }
            else if (currentLabel != null)
            {
                currentContent.Add(trimmed);
            }
        }

        if (currentLabel != null)
        {
            sections.Add((currentLabel, string.Join("\n", currentContent)));
        }

        return sections;
    }
}
