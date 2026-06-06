// Feature: drawio-to-markdown, Property 1: Parse/Print Round-Trip
// Feature: drawio-to-roadmap-markdown, Property 3: XML round-trip preserves model
using DrawioToMarkdown.Graph;
using DrawioToMarkdown.Model;
using DrawioToMarkdown.Output;
using DrawioToMarkdown.Parsing;
using DrawioToMarkdown.Resolution;
using DrawioToMarkdown.Tests.Generators;
using FsCheck;
using FsCheck.Xunit;

namespace DrawioToMarkdown.Tests.Parsing;

/// <summary>
/// Property 1: Parse/Print Round-Trip (Legacy)
/// Validates: Requirements 1.1, 1.2, 1.3, 2.1, 2.2, 5.1, 5.2
///
/// For any valid RoadmapModel, pretty-printing the model to draw.io XML
/// and then parsing that XML back produces a diagram containing the same activity labels.
///
/// Property 3: XML round-trip preserves model
/// Validates: Requirements 6.1, 1.2, 1.3, 1.4, 1.5, 3.1, 3.3, 3.4
///
/// For any valid RoadmapModel, printing to draw.io XML and parsing back via
/// DrawioParser + PositionResolver produces a RoadmapModel with the same activity labels,
/// categories, quarters, and dependency label sets.
/// </summary>
public class RoundTripPropertyTests
{
    private readonly XmlPrettyPrinter _printer = new();
    private readonly DrawioParser _parser = new();
    private readonly PositionResolver _resolver = new();

    /// <summary>
    /// Provides the custom Arbitrary for RoadmapModel to FsCheck.
    /// </summary>
    public static class Arbitraries
    {
        public static Arbitrary<RoadmapModel> RoadmapModelArbitrary() =>
            ArbitraryGraphs.ArbRoadmapModel();
    }

    /// <summary>
    /// **Validates: Requirements 1.1, 1.2, 1.3, 2.1, 2.2, 5.1, 5.2**
    ///
    /// Pretty-printing a RoadmapModel to draw.io XML and parsing it back produces
    /// a diagram containing the same set of activity labels.
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(Arbitraries) })]
    public bool ParsePrintRoundTripPreservesActivityLabels(RoadmapModel original)
    {
        // Step 1: Pretty-print the model to XML
        var xml = _printer.Print(original);

        // Step 2: Parse the XML back into a ParsedDiagram
        var parsed = _parser.Parse(xml);

        // Step 3: Compare - same set of activity labels
        var originalLabels = original.Activities.Select(a => a.Label).OrderBy(l => l).ToList();
        var parsedLabels = parsed.ActivityNodes.Select(n => n.Label).OrderBy(l => l).ToList();

        return originalLabels.SequenceEqual(parsedLabels);
    }

    /// <summary>
    /// **Validates: Requirements 6.1, 1.2, 1.3, 1.4, 1.5, 3.1, 3.3, 3.4**
    ///
    /// For any valid RoadmapModel containing 1–500 activities with non-empty labels,
    /// quarters, categories, and 0–50 dependency labels each, printing the model to
    /// draw.io XML via the XmlPrettyPrinter and then parsing that XML back via the
    /// DrawioParser and PositionResolver SHALL produce a RoadmapModel with the same
    /// set of activity labels (case-sensitive), the same category per activity, the same
    /// quarter per activity, and the same dependency label sets per activity.
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(Arbitraries) })]
    // Feature: drawio-to-roadmap-markdown, Property 3: XML round-trip preserves model
    public bool XmlRoundTripPreservesModel(RoadmapModel original)
    {
        // Step 1: Print the model to draw.io XML
        var xml = _printer.Print(original);

        // Step 2: Parse the XML back into a ParsedDiagram
        var parsed = _parser.Parse(xml);

        // Step 3: Resolve positions to produce a RoadmapModel
        var roundTripped = _resolver.Resolve(parsed);

        // Step 4: Verify same set of activity labels (case-sensitive)
        var originalByLabel = original.Activities
            .ToDictionary(a => a.Label, StringComparer.Ordinal);
        var roundTrippedByLabel = roundTripped.Activities
            .ToDictionary(a => a.Label, StringComparer.Ordinal);

        if (originalByLabel.Count != roundTrippedByLabel.Count)
            return false;

        foreach (var (label, originalActivity) in originalByLabel)
        {
            if (!roundTrippedByLabel.TryGetValue(label, out var rtActivity))
                return false;

            // Same category per activity (case-sensitive)
            if (!string.Equals(originalActivity.Category, rtActivity.Category, StringComparison.Ordinal))
                return false;

            // Same quarter per activity (case-sensitive)
            if (!string.Equals(originalActivity.Quarter, rtActivity.Quarter, StringComparison.Ordinal))
                return false;

            // Same dependency label sets per activity (unordered, deduplicated)
            var originalDeps = originalActivity.DependencyLabels
                .Distinct(StringComparer.Ordinal)
                .OrderBy(d => d, StringComparer.Ordinal).ToList();
            var rtDeps = rtActivity.DependencyLabels
                .OrderBy(d => d, StringComparer.Ordinal).ToList();

            if (!originalDeps.SequenceEqual(rtDeps, StringComparer.Ordinal))
                return false;
        }

        return true;
    }
}
