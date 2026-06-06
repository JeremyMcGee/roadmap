// Feature: drawio-to-roadmap-markdown, Property 7: Duplicate label merging
using DrawioToMarkdown.Parsing;
using FsCheck;
using FsCheck.Xunit;
using FsCheck.Fluent;
using Gen = FsCheck.Fluent.Gen;

namespace DrawioToMarkdown.Tests.Parsing;

/// <summary>
/// Property 7: Duplicate label merging
///
/// **Validates: Requirements 6.5**
///
/// For any draw.io XML containing two or more activity nodes with the same label
/// (after whitespace normalization), the parser SHALL produce a single activity entry
/// for that label with the dependency set being the union of all individual nodes'
/// dependency sets.
/// </summary>
public class DuplicateLabelMergingPropertyTests
{
    private readonly DrawioParser _parser = new();

    /// <summary>
    /// Generates a test scenario with duplicate activity labels and random edges.
    /// </summary>
    private static Gen<DuplicateLabelScenario> GenDuplicateLabelScenario()
    {
        // Generate 1-5 unique labels
        return Gen.Choose(1, 5).SelectMany(uniqueLabelCount =>
        {
            var labelGens = Enumerable.Range(0, uniqueLabelCount)
                .Select(i => Gen.Constant($"Activity{i}"));

            return Gen.CollectToArray(labelGens).SelectMany(uniqueLabels =>
            {
                // For each unique label, generate 1-3 duplicate nodes (same label, different IDs)
                var nodeCountGens = uniqueLabels.Select(_ => Gen.Choose(1, 3));

                return Gen.CollectToArray(nodeCountGens).SelectMany(nodeCounts =>
                {
                    // Build node list: each unique label gets nodeCounts[i] nodes
                    var nodes = new List<(string Id, string Label)>();
                    var nodeIndex = 0;

                    for (var i = 0; i < uniqueLabels.Length; i++)
                    {
                        for (var j = 0; j < nodeCounts[i]; j++)
                        {
                            nodes.Add(($"node_{nodeIndex}", uniqueLabels[i]));
                            nodeIndex++;
                        }
                    }

                    // Generate random edges between nodes (0 to nodes.Count * 2 edges)
                    var maxEdges = Math.Max(1, nodes.Count * 2);
                    return Gen.Choose(0, maxEdges).SelectMany(edgeCount =>
                    {
                        if (nodes.Count < 2 || edgeCount == 0)
                        {
                            return Gen.Constant(new DuplicateLabelScenario(
                                nodes.ToArray(),
                                Array.Empty<(string SourceId, string TargetId)>(),
                                uniqueLabels));
                        }

                        var edgeGens = Enumerable.Range(0, edgeCount).Select(_ =>
                            Gen.Choose(0, nodes.Count - 1).SelectMany(srcIdx =>
                                Gen.Choose(0, nodes.Count - 1).Select(tgtIdx =>
                                    (SourceId: nodes[srcIdx].Id, TargetId: nodes[tgtIdx].Id))));

                        return Gen.CollectToArray(edgeGens).Select(edges =>
                            new DuplicateLabelScenario(
                                nodes.ToArray(),
                                edges,
                                uniqueLabels));
                    });
                });
            });
        });
    }

    /// <summary>
    /// Builds a draw.io XML string from the given nodes and edges.
    /// </summary>
    private static string BuildDrawioXml(
        (string Id, string Label)[] nodes,
        (string SourceId, string TargetId)[] edges)
    {
        var cellsXml = string.Join("\n",
            nodes.Select(n =>
                $"""
                    <mxCell id="{n.Id}" value="{n.Label}" style="rounded=1;whiteSpace=wrap;html=1" vertex="1" parent="1">
                      <mxGeometry x="100" y="100" width="120" height="40" as="geometry" />
                    </mxCell>
                """));

        var edgesXml = string.Join("\n",
            edges.Select((e, i) =>
                $"""
                    <mxCell id="edge_{i}" style="edgeStyle=orthogonalEdgeStyle" edge="1" source="{e.SourceId}" target="{e.TargetId}" parent="1">
                      <mxGeometry relative="1" as="geometry" />
                    </mxCell>
                """));

        return $"""
            <mxfile>
              <diagram>
                <mxGraphModel>
                  <root>
                    <mxCell id="0" />
                    <mxCell id="1" parent="0" />
                    {cellsXml}
                    {edgesXml}
                  </root>
                </mxGraphModel>
              </diagram>
            </mxfile>
            """;
    }

