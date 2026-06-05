using System.Text.RegularExpressions;
using MarkdownToDrawio.Model;

namespace MarkdownToDrawio.Parsing;

/// <summary>
/// Parses Markdown text into a RoadmapModel by extracting activities
/// from level-2 headings and their metadata from level-3 sub-sections.
/// </summary>
public sealed partial class MarkdownParser : IMarkdownParser
{
    [GeneratedRegex(@"^\-\s+\[(.+?)\]\(.*?\)\s*$")]
    private static partial Regex BulletLinkRegex();

    public RoadmapModel Parse(string markdownContent)
    {
        var activities = new List<Activity>();
        var lines = markdownContent.Split('\n');

        string? currentActivityLabel = null;
        string? currentSection = null;
        string? quarter = null;
        string? category = null;
        List<string> dependencyLabels = new();

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');

            if (line.StartsWith("## ") && !line.StartsWith("### "))
            {
                // Save previous activity if any
                if (currentActivityLabel is not null)
                {
                    activities.Add(new Activity(currentActivityLabel, quarter, category, dependencyLabels));
                }

                // Start new activity
                currentActivityLabel = line[3..].Trim();
                currentSection = null;
                quarter = null;
                category = null;
                dependencyLabels = new List<string>();
            }
            else if (line.StartsWith("### ") && currentActivityLabel is not null)
            {
                var sectionName = line[4..].Trim();
                currentSection = sectionName;
            }
            else if (currentActivityLabel is not null && currentSection is not null)
            {
                switch (currentSection)
                {
                    case "Depends on":
                        ParseDependsOnLine(line, dependencyLabels);
                        break;
                    case "Quarter":
                        if (quarter is null && !string.IsNullOrWhiteSpace(line))
                        {
                            quarter = line.Trim();
                        }
                        break;
                    case "Category":
                        if (category is null && !string.IsNullOrWhiteSpace(line))
                        {
                            category = line.Trim();
                        }
                        break;
                }
            }
        }

        // Save last activity
        if (currentActivityLabel is not null)
        {
            activities.Add(new Activity(currentActivityLabel, quarter, category, dependencyLabels));
        }

        return new RoadmapModel(activities);
    }

    private static void ParseDependsOnLine(string line, List<string> dependencyLabels)
    {
        var trimmed = line.Trim();

        if (string.IsNullOrEmpty(trimmed) || trimmed == "No dependencies")
        {
            return;
        }

        var match = BulletLinkRegex().Match(trimmed);
        if (match.Success)
        {
            dependencyLabels.Add(match.Groups[1].Value);
        }
    }
}
