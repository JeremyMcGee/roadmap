using FsCheck;
using FsCheck.Fluent;
using MarkdownToDrawio.Model;
using Gen = FsCheck.Fluent.Gen;

namespace MarkdownToDrawio.Tests.Generators;

/// <summary>
/// FsCheck Arbitrary generators for RoadmapModel domain types used in property-based tests.
/// </summary>
public static class ArbitraryRoadmaps
{
    /// <summary>
    /// Characters allowed in generated labels and category strings.
    /// Excludes special markdown characters (#, -, [, ]).
    /// </summary>
    private static readonly char[] AlphanumericChars =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789".ToCharArray();

    /// <summary>
    /// Generates a non-empty alphanumeric string of length 1–15.
    /// </summary>
    private static Gen<string> GenAlphanumericString()
    {
        return Gen.Choose(1, 15).SelectMany(length =>
            Gen.Elements(AlphanumericChars).ArrayOf(length).Select(chars => new string(chars)));
    }

    /// <summary>
    /// Generates a quarter string in "Q{1-4} {2020-2030}" format.
    /// </summary>
    private static Gen<string> GenQuarter()
    {
        return Gen.Choose(1, 4).SelectMany(q =>
            Gen.Choose(2020, 2030).Select(year => $"Q{q} {year}"));
    }

    /// <summary>
    /// Generates a non-empty alphanumeric category string.
    /// </summary>
    private static Gen<string> GenCategory()
    {
        return GenAlphanumericString();
    }

    /// <summary>
    /// Generator for a valid RoadmapModel with:
    /// - 1–20 activities with unique non-empty alphanumeric labels
    /// - Quarter values in "Q{1-4} {2020-2030}" format
    /// - Non-empty alphanumeric category strings
    /// - Dependency labels referencing only existing activity labels (no self-references)
    /// </summary>
    public static Gen<RoadmapModel> GenValidRoadmapModel()
    {
        return Gen.Choose(1, 20).SelectMany(activityCount =>
        {
            // Generate unique labels using index prefix to guarantee uniqueness
            var labelGens = Enumerable.Range(0, activityCount)
                .Select(i => GenAlphanumericString().Select(s => $"{s}{i}"));

            return Gen.CollectToArray(labelGens).SelectMany(labels =>
            {
                // Generate activities with valid metadata
                var activityGens = labels.Select((label, index) =>
                {
                    // Possible dependency targets: all labels except this activity's own label
                    var possibleDeps = labels.Where((_, idx) => idx != index).ToArray();

                    Gen<IReadOnlyList<string>> depsGen;
                    if (possibleDeps.Length == 0)
                    {
                        depsGen = Gen.Constant<IReadOnlyList<string>>(Array.Empty<string>());
                    }
                    else
                    {
                        // Pick a random subset of possible dependencies (0 to min(3, available))
                        var maxDeps = Math.Min(3, possibleDeps.Length);
                        depsGen = Gen.Choose(0, maxDeps).SelectMany(depCount =>
                            Gen.Shuffle(possibleDeps).Select(shuffled =>
                                (IReadOnlyList<string>)shuffled.Take(depCount).ToList()));
                    }

                    return GenQuarter().SelectMany(quarter =>
                        GenCategory().SelectMany(category =>
                            depsGen.Select(deps =>
                                new Activity(label, quarter, category, deps))));
                });

                return Gen.CollectToArray(activityGens).Select(activities =>
                    new RoadmapModel(activities.ToList()));
            });
        });
    }

