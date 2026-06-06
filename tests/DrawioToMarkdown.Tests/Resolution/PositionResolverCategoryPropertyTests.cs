// Feature: drawio-to-roadmap-markdown, Property 1: Position resolver assigns correct category

using DrawioToMarkdown.Parsing;
using DrawioToMarkdown.Resolution;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Gen = FsCheck.Fluent.Gen;

namespace DrawioToMarkdown.Tests.Resolution;

/// <summary>
/// Property-based tests for PositionResolver category assignment logic.
/// **Validates: Requirements 2.1, 2.3, 2.5**
/// </summary>
public class PositionResolverCategoryPropertyTests
{
    private static readonly string[] CategoryLabels =
    {
        "Infrastructure", "Platform", "Security", "Data", "Frontend",
        "Backend", "DevOps", "QA", "Design", "Analytics"
    };

    /// <summary>
    /// Test input combining non-overlapping swimlanes and activity nodes.
    /// </summary>
    public sealed record CategoryTestInput(
        List<SwimlaneDef> Swimlanes,
        List<ActivityNodeDef> Nodes,
        QuarterColumnDef Quarter);

    public static class Arbitraries
    {
        /// <summary>
        /// Generates a list of non-overlapping swimlanes with random y-positions, heights, and labels.
        /// Swimlanes are stacked vertically with optional gaps between them.
        /// </summary>
        private static Gen<List<SwimlaneDef>> GenNonOverlappingSwimlanes()
        {
            return Gen.Choose(1, 6).SelectMany(count =>
            {
                return Gen.Choose(0, 500).SelectMany(startY =>
                {
                    var heightGens = Enumerable.Range(0, count)
                        .Select(_ => Gen.Choose(50, 300));
                    var gapGens = Enumerable.Range(0, count)
                        .Select(_ => Gen.Choose(0, 100));

                    return Gen.CollectToArray(heightGens).SelectMany(heights =>
                        Gen.CollectToArray(gapGens).SelectMany(gaps =>
                        {
                            return Gen.Shuffle(
                                Enumerable.Range(0, CategoryLabels.Length).ToArray()
                            ).Select(shuffled =>
                            {
                                var swimlanes = new List<SwimlaneDef>();
                                double currentY = startY;

                                for (int i = 0; i < count; i++)
                                {
                                    var label = CategoryLabels[shuffled[i % CategoryLabels.Length]];
                                    var id = $"lane_{i}";
                                    swimlanes.Add(new SwimlaneDef(id, label, 0.0, currentY, 1000.0, heights[i]));
                                    currentY += heights[i] + gaps[i];
                                }

                                return swimlanes;
                            });
                        }));
                });
            });
        }

        /// <summary>
        /// Generates an activity node at a random vertical position within the overall range
        /// (including positions above, below, and within swimlane boundaries).
        /// </summary>
        private static Gen<ActivityNodeDef> GenActivityNode(double overallMinY, double overallMaxY, int index)
        {
            return Gen.Choose(20, 60).SelectMany(height =>
            {
                // Position the node so its vertical center can be anywhere in [overallMinY, overallMaxY]
                var minNodeY = (int)Math.Max(0, overallMinY - height / 2.0);
                var maxNodeY = (int)Math.Max(minNodeY + 1, overallMaxY - height / 2.0);

                return Gen.Choose(minNodeY, maxNodeY).SelectMany(y =>
                    Gen.Choose(50, 800).SelectMany(x =>
                        Gen.Choose(60, 200).Select(width =>
                            new ActivityNodeDef(
                                $"act_{index}",
                                $"Activity{index}",
                                x,
                                y,
                                width,
                                height))));
            });
        }

        public static Arbitrary<CategoryTestInput> ArbCategoryTestInput()
        {
            var gen = GenNonOverlappingSwimlanes().SelectMany(swimlanes =>
            {
                var overallMinY = swimlanes.Min(s => s.Y) - 200;
                var overallMaxY = swimlanes.Max(s => s.Y + s.Height) + 200;

                return Gen.Choose(1, 5).SelectMany(nodeCount =>
                {
                    var nodeGens = Enumerable.Range(0, nodeCount)
                        .Select(i => GenActivityNode(overallMinY, overallMaxY, i));

                    return Gen.CollectToArray(nodeGens).Select(nodes =>
                    {
                        // Include a single quarter column covering all x positions
                        var quarter = new QuarterColumnDef("qlabel_q1", "Q1 2025", 0.0, 2000.0);
                        return new CategoryTestInput(swimlanes, nodes.ToList(), quarter);
                    });
                });
            });

            return gen.ToArbitrary();
        }
    }

    /// <summary>
    /// For any set of non-overlapping swimlanes and any activity node whose vertical center
    /// falls within exactly one swimlane's y-range, the position resolver SHALL assign that
    /// swimlane's category label to the activity.
    ///
    /// For any activity node whose vertical center falls outside all swimlanes or within
    /// multiple overlapping swimlanes (not applicable here since swimlanes are non-overlapping),
    /// the position resolver SHALL assign the swimlane whose vertical midpoint is closest
    /// to the node's vertical center, breaking ties by document order.
    ///
    /// **Validates: Requirements 2.1, 2.3, 2.5**
    /// </summary>
    [Property(MaxTest = 200, Arbitrary = new[] { typeof(Arbitraries) })]
    public bool PositionResolver_AssignsCorrectCategory(CategoryTestInput input)
    {
        var diagram = new ParsedDiagram(
            input.Swimlanes,
            new List<QuarterColumnDef> { input.Quarter },
            input.Nodes,
            new List<DependencyEdgeDef>());

        var resolver = new PositionResolver();
        var model = resolver.Resolve(diagram);

        for (int i = 0; i < input.Nodes.Count; i++)
        {
            var node = input.Nodes[i];
            var activity = model.Activities.First(a => a.Label == node.Label);
            var expectedCategory = ComputeExpectedCategory(node, input.Swimlanes);

            if (activity.Category != expectedCategory)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Computes the expected category for an activity node using the algorithm from the design:
    /// 1. If vertical center falls within exactly one swimlane → that swimlane's label
    /// 2. If zero or multiple → closest midpoint, tie-break by document order (first in list)
    /// </summary>
    private static string ComputeExpectedCategory(ActivityNodeDef node, List<SwimlaneDef> swimlanes)
    {
        var verticalCenter = node.Y + node.Height / 2.0;

        // Find all swimlanes containing the vertical center
        var containing = swimlanes
            .Where(s => verticalCenter >= s.Y && verticalCenter <= s.Y + s.Height)
            .ToList();

        if (containing.Count == 1)
        {
            return containing[0].Label;
        }

        // Zero or multiple: find closest by vertical midpoint, tie-break by document order
        var bestLabel = swimlanes[0].Label;
        var bestDistance = Math.Abs((swimlanes[0].Y + swimlanes[0].Height / 2.0) - verticalCenter);

        for (int i = 1; i < swimlanes.Count; i++)
        {
            var lane = swimlanes[i];
            var midpoint = lane.Y + lane.Height / 2.0;
            var distance = Math.Abs(midpoint - verticalCenter);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestLabel = lane.Label;
            }
            // On tie (distance == bestDistance), first in list wins — no update
        }

        return bestLabel;
    }
}
