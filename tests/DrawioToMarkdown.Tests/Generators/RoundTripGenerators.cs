using DrawioToMarkdown.Model;
using DrawioToMarkdown.Parsing;
using FsCheck;
using FsCheck.Fluent;
using Gen = FsCheck.Fluent.Gen;

namespace DrawioToMarkdown.Tests.Generators;

/// <summary>
/// FsCheck generators tailored for round-trip property tests.
/// Generates models with unique labels (alphanumeric + spaces only),
/// quarters in strict "Q{1-4} {2020-2030}" format, and dependencies
/// referencing only labels that exist in the model.
/// </summary>
public static class RoundTripGenerators
{
    /// <summary>
    /// Characters allowed in round-trip labels: alphanumeric plus space.
    /// No special chars that could get mangled by HTML encoding/decoding.
    /// </summary>
    private static readonly char[] LabelChars =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 ".ToCharArray();

    /// <summary>
    /// Alphanumeric-only characters (no spaces) for generating label prefixes that guarantee uniqueness.
    /// </summary>
    private static readonly char[] AlphaChars =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789".ToCharArray();

    /// <summary>
    /// Pool of category names to draw from.
    /// </summary>
    private static readonly string[] CategoryPool = new[]
    {
        "Infrastructure", "Platform", "Security", "Data", "Frontend",
        "Backend", "DevOps", "Analytics", "Mobile", "Testing"
    };

    /// <summary>
    /// All valid quarters in "Q{1-4} {2020-2030}" format.
    /// </summary>
    private static readonly string[] AllQuarters =
        Enumerable.Range(2020, 11)
            .SelectMany(year => Enumerable.Range(1, 4).Select(q => $"Q{q} {year}"))
            .ToArray();

    /// <summary>
    /// Generates a non-empty label string with alphanumeric + space chars, 1-50 chars long.
    /// Ensures no leading/trailing spaces and no consecutive spaces (to survive whitespace normalization).
    /// </summary>
    private static Gen<string> GenRoundTripLabel()
    {
        return Gen.Choose(1, 50).SelectMany(length =>
        {
            if (length == 1)
            {
                // Single char must be alphanumeric (not a space)
                return Gen.Elements(AlphaChars).Select(c => c.ToString());
            }

            // Generate a string where:
            // - First and last chars are alphanumeric (no leading/trailing spaces)
            // - Internal chars can include spaces but not consecutive ones
            return Gen.Elements(AlphaChars).SelectMany(first =>
                Gen.Elements(AlphaChars).SelectMany(last =>
                {
                    if (length <= 2)
                    {
                        return Gen.Constant($"{first}{last}");
                    }

                    var innerLength = length - 2;
                    return GenInnerLabelChars(innerLength).Select(inner =>
                        $"{first}{inner}{last}");
                }));
        });
    }

    /// <summary>
    /// Generates inner label characters ensuring no consecutive spaces.
    /// </summary>
    private static Gen<string> GenInnerLabelChars(int length)
    {
        // Generate a string of the requested length with no consecutive spaces
        return Gen.Elements(LabelChars).ArrayOf(length).Select(chars =>
        {
            // Post-process: replace consecutive spaces with a single space,
            // then trim to avoid trailing spaces (we'll pad with alpha if needed)
            var result = new char[length];
            var writeIdx = 0;
            var lastWasSpace = false;

            for (var i = 0; i < chars.Length && writeIdx < length; i++)
            {
                if (chars[i] == ' ')
                {
                    if (!lastWasSpace)
                    {
                        result[writeIdx++] = ' ';
                        lastWasSpace = true;
                    }
                    // Skip consecutive spaces
                }
                else
                {
                    result[writeIdx++] = chars[i];
                    lastWasSpace = false;
                }
            }

            // Fill remaining positions with alphanumeric if we ran short
            while (writeIdx < length)
            {
                result[writeIdx++] = 'x';
            }

            return new string(result, 0, writeIdx);
        });
    }

