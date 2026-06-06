// Feature: drawio-to-roadmap-markdown, Property 4: Markdown round-trip preserves model
using DrawioToMarkdown.Model;
using DrawioToMarkdown.Output;
using DrawioToMarkdown.Tests.Generators;
using FsCheck;
using FsCheck.Xunit;
using MtdParser = MarkdownToDrawio.Parsing.MarkdownParser;

namespace DrawioToMarkdown.Tests.Output;

/// <summary>
/// Property 4: Markdown round-trip preserves model
///
/// For any valid RoadmapModel containing 1–500 activities where each activity has a non-empty
/// label of at most 200 characters, a non-empty quarter, a non-empty category, and 0–50
/// dependency labels, generating markdown via the MarkdownGenerator and then parsing that
/// markdown with MarkdownToDrawio's MarkdownParser SHALL yield a model with the same activity
/// labels, quarter values, category values, and dependency label sets.
///
/// **Validates: Requirements 6.4, 4.2, 4.3, 4.4, 4.6, 4.7**
/// </summary>
public class MarkdownRoundTripPropertyTests
{
    private readonly MarkdownGenerator _generator = new();
    private readonly MtdParser _parser = new();

    public static class Arbitraries
    {
        public static Arbitrary<RoadmapModel> RoadmapModelArbitrary() =>
            RoundTripGenerators.ArbRoundTripRoadmapModel();
    }

    /// <summary>
    /// **Validates: Requirements 6.4, 4.2, 4.3, 4.4, 4.6, 4.7**
    ///
    /// For any valid RoadmapModel, generating markdown then parsing it back produces a model
    /// with the same activity labels, quarter values, category values, and dependency label sets.
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(Arbitraries) })]
    public bool MarkdownRoundTripPreservesModel(RoadmapModel original)
    {
        // Step 1: Generate markdown from our model
        var markdown = _generator.Generate(original);

        // Step 2: Parse that markdown with MarkdownToDrawio's parser
        var parsed = _parser.Parse(markdown);

        // Step 3: Compare - same number of activities
        if (parsed.Activities.Count != original.Activities.Count)
            return false;

        // Build lookup from original model (label -> activity)
        var originalByLabel = original.Activities.ToDictionary(a => a.Label);

        // Step 4: For each parsed activity, verify it matches the original
        foreach (var parsedActivity in parsed.Activities)
        {
            // Same set of activity labels (case-sensitive)
            if (!originalByLabel.TryGetValue(parsedActivity.Label, out var originalActivity))
                return false;

            // Same quarter per activity (case-sensitive)
            if (parsedActivity.Quarter != originalActivity.Quarter)
                return false;

            // Same category per activity (case-sensitive)
            if (parsedActivity.Category != originalActivity.Category)
                return false;

            // Same dependency label sets (unordered)
            var originalDeps = originalActivity.DependencyLabels.OrderBy(d => d, StringComparer.Ordinal).ToList();
            var parsedDeps = parsedActivity.DependencyLabels.OrderBy(d => d, StringComparer.Ordinal).ToList();

            if (originalDeps.Count != parsedDeps.Count)
                return false;

            for (var i = 0; i < originalDeps.Count; i++)
            {
                if (originalDeps[i] != parsedDeps[i])
                    return false;
            }
        }

        return true;
    }
}
