// Feature: markdown-to-drawio, Property 1: Parse/Print Round-Trip
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using MarkdownToDrawio.Model;
using MarkdownToDrawio.Parsing;
using MarkdownToDrawio.Tests.Generators;

namespace MarkdownToDrawio.Tests.Parsing;

/// <summary>
/// Property-based test verifying that Parse(Print(model)) produces an equivalent model.
/// **Validates: Requirements 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 5.1, 5.2, 5.3, 5.4, 5.5**
/// </summary>
public class RoundTripPropertyTests
{
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(RoundTripArbitrary) })]
    public bool RoundTrip_ParsePrint_ProducesEquivalentModel(RoadmapModel model)
    {
        // Pretty-print the model to Markdown
        var printer = new MarkdownPrettyPrinter();
        var markdown = printer.Print(model);

        // Parse the Markdown back into a model
        var parser = new MarkdownParser();
        var parsedModel = parser.Parse(markdown);

        // Assert equivalence: same set of activity labels
        var originalLabels = model.Activities.Select(a => a.Label).OrderBy(l => l).ToList();
        var parsedLabels = parsedModel.Activities.Select(a => a.Label).OrderBy(l => l).ToList();

        if (!originalLabels.SequenceEqual(parsedLabels))
            return false;

        // For each activity, verify quarter, category, and dependencies match
        var originalByLabel = model.Activities.ToDictionary(a => a.Label);
        var parsedByLabel = parsedModel.Activities.ToDictionary(a => a.Label);

        foreach (var label in originalLabels)
        {
            var original = originalByLabel[label];
            var parsed = parsedByLabel[label];

            if (original.Quarter != parsed.Quarter)
                return false;

            if (original.Category != parsed.Category)
                return false;

            var originalDeps = original.DependencyLabels.OrderBy(d => d).ToList();
            var parsedDeps = parsed.DependencyLabels.OrderBy(d => d).ToList();

            if (!originalDeps.SequenceEqual(parsedDeps))
                return false;
        }

        return true;
    }
}

public class RoundTripArbitrary
{
    public static Arbitrary<RoadmapModel> RoadmapModel() =>
        ArbitraryRoadmaps.GenValidRoadmapModel().ToArbitrary();
}