    /// <summary>
    /// Generates a valid quarter string in "Q{1-4} {2020-2030}" format.
    /// </summary>
    public static Gen<string> GenQuarter()
    {
        return Gen.Elements(AllQuarters);
    }

    /// <summary>
    /// Generates a category from the category pool.
    /// </summary>
    public static Gen<string> GenCategory()
    {
        return Gen.Elements(CategoryPool);
    }

    /// <summary>
    /// Generates a RoadmapModel suitable for round-trip testing:
    /// - 1-20 activities (scaled down from 500 for test speed)
    /// - Unique labels (alphanumeric + space chars only, 1-50 chars)
    /// - Quarters in Q{1-4} {2020-2030} format
    /// - Categories from pool
    /// - 0-5 dependency labels referencing other labels in the model
    ///
    /// Validates: Requirements 6.1, 6.2, 6.4
    /// </summary>
    public static Gen<RoadmapModel> GenRoundTripRoadmapModel()
    {
        return Gen.Choose(1, 20).SelectMany(activityCount =>
        {
            // Generate unique labels by appending index suffix
            var labelGens = Enumerable.Range(0, activityCount)
                .Select(i => GenRoundTripLabel().Select(baseLabel => $"{baseLabel}{i}"));

            return Gen.CollectToArray(labelGens).SelectMany(labels =>
            {
                var activityGens = labels.Select(label =>
                    GenQuarter().SelectMany(quarter =>
                        GenCategory().SelectMany(category =>
                        {
                            var otherLabels = labels.Where(l => l != label).ToArray();
                            if (otherLabels.Length == 0)
                            {
                                return Gen.Constant(new RoadmapActivity(
                                    label, quarter, category, Array.Empty<string>()));
                            }

                            var maxDeps = Math.Min(5, otherLabels.Length);
                            return Gen.Choose(0, maxDeps).SelectMany(depCount =>
                            {
                                if (depCount == 0)
                                {
                                    return Gen.Constant(new RoadmapActivity(
                                        label, quarter, category, Array.Empty<string>()));
                                }

                                // Pick depCount unique labels from otherLabels
                                return GenUniqueSubset(otherLabels, depCount).Select(deps =>
                                    new RoadmapActivity(label, quarter, category, deps));
                            });
                        })));

                return Gen.CollectToArray(activityGens).Select(activities =>
                    new RoadmapModel(activities.ToList()));
            });
        });
    }

    /// <summary>
    /// Picks a subset of exactly 'count' unique elements from the source array.
    /// Uses Fisher-Yates shuffle approach.
    /// </summary>
    private static Gen<string[]> GenUniqueSubset(string[] source, int count)
    {
        if (count >= source.Length)
        {
            return Gen.Constant(source.ToArray());
        }

        // Shuffle and take the first 'count' elements
        return Gen.Shuffle(source).Select(shuffled => shuffled.Take(count).ToArray());
    }

    /// <summary>
    /// Generates a list of non-overlapping swimlanes (vertical bands).
    /// Each swimlane has a unique category label and non-overlapping Y ranges.
    /// </summary>
    /// <param name="count">Number of swimlanes to generate.</param>
    public static Gen<SwimlaneDef[]> GenNonOverlappingSwimlanes(int count)
    {
        if (count <= 0)
        {
            return Gen.Constant(Array.Empty<SwimlaneDef>());
        }

        // Generate swimlane heights (40-200 each) and gaps (0-20 between them)
        var heightGen = Gen.Choose(40, 200).Select(h => (double)h);
        var gapGen = Gen.Choose(0, 20).Select(g => (double)g);

        return heightGen.ArrayOf(count).SelectMany(heights =>
            gapGen.ArrayOf(count).SelectMany(gaps =>
                Gen.Choose(0, 100).SelectMany(startY =>
                    Gen.Choose(0, 500).Select(startX =>
                    {
                        var swimlanes = new SwimlaneDef[count];
                        var currentY = (double)startY;
                        var categories = CategoryPool.Take(count).ToArray();

                        for (var i = 0; i < count; i++)
                        {
                            var category = i < categories.Length
                                ? categories[i]
                                : $"Category{i}";

                            swimlanes[i] = new SwimlaneDef(
                                Id: $"swim_{i}",
                                Label: category,
                                X: startX,
                                Y: currentY,
                                Width: 800.0,
                                Height: heights[i]);

                            currentY += heights[i] + gaps[i];
                        }

                        return swimlanes;
                    }))));
    }