    public static class Arbitraries
    {
        public static Arbitrary<DuplicateLabelScenario> DuplicateLabelScenarioArbitrary() =>
            GenDuplicateLabelScenario().ToArbitrary();
    }

    /// <summary>
    /// **Validates: Requirements 6.5**
    ///
    /// For any draw.io XML containing activity nodes with duplicate labels,
    /// parsing produces only unique labels (one ActivityNodeDef per unique label).
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(Arbitraries) })]
    // Feature: drawio-to-roadmap-markdown, Property 7: Duplicate label merging
    public bool DuplicateLabelsAreMergedToUniqueEntries(DuplicateLabelScenario scenario)
    {
        var xml = BuildDrawioXml(scenario.Nodes, scenario.Edges);
        var parsed = _parser.Parse(xml);

        // Result should contain exactly one activity per unique label
        var parsedLabels = parsed.ActivityNodes.Select(n => n.Label).ToList();

        // No duplicates in output
        var hasDuplicates = parsedLabels.Count != parsedLabels.Distinct(StringComparer.Ordinal).Count();
        if (hasDuplicates)
            return false;

        // All unique labels from input are present in output
        var allInputLabelsPresent = scenario.UniqueLabels
            .All(label => parsedLabels.Contains(label, StringComparer.Ordinal));

        return allInputLabelsPresent && parsedLabels.Count == scenario.UniqueLabels.Length;
    }

    /// <summary>
    /// **Validates: Requirements 6.5**
    ///
    /// For any draw.io XML containing activity nodes with duplicate labels and edges
    /// between them, after merging, the dependency set for each label is the union of
    /// all individual nodes' dependency edges, and self-referencing edges introduced
    /// by merging are discarded.
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(Arbitraries) })]
    // Feature: drawio-to-roadmap-markdown, Property 7: Duplicate label merging
    public bool DuplicateLabelEdgesAreMergedAsUnion(DuplicateLabelScenario scenario)
    {
        var xml = BuildDrawioXml(scenario.Nodes, scenario.Edges);
        var parsed = _parser.Parse(xml);

        // Build expected edge set manually:
        // 1. Map each node ID to its label
        var idToLabel = scenario.Nodes.ToDictionary(n => n.Id, n => n.Label);

        // 2. For each label, collect the representative ID (first node with that label)
        var labelToRepId = scenario.Nodes
            .GroupBy(n => n.Label, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.Ordinal);

        // 3. Build mapping from any node ID to its representative ID
        var idToRepId = scenario.Nodes
            .ToDictionary(n => n.Id, n => labelToRepId[n.Label]);

        // 4. Compute expected edges after merging:
        //    - Rewrite source/target to representative IDs
        //    - Filter edges where both source and target are activity node IDs
        //    - Remove self-references (where source rep == target rep)
        //    - Deduplicate
        var activityNodeIds = new HashSet<string>(scenario.Nodes.Select(n => n.Id));
        var expectedEdges = scenario.Edges
            .Where(e => activityNodeIds.Contains(e.SourceId) && activityNodeIds.Contains(e.TargetId))
            .Select(e => (Source: idToRepId[e.SourceId], Target: idToRepId[e.TargetId]))
            .Where(e => e.Source != e.Target)
            .Distinct()
            .ToHashSet();

        // 5. Compare with actual parsed edges
        var actualEdges = parsed.Edges
            .Select(e => (Source: e.SourceId, Target: e.TargetId))
            .ToHashSet();

        return expectedEdges.SetEquals(actualEdges);
    }
}

/// <summary>
/// Test scenario containing nodes (with potential duplicate labels) and edges.
/// </summary>
public record DuplicateLabelScenario(
    (string Id, string Label)[] Nodes,
    (string SourceId, string TargetId)[] Edges,
    string[] UniqueLabels)
{
    public override string ToString() =>
        $"Nodes=[{string.Join(", ", Nodes.Select(n => $"{n.Id}:{n.Label}"))}], " +
        $"Edges=[{string.Join(", ", Edges.Select(e => $"{e.SourceId}->{e.TargetId}"))}]";
}