    /// <summary>
    /// Generator for an invalid RoadmapModel with:
    /// - Some activities having null Quarter or Category
    /// - Some activities having whitespace-only Quarter or Category
    /// - Some dependency labels referencing non-existent activity names
    /// </summary>
    public static Gen<RoadmapModel> GenInvalidRoadmapModel()
    {
        return Gen.Choose(3, 20).SelectMany(activityCount =>
        {
            // Generate unique labels
            var labelGens = Enumerable.Range(0, activityCount)
                .Select(i => GenAlphanumericString().Select(s => $"{s}{i}"));

            return Gen.CollectToArray(labelGens).SelectMany(labels =>
            {
                // Generate activities, introducing invalid metadata for some
                var activityGens = labels.Select((label, index) =>
                {
                    // Quarter: valid, null, or whitespace
                    Gen<string?> quarterGen;
                    if (index % 3 == 0)
                    {
                        quarterGen = Gen.Constant<string?>(null);
                    }
                    else if (index % 3 == 1)
                    {
                        quarterGen = Gen.Constant<string?>(" ");
                    }
                    else
                    {
                        quarterGen = GenQuarter().Select<string, string?>(q => q);
                    }

                    // Category: valid, null, or whitespace
                    Gen<string?> categoryGen;
                    if (index % 4 == 0)
                    {
                        categoryGen = Gen.Constant<string?>(null);
                    }
                    else if (index % 4 == 1)
                    {
                        categoryGen = Gen.Constant<string?>(" ");
                    }
                    else
                    {
                        categoryGen = GenCategory().Select<string, string?>(c => c);
                    }

                    // Dependencies: mix of valid references and non-existent labels
                    var possibleDeps = labels.Where((_, idx) => idx != index).ToArray();
                    var nonExistentLabelGen = GenAlphanumericString().Select(s => $"nonexistent_{s}");

                    Gen<IReadOnlyList<string>> depsGen;
                    if (index % 5 == 0)
                    {
                        // Add a non-existent dependency reference
                        depsGen = nonExistentLabelGen.Select(fake =>
                            (IReadOnlyList<string>)new List<string> { fake });
                    }
                    else if (possibleDeps.Length > 0)
                    {
                        var maxDeps = Math.Min(2, possibleDeps.Length);
                        depsGen = Gen.Choose(0, maxDeps).SelectMany(depCount =>
                            Gen.Shuffle(possibleDeps).Select(shuffled =>
                                (IReadOnlyList<string>)shuffled.Take(depCount).ToList()));
                    }
                    else
                    {
                        depsGen = Gen.Constant<IReadOnlyList<string>>(Array.Empty<string>());
                    }

                    return quarterGen.SelectMany(quarter =>
                        categoryGen.SelectMany(category =>
                            depsGen.Select(deps =>
                                new Activity(label, quarter, category, deps))));
                });

                return Gen.CollectToArray(activityGens).Select(activities =>
                    new RoadmapModel(activities.ToList()));
            });
        });
    }

    /// <summary>
    /// Generator for file path strings with various extensions.
    /// </summary>
    public static Gen<string> GenFilePath()
    {
        var extensions = new[] { ".drawio", ".xml", ".txt", ".md", ".json", ".csv", ".html", ".yaml" };
        var directorySegments = new[] { "docs", "src", "output", "diagrams", "data", "projects" };
        var fileNameChars = "abcdefghijklmnopqrstuvwxyz0123456789_-".ToCharArray();

        var genFileName = Gen.Choose(1, 30).SelectMany(length =>
            Gen.Elements(fileNameChars).ArrayOf(length).Select(chars => new string(chars)));

        var genExtension = Gen.Elements(extensions);
        var genDirSegment = Gen.Elements(directorySegments);

        return Gen.Choose(0, 4).SelectMany(dirDepth =>
            genDirSegment.ArrayOf(dirDepth).SelectMany(dirs =>
                genFileName.SelectMany(fileName =>
                    genExtension.Select(ext =>
                    {
                        var path = dirs.Length > 0
                            ? string.Join("/", dirs) + "/" + fileName + ext
                            : fileName + ext;
                        return path;
                    }))));
    }

    /// <summary>
    /// Arbitrary instance for valid RoadmapModel.
    /// </summary>
    public static Arbitrary<RoadmapModel> ArbRoadmapModel() =>
        GenValidRoadmapModel().ToArbitrary();

    /// <summary>
    /// Arbitrary instance for file path strings.
    /// </summary>
    public static Arbitrary<string> ArbFilePath() =>
        GenFilePath().ToArbitrary();
}
