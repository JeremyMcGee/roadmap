// Feature: markdown-to-drawio, Property 3: Validation Detects All Unresolved Dependencies
using FsCheck;
using FsCheck.Xunit;
using FsCheck.Fluent;
using MarkdownToDrawio.Model;
using MarkdownToDrawio.Tests.Generators;
using MarkdownToDrawio.Validation;
using Gen = FsCheck.Fluent.Gen;

namespace MarkdownToDrawio.Tests.Validation;

/// <summary>
/// Property-based test verifying that the validator detects all unresolved dependency references.
/// **Validates: Requirements 2.5, 2.6**
/// </summary>
public class DependencyValidationPropertyTests
{
    public static class Arbitraries
    {
        public static Arbitrary<RoadmapModel> RoadmapModelArbitrary() =>
            GenModelWithUnresolvedDependencies().ToArbitrary();
    }

    /// <summary>
    /// Generates a RoadmapModel where all activities have valid Quarter and Category,
    /// but some activities have dependency labels referencing non-existent activities.
    /// </summary>
    private static Gen<RoadmapModel> GenModelWithUnresolvedDependencies()
    {
        return Gen.Choose(2, 15).SelectMany(activityCount =>
        {
            // Generate unique labels
            var labelGens = Enumerable.Range(0, activityCount)
                .Select(i => Gen.Choose(1, 15).SelectMany(length =>
                    Gen.Elements("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789".ToCharArray())
                        .ArrayOf(length)
                        .Select(chars => $"{new string(chars)}{i}")));

            return Gen.CollectToArray(labelGens).SelectMany(labels =>
            {
                // Generate non-existent labels that are guaranteed not to match any real label
                var nonExistentGen = Gen.Choose(1, 10).SelectMany(length =>
                    Gen.Elements("abcdefghijklmnopqrstuvwxyz".ToCharArray())
                        .ArrayOf(length)
                        .Select(chars => $"FAKE_{new string(chars)}"));

                // For each activity, generate valid metadata but potentially invalid dependencies
                var activityGens = labels.Select((label, index) =>
                {
                    // Valid quarter
                    var quarterGen = Gen.Choose(1, 4).SelectMany(q =>
                        Gen.Choose(2020, 2030).Select(year => $"Q{q} {year}"));

                    // Valid category
                    var categoryGen = Gen.Choose(1, 10).SelectMany(length =>
                        Gen.Elements("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray())
                            .ArrayOf(length)
                            .Select(chars => new string(chars)));

                    // Dependencies: mix of valid and non-existent references
                    var validDeps = labels.Where((_, idx) => idx != index).ToArray();

                    // Decide how many fake deps to add (at least 1 for some activities)
                    Gen<IReadOnlyList<string>> depsGen;
                    if (index % 2 == 0)
                    {
                        // This activity gets at least one unresolved dependency
                        var fakeDepsCount = Gen.Choose(1, 3);
                        depsGen = fakeDepsCount.SelectMany(fakeCount =>
                            nonExistentGen.ArrayOf(fakeCount).SelectMany(fakes =>
                            {
                                // Optionally add some valid deps too
                                if (validDeps.Length > 0)
                                {
                                    var maxValid = Math.Min(2, validDeps.Length);
                                    return Gen.Choose(0, maxValid).SelectMany(validCount =>
                                        Gen.Shuffle(validDeps).Select(shuffled =>
                                            (IReadOnlyList<string>)fakes
                                                .Concat(shuffled.Take(validCount))
                                                .ToList()));
                                }
                                return Gen.Constant((IReadOnlyList<string>)fakes.ToList());
                            }));
                    }
                    else
                    {
                        // This activity gets only valid deps (or none)
                        if (validDeps.Length > 0)
                        {
                            var maxDeps = Math.Min(2, validDeps.Length);
                            depsGen = Gen.Choose(0, maxDeps).SelectMany(depCount =>
                                Gen.Shuffle(validDeps).Select(shuffled =>
                                    (IReadOnlyList<string>)shuffled.Take(depCount).ToList()));
                        }
                        else
                        {
                            depsGen = Gen.Constant<IReadOnlyList<string>>(Array.Empty<string>());
                        }
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

    [Property(MaxTest = 100, Arbitrary = new[] { typeof(Arbitraries) })]
    public bool Validation_Detects_All_Unresolved_Dependencies(RoadmapModel model)
    {
        // Compute expected unresolved dependency count
        var allLabels = new HashSet<string>(
            model.Activities.Select(a => a.Label),
            StringComparer.Ordinal);

        var expectedUnresolved = new List<(string ActivityLabel, string TargetLabel)>();
        foreach (var activity in model.Activities)
        {
            foreach (var dep in activity.DependencyLabels)
            {
                if (!allLabels.Contains(dep))
                {
                    expectedUnresolved.Add((activity.Label, dep));
                }
            }
        }

        // Run validation
        var validator = new RoadmapValidator();
        var errors = validator.Validate(model);

        // Filter to dependency-related errors (containing "depends on" and "does not exist")
        var dependencyErrors = errors
            .Where(e => e.Message.Contains("depends on") && e.Message.Contains("does not exist"))
            .ToList();

        // Assert count matches
        if (dependencyErrors.Count != expectedUnresolved.Count)
            return false;

        // Assert each error identifies both the referring activity and the unresolved target
        foreach (var (activityLabel, targetLabel) in expectedUnresolved)
        {
            var matchingError = dependencyErrors.Any(e =>
                e.Message.Contains(activityLabel) &&
                e.Message.Contains(targetLabel));

            if (!matchingError)
                return false;
        }

        return true;
    }
}
