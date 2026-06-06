// Feature: drawio-to-roadmap-markdown, Property 6: Markdown output byte-for-byte compatibility
using DrawioToMarkdown.Model;
using DrawioToMarkdown.Output;
using DrawioToMarkdown.Tests.Generators;
using FsCheck;
using FsCheck.Xunit;
using MarkdownToDrawio.Parsing;
using MtdModel = MarkdownToDrawio.Model;

namespace DrawioToMarkdown.Tests.Output;

/// <summary>
/// Property 6: Markdown output byte-for-byte compatibility
///
/// For any valid RoadmapModel, the markdown produced by our MarkdownGenerator SHALL be
/// byte-for-byte identical to the output that MarkdownToDrawio's markdown pretty-printer
/// would produce for the same model (same labels, quarters, categories, dependency sets).
///
/// **Validates: Requirements 6.3**
/// </summary>
public class MarkdownByteForBytePropertyTests
{
    private readonly MarkdownGenerator _ourGenerator = new();
    private readonly MarkdownPrettyPrinter _referenceGenerator = new();

    public static class Arbitraries
    {
        public static Arbitrary<RoadmapModel> RoadmapModelArbitrary() =>
            RoundTripGenerators.ArbRoundTripRoadmapModel();
    }

    /// <summary>
    /// **Validates: Requirements 6.3**
    ///
    /// For any valid RoadmapModel, the markdown produced by our MarkdownGenerator is
    /// byte-for-byte identical to the output of MarkdownToDrawio's MarkdownPrettyPrinter.Print()
    /// for the equivalent model.
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(Arbitraries) })]
    public bool MarkdownOutputIsByteForByteIdenticalToReferencePrinter(RoadmapModel original)
    {
        // Step 1: Generate markdown via our DrawioToMarkdown.Output.MarkdownGenerator
        var ourMarkdown = _ourGenerator.Generate(original);

        // Step 2: Convert our model to MarkdownToDrawio.Model.RoadmapModel
        var mtdActivities = original.Activities.Select(a =>
            new MtdModel.Activity(
                a.Label,
                a.Quarter,
                a.Category,
                a.DependencyLabels.ToList()
            )).ToList();

        var mtdModel = new MtdModel.RoadmapModel(mtdActivities);

        // Step 3: Generate markdown via MarkdownToDrawio's MarkdownPrettyPrinter.Print()
        var referenceMarkdown = _referenceGenerator.Print(mtdModel);

        // Step 4: Assert byte-for-byte equality (ordinal string comparison)
        return string.Equals(ourMarkdown, referenceMarkdown, StringComparison.Ordinal);
    }
}
