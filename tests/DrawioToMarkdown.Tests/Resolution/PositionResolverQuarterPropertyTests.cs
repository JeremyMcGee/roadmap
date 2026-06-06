// Feature: drawio-to-roadmap-markdown, Property 2: Position resolver assigns correct quarter

using DrawioToMarkdown.Parsing;
using DrawioToMarkdown.Resolution;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Gen = FsCheck.Fluent.Gen;

namespace DrawioToMarkdown.Tests.Resolution;

/// <summary>
/// Property-based tests for PositionResolver quarter assignment logic.
/// **Validates: Requirements 2.2, 2.4, 2.6**
/// </summary>
public class PositionResolverQuarterPropertyTests
{
    private static readonly string[] QuarterLabels =
    {
        "Q1 2024", "Q2 2024", "Q3 2024", "Q4 2024",
        "Q1 2025", "Q2 2025", "Q3 2025", "Q4 2025",
        "Q1 2026", "Q2 2026"
    };

    /// <summary>
    /// Test input combining non-overlapping quarter columns and activity nodes.
    /// </summary>
    public sealed record QuarterTestInput(
        List<QuarterColumnDef> Columns,
        List<ActivityNodeDef> Nodes,
        SwimlaneDef Swimlane);

    public static class Arbitraries
    {
        /// <summary>
        /// Generates a list of non-overlapping quarter columns with random x-positions, widths, and labels.
        /// Columns are arranged horizontally with optional gaps between them.
        /// </summary>
        private static Gen<List<QuarterColumnDef>> GenNonOverlappingColumns()
        {
            return Gen.Choose(1, 6).SelectMany(count =>
            {
                return Gen.Choose(0, 500).SelectMany(startX =>
                {
                    var widthGens = Enumerable.Range(0, count)
                        .Select(_ => Gen.Choose(100, 400));
                    var gapGens = Enumerable.Range(0, count)
                        .Select(_ => Gen.Choose(0, 150));

                    return Gen.CollectToArray(widthGens).SelectMany(widths =>
                        Gen.CollectToArray(gapGens).SelectMany(gaps =>
                        {
                            return Gen.Shuffle(
                                Enumerable.Range(0, QuarterLabels.Length).ToArray()
                            ).Select(shuffled =>
                            {
                                var columns = new List<QuarterColumnDef>();
                                double currentX = startX;

                                for (int i = 0; i < count; i++)
                                {
                                    var label = QuarterLabels[shuffled[i % QuarterLabels.Length]];
                                    var id = $"qlabel_q{i}";
                                    columns.Add(new QuarterColumnDef(id, label, currentX, widths[i]));
                                    currentX += widths[i] + gaps[i];
                                }

                                return columns;
                            });
                        }));
                });
            });
        }

        /// <summary>
        /// Generates an activity node at a random horizontal position within the overall range
        /// (including positions to the left, right, and within column boundaries).
        /// </summary>
        private static Gen<ActivityNodeDef> GenActivityNode(double overallMinX, double overallMaxX, int index)
        {
            return Gen.Choose(60, 200).SelectMany(width =>
            {
                // Position the node so its horizontal center can be anywhere in [overallMinX, overallMaxX]
                var minNodeX = (int)Math.Max(0, overallMinX - width / 2.0);
                var maxNodeX = (int)Math.Max(minNodeX + 1, overallMaxX - width / 2.0);

                return Gen.Choose(minNodeX, maxNodeX).SelectMany(x =>
                    Gen.Choose(50, 800).SelectMany(y =>
                        Gen.Choose(20, 60).Select(height =>
                            new ActivityNodeDef(
                                $"act_{index}",
                                $"Activity{index}",
                                x,
                                y,
                                width,
                                height))));
            });
        }

        public static Arbitrary<QuarterTestInput> ArbQuarterTestInput()
        {
            var gen = GenNonOverlappingColumns().SelectMany(columns =>
            {
                var overallMinX = columns.Min(c => c.X) - 200;
                var overallMaxX = columns.Max(c => c.X + c.Width) + 200;

                return Gen.Choose(1, 5).SelectMany(nodeCount =>
                {
                    var nodeGens = Enumerable.Range(0, nodeCount)
                        .Select(i => GenActivityNode(overallMinX, overallMaxX, i));

                    return Gen.CollectToArray(nodeGens).Select(nodes =>
                    {
                        // Include a single swimlane covering all y positions so PositionResolver doesn't throw
                        var swimlane = new SwimlaneDef("lane_all", "AllCategories", 0.0, 0.0, 3000.0, 2000.0);
                        return new QuarterTestInput(columns, nodes.ToList(), swimlane);
                    });
                });
            });

            return gen.ToArbitrary();
        }
    }

    /// <summary>
    /// For any set of non-overlapping quarter columns and any activity node whose horizontal center
    /// falls within exactly one column's x-range, the position resolver SHALL assign that
    /// column's quarter label to the activity.
    ///
    /// For any activity node whose horizontal center falls outside all columns or within
    /// multiple overlapping columns (not applicable here since columns are non-overlapping),
    /// the position resolver SHALL assign the column whose horizontal midpoint is closest
    /// to the node's horizontal center, breaking ties by document order.
    ///
    /// **Validates: Requirements 2.2, 2.4, 2.6**
    /// </summary>
    [Property(MaxTest = 200, Arbitrary = new[] { typeof(Arbitraries) })]
    public bool PositionResolver_AssignsCorrectQuarter(QuarterTestInput input)
    {
        var diagram = new ParsedDiagram(
            new List<SwimlaneDef> { input.Swimlane },
            input.Columns,
            input.Nodes,
            new List<DependencyEdgeDef>());

        var resolver = new PositionResolver();
        var model = resolver.Resolve(diagram);

        for (int i = 0; i < input.Nodes.Count; i++)
        {
            var node = input.Nodes[i];
            var activity = model.Activities.First(a => a.Label == node.Label);
            var expectedQuarter = ComputeExpectedQuarter(node, input.Columns);

            if (activity.Quarter != expectedQuarter)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Computes the expected quarter for an activity node using the algorithm from the design:
    /// 1. If horizontal center falls within exactly one column → that column's label
    /// 2. If zero or multiple → closest midpoint, tie-break by document order (first in list)
    /// </summary>
    private static string ComputeExpectedQuarter(ActivityNodeDef node, List<QuarterColumnDef> columns)
    {
        var horizontalCenter = node.X + node.Width / 2.0;

        // Find all columns containing the horizontal center
        var containing = columns
            .Where(c => horizontalCenter >= c.X && horizontalCenter <= c.X + c.Width)
            .ToList();

        if (containing.Count == 1)
        {
            return containing[0].Label;
        }

        // Zero or multiple: find closest by horizontal midpoint, tie-break by document order
        var bestLabel = columns[0].Label;
        var bestDistance = Math.Abs((columns[0].X + columns[0].Width / 2.0) - horizontalCenter);

        for (int i = 1; i < columns.Count; i++)
        {
            var col = columns[i];
            var midpoint = col.X + col.Width / 2.0;
            var distance = Math.Abs(midpoint - horizontalCenter);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestLabel = col.Label;
            }
            // On tie (distance == bestDistance), first in list wins — no update
        }

        return bestLabel;
    }
}
