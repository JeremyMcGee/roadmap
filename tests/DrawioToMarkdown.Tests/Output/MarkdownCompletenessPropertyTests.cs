// Feature: drawio-to-markdown, Property 4: Markdown Output Completeness
using DrawioToMarkdown.Graph;
using DrawioToMarkdown.Output;
using DrawioToMarkdown.Tests.Generators;
using FsCheck;
using FsCheck.Xunit;

namespace DrawioToMarkdown.Tests.Output;

/// <summary>
/// Property 4: Markdown Output Completeness
/// Validates: Requirements 3.2, 3.3, 3.4, 3.5
///
/// For any valid DependencyGraph, the generated Markdown string SHALL contain:
/// (a) the heading "# Dependency Documentation",
/// (b) a "## {label}" section for every node in the graph,
/// (c) for each node with antecedents, a "Depends on" sub-section listing each prerequisite node's label,
/// (d) for each node without antecedents, an indication that it has no dependencies.
/// </summary>
public class MarkdownCompletenessPropertyTests
{
    private readonly MarkdownGenerator _generator = new();

    /// <summary>
    /// Provides the custom Arbitrary for DependencyGraph to FsCheck.
    /// </summary>
    public static class Arbitraries
    {
        public static Arbitrary<DependencyGraph> DependencyGraphArbitrary() =>
            ArbitraryGraphs.ArbDependencyGraph();
    }

    /// <summary>
    /// **Validates: Requirements 3.2, 3.3, 3.4, 3.5**
    ///
    /// For any valid DependencyGraph, the generated Markdown output contains:
    /// (a) the heading "# Dependency Documentation",
    /// (b) a "## {label}" section for every node,
    /// (c) for each node with dependencies, each prerequisite node's label appears in the output,
    /// (d) for each node without dependencies, "No dependencies" appears in the output.
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(Arbitraries) })]
    public bool MarkdownOutputContainsAllRequiredSections(DependencyGraph graph)
    {
        var markdown = _generator.Generate(graph);

        // (a) Output contains the main heading
        if (!markdown.Contains("# Dependency Documentation"))
            return false;

        // Parse the markdown into per-node sections for verification.
        var sections = ParseSections(markdown);

        // (b) There must be exactly as many sections as there are nodes
        if (sections.Count != graph.Nodes.Count)
            return false;

        // (b) Every node's label must appear as a section heading
        foreach (var node in graph.Nodes.Values)
        {
            if (!markdown.Contains($"## {node.Label}"))
                return false;
        }

        // (c) For each node with dependencies, each prerequisite node's label
        // must appear in the markdown output
        foreach (var node in graph.Nodes.Values)
        {
            if (graph.Dependencies.TryGetValue(node.Id, out var depIds) && depIds.Count > 0)
            {
                foreach (var depId in depIds)
                {
                    var depLabel = graph.Nodes[depId].Label;
                    if (!markdown.Contains(depLabel))
                        return false;
                }
            }
        }

        // (d) For each node without dependencies, verify "No dependencies" appears
        // in at least one section with that node's label
        foreach (var node in graph.Nodes.Values)
        {
            var hasDeps = graph.Dependencies.TryGetValue(node.Id, out var deps) && deps.Count > 0;
            if (!hasDeps)
            {
                // Find all sections with this node's label
                var matchingSections = sections.Where(s => s.Label == node.Label).ToList();
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