    /// <summary>
    /// Generates a list of non-overlapping quarter columns (horizontal bands).
    /// Each column has a quarter label in "Q{1-4} {2020-2030}" format and non-overlapping X ranges.
    /// </summary>
    /// <param name="count">Number of quarter columns to generate.</param>
    public static Gen<QuarterColumnDef[]> GenNonOverlappingQuarterColumns(int count)
    {
        if (count <= 0)
        {
            return Gen.Constant(Array.Empty<QuarterColumnDef>());
        }

        // Generate column widths (100-300 each) and gaps (0-20 between them)
        var widthGen = Gen.Choose(100, 300).Select(w => (double)w);
        var gapGen = Gen.Choose(0, 20).Select(g => (double)g);

        return widthGen.ArrayOf(count).SelectMany(widths =>
            gapGen.ArrayOf(count).SelectMany(gaps =>
                Gen.Choose(0, 200).Select(startX =>
                {
                    var columns = new QuarterColumnDef[count];
                    var currentX = (double)startX;

                    // Pick quarters sequentially from our pool to ensure valid format
                    var quarterLabels = AllQuarters.Take(count).ToArray();

                    for (var i = 0; i < count; i++)
                    {
                        var quarterLabel = i < quarterLabels.Length
                            ? quarterLabels[i]
                            : $"Q{(i % 4) + 1} {2020 + (i / 4)}";

                        columns[i] = new QuarterColumnDef(
                            Id: $"qlabel_{i}",
                            Label: quarterLabel,
                            X: currentX,
                            Width: widths[i]);

                        currentX += widths[i] + gaps[i];
                    }

                    return columns;
                })));
    }

    /// <summary>
    /// Generates activity node definitions positioned within the bounds of given swimlanes and quarter columns.
    /// Useful for testing the PositionResolver with known expected assignments.
    /// </summary>
    public static Gen<ActivityNodeDef> GenActivityNodeInBounds(
        SwimlaneDef swimlane,
        QuarterColumnDef quarterColumn,
        int index)
    {
        var nodeWidth = 120.0;
        var nodeHeight = 40.0;

        // Position node so its center falls within the swimlane's Y range and column's X range
        var minX = quarterColumn.X;
        var maxX = quarterColumn.X + quarterColumn.Width - nodeWidth;
        var minY = swimlane.Y;
        var maxY = swimlane.Y + swimlane.Height - nodeHeight;

        // Clamp to ensure valid range
        var safeMinX = (int)Math.Max(0, minX);
        var safeMaxX = (int)Math.Max(safeMinX + 1, maxX);
        var safeMinY = (int)Math.Max(0, minY);
        var safeMaxY = (int)Math.Max(safeMinY + 1, maxY);

        return Gen.Choose(safeMinX, safeMaxX).SelectMany(x =>
            Gen.Choose(safeMinY, safeMaxY).SelectMany(y =>
                GenRoundTripLabel().Select(label =>
                    new ActivityNodeDef(
                        Id: $"node_{index}",
                        Label: $"{label}{index}",
                        X: x,
                        Y: y,
                        Width: nodeWidth,
                        Height: nodeHeight))));
    }

    /// <summary>
    /// Arbitrary instance for round-trip RoadmapModel.
    /// </summary>
    public static Arbitrary<RoadmapModel> ArbRoundTripRoadmapModel() =>
        GenRoundTripRoadmapModel().ToArbitrary();
}
