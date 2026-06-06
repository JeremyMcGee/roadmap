// Feature: drawio-to-roadmap-markdown, Property 5: Full pipeline round-trip
using DrawioToMarkdown.Model;
using DrawioToMarkdown.Output;
using DrawioToMarkdown.Parsing;
using DrawioToMarkdown.Resolution;
using DrawioToMarkdown.Tests.Generators;
using FsCheck;
using FsCheck.Xunit;
using MtdModel = MarkdownToDrawio.Model;
using MtdDiagramGenerator = MarkdownToDrawio.Output.DiagramGenerator;
using MtdParser = MarkdownToDrawio.Parsing.MarkdownParser;

namespace DrawioToMarkdown.Tests.Output;

/// <summary>
/// Property 5: Full pipeline round-trip
///
/// For any valid RoadmapModel, generating draw.io XML via MarkdownToDrawio's DiagramGenerator,
/// parsing that XML with our DrawioParser and PositionResolver, then generating markdown and
/// parsing it with MarkdownToDrawio's MarkdownParser SHALL yield a model equivalent on activity
/// labels (case-sensitive), quarter values (case-insensitive), category values (case-insensitive),
/// and dependency sets (unordered, case-insensitive).
///
/// **Validates: Requirements 6.2**
/// </summary>
public class FullPipelineRoundTripPropertyTests
{
    private readonly MtdDiagramGenerator _diagramGenerator = new();
    private readonly DrawioParser _drawioParser = new();
    private readonly PositionResolver _positionResolver = new();
    private readonly MarkdownGenerator _markdownGenerator = new();
    private readonly MtdParser _markdownParser = new();

    public static class Arbitraries
    {
        public static Arbitrary<RoadmapModel> RoadmapModelArbitrary() =>
            RoundTripGenerators.ArbRoundTripRoadmapModel();
    }

    /// <summary>
    /// **Validates: Requirements 6.2**
    ///
    /// For any valid RoadmapModel, the full pipeline round-trip preserves activity labels,
    /// quarter values (case-insensitive), category values (case-insensitive), and dependency
    /// sets (unordered, case-insensitive).
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(Arbitraries) })]
    public bool FullPipelineRoundTripPreservesModel(RoadmapModel original)
    {
        // Step 1: Convert our DrawioToMarkdown model to MarkdownToDrawio model
        var mtdActivities = original.Activities.Select(a =>
            new MtdModel.Activity(
                a.Label,
                a.Quarter,
                a.Category,
                a.DependencyLabels.ToList()
            )).ToList();
        var mtdModel = new MtdModel.RoadmapModel(mtdActivities);

        // Step 2: Generate draw.io XML via MarkdownToDrawio's DiagramGenerator
        var xml = _diagramGenerator.Generate(mtdModel);

        // Step 3: Parse that XML with our DrawioParser
        var parsedDiagram = _drawioParser.Parse(xml);

        // Step 4: Resolve positions with our PositionResolver
        var resolvedModel = _positionResolver.Resolve(parsedDiagram);

        // Step 5: Generate markdown with our MarkdownGenerator
        var markdown = _markdownGenerator.Generate(resolvedModel);

        // Step 6: Parse markdown with MarkdownToDrawio's MarkdownParser
        var finalModel = _markdownParser.Parse(markdown);

        // Step 7: Compare the result to the original model
        if (finalModel.Activities.Count != original.Activities.Count)
            return false;

        // Build lookup from original model (label -> activity) - labels are case-sensitive
        var originalByLabel = original.Activities.ToDictionary(a => a.Label);

        foreach (var finalActivity in finalModel.Activities)
        {
            // Same set of activity labels (case-sensitive)
            if (!originalByLabel.TryGetValue(finalActivity.Label, out var originalActivity))
                return false;

            // Same quarter values (case-INSENSITIVE)
            if (!string.Equals(finalActivity.Quarter, originalActivity.Quarter, StringComparison.OrdinalIgnoreCase))
                return false;

            // Same category values (case-INSENSITIVE)
            if (!string.Equals(finalActivity.Category, originalActivity.Category, StringComparison.OrdinalIgnoreCase))
                return false;

            // Same dependency sets (unordered, case-INSENSITIVE)
            var originalDeps = originalActivity.DependencyLabels
                .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var finalDeps = finalActivity.DependencyLabels
                .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (originalDeps.Count != finalDeps.Count)
                return false;

            for (var i = 0; i < originalDeps.Count; i++)
            {
                if (!string.Equals(originalDeps[i], finalDeps[i], StringComparison.OrdinalIgnoreCase))
                    return false;
            }
        }

        return true;
    }
}
