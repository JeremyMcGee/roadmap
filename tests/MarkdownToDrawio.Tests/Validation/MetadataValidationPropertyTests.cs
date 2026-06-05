// Feature: markdown-to-drawio, Property 2: Validation Detects All Metadata Errors
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using MarkdownToDrawio.Model;
using MarkdownToDrawio.Tests.Generators;
using MarkdownToDrawio.Validation;
using Gen = FsCheck.Fluent.Gen;

namespace MarkdownToDrawio.Tests.Validation;

/// <summary>
/// Property-based test verifying that the validator detects all metadata errors
/// (missing or whitespace-only Quarter and Category values).
/// **Validates: Requirements 2.1, 2.2, 2.3, 2.4, 2.6**
/// </summary>
public class MetadataValidationPropertyTests
{
    /// <summary>
    /// Provides the custom Arbitrary for RoadmapModel with known metadata errors.
    /// </summary>
    public static class Arbitraries
    {
        public static Arbitrary<RoadmapModel> RoadmapModelArbitrary() =>
            GenModelWithKnownMetadataErrors().ToArbitrary();
    }

    /// <summary>
    /// Generates a RoadmapModel where a known subset of activities have invalid metadata
    /// (null or whitespace Quarter/Category), ensuring at least one activity has invalid metadata.
    /// </summary>
    private static Gen<RoadmapModel> GenModelWithKnownMetadataErrors()
    {
        var alphanumericChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789".ToCharArray();

        Gen<string> genLabel(int index) =>
            Gen.Choose(1, 12)
                .SelectMany(len => Gen.Elements(alphanumericChars).ArrayOf(len))
                .Select(chars => $"{new string(chars)}_{index}");

        Gen<string> genValidQuarter() =>
            Gen.Choose(1, 4).SelectMany(q =>
                Gen.Choose(2020, 2030).Select(year => $"Q{q} {year}"));

        Gen<string> genValidCategory() =>
            Gen.Choose(1, 10)
                .SelectMany(len => Gen.Elements(alphanumericChars).ArrayOf(len))
                .Select(chars => new string(chars));

        // Invalid quarter: null or whitespace
        Gen<string?> genInvalidQuarter() =>
            Gen.Elements<string?>(null, " ", "  ", "\t");

        // Invalid category: null or whitespace
        Gen<string?> genInvalidCategory() =>
            Gen.Elements<string?>(null, " ", "  ", "\t");

        return Gen.Choose(2, 15).SelectMany(activityCount =>
        {
            // Guarantee at least 1 activity has invalid metadata
            return Gen.Choose(1, Math.Max(1, activityCount - 1)).SelectMany(invalidCount =>
            {
                var labelGens = Enumerable.Range(0, activityCount).Select(i => genLabel(i));

                return Gen.CollectToArray(labelGens).SelectMany(labels =>
                {
                    // First `invalidCount` activities get invalid metadata, rest are valid
                    var activityGens = labels.Select((label, index) =>
                    {
                        bool isInvalid = index < invalidCount;

                        if (isInvalid)
                        {
                            // Each invalid activity: at least one of Quarter or Category is invalid
                            var combinedGen = Gen.Choose(1, 3).SelectMany(mode =>
                            {
                                return mode switch
                                {
                                    1 => // Only quarter invalid
                                        genInvalidQuarter().SelectMany(q =>
                                            genValidCategory().Select<string, string?>(c => c)
                                                .Select(c => (Quarter: q, Category: c))),
                                    2 => // Only category invalid
                                        genValidQuarter().Select<string, string?>(q => q).SelectMany(q =>
                                            genInvalidCategory()
                                                .Select(c => (Quarter: q, Category: c))),
                                    _ => // Both invalid
                                        genInvalidQuarter().SelectMany(q =>
                                            genInvalidCategory()
                                                .Select(c => (Quarter: q, Category: c)))
                                };
                            });

                            return combinedGen.Select(pair =>
                                new Activity(label, pair.Quarter, pair.Category,
                                    (IReadOnlyList<string>)Array.Empty<string>()));
                        }
                        else
                        {
                            // Valid activity
                            return genValidQuarter().SelectMany(q =>
                                genValidCategory().Select(c =>
                                    new Activity(label, q, c,
                                        (IReadOnlyList<string>)Array.Empty<string>())));
                        }
                    });

                    return Gen.CollectToArray(activityGens)
                        .Select(activities => new RoadmapModel(activities.ToList()));
                });
            });
        });
    }

    /// <summary>
    /// For any RoadmapModel where a known subset of activities have missing or whitespace-only
    /// Quarter values and/or missing or whitespace-only Category values, the validator SHALL
    /// return an error for every such activity, and the total number of metadata errors SHALL
    /// equal the number of activities with invalid metadata fields.
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(Arbitraries) })]
    public bool Validation_Detects_All_Metadata_Errors(RoadmapModel model)
    {
        var validator = new RoadmapValidator();
        var errors = validator.Validate(model);

        // Compute expected error count:
        // Count activities with null/whitespace Quarter + count activities with null/whitespace Category
        int expectedQuarterErrors = model.Activities
            .Count(a => a.Quarter is null || string.IsNullOrWhiteSpace(a.Quarter));
        int expectedCategoryErrors = model.Activities
            .Count(a => a.Category is null || string.IsNullOrWhiteSpace(a.Category));
        int expectedMetadataErrorCount = expectedQuarterErrors + expectedCategoryErrors;

        // Filter returned errors to only metadata-related ones
        var metadataErrors = errors.Where(e =>
            e.Message.Contains("missing a Quarter") ||
            e.Message.Contains("missing a Category") ||
            e.Message.Contains("empty Quarter") ||
            e.Message.Contains("empty Category")).ToList();

        // Assert the metadata error count equals expected
        if (metadataErrors.Count != expectedMetadataErrorCount)
            return false;

        // Assert every activity with invalid metadata has a corresponding error
        foreach (var activity in model.Activities)
        {
            bool hasInvalidQuarter = activity.Quarter is null || string.IsNullOrWhiteSpace(activity.Quarter);
            bool hasInvalidCategory = activity.Category is null || string.IsNullOrWhiteSpace(activity.Category);

            if (hasInvalidQuarter)
            {
                bool hasQuarterError = metadataErrors.Any(e =>
                    e.ActivityLabel == activity.Label &&
                    (e.Message.Contains("missing a Quarter") || e.Message.Contains("empty Quarter")));

                if (!hasQuarterError)
                    return false;
            }

            if (hasInvalidCategory)
            {
                bool hasCategoryError = metadataErrors.Any(e =>
                    e.ActivityLabel == activity.Label &&
                    (e.Message.Contains("missing a Category") || e.Message.Contains("empty Category")));

                if (!hasCategoryError)
                    return false;
            }
        }

        return true;
    }
}
