using DrawioToMarkdown.Graph;
using DrawioToMarkdown.Model;
using DrawioToMarkdown.Parsing;
using FsCheck;
using FsCheck.Fluent;
using Gen = FsCheck.Fluent.Gen;

namespace DrawioToMarkdown.Tests.Generators;

/// <summary>
/// FsCheck Arbitrary generators for domain types used in property-based tests.
/// </summary>
public static class ArbitraryGraphs
{
    /// <summary>
    /// Characters allowed in generated node IDs and labels.
    /// </summary>
    private static readonly char[] AlphanumericChars =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789".ToCharArray();

    /// <summary>
    /// Generates a non-empty alphanumeric string of length 1–20.
    /// </summary>
    private static Gen<string> GenAlphanumericString()
    {
        return Gen.Choose(1, 20).SelectMany(length =>
            Gen.Elements(AlphanumericChars).ArrayOf(length).Select(chars => new string(chars)));
    }

    /// <summary>
    /// Generator for a valid DependencyGraph with:
    /// - 1–50 nodes with unique IDs and non-empty alphanumeric labels
    /// - Random valid edges (source and target reference existing node IDs)
    /// - Dependencies dictionary built from those edges
    /// </summary>
    public static Gen<DependencyGraph> GenDependencyGraph()
    {
        return Gen.Choose(1, 50).SelectMany(nodeCount =>
        {
            // Generate unique nodes: use index prefix to guarantee unique IDs
            var nodeGens = Enumerable.Range(0, nodeCount)
                .Select(i => GenAlphanumericString().Select(label =>
                    new ActivityNode($"n{i}", label)));

            return Gen.CollectToArray(nodeGens).SelectMany(nodes =>
            {
                var nodeIds = nodes.Select(n => n.Id).ToArray();
                var maxEdges = Math.Min(nodeCount * 2, nodeCount * (nodeCount - 1));

                if (nodeIds.Length < 2 || maxEdges == 0)
                {
                    // No edges possible: build graph with empty dependency sets
                    var nodesDict = nodes.ToDictionary(n => n.Id, n => n);
                    var emptyDeps = nodes.ToDictionary(
                        n => n.Id,
                        _ => (IReadOnlySet<string>)new HashSet<string>());
                    return Gen.Constant(new DependencyGraph(nodesDict, emptyDeps));
                }

                return Gen.Choose(0, maxEdges).SelectMany(edgeCount =>
                {
                    var edgeGen = Gen.Two(Gen.Elements(nodeIds))
                        .Where(pair => !string.Equals(pair.Item1, pair.Item2, StringComparison.Ordinal))
                        .Select(pair => new DependencyEdge(pair.Item1, pair.Item2));

                    return edgeGen.ArrayOf(edgeCount).Select(edges =>
                    {
                        var nodesDict = nodes.ToDictionary(n => n.Id, n => n);

                        // Build dependencies: for each edge, target depends on source
                        var deps = nodes.ToDictionary(
                            n => n.Id,
                            _ => new HashSet<string>());

                        foreach (var edge in edges)
                        {
                            deps[edge.TargetId].Add(edge.SourceId);
                        }

                        var readOnlyDeps = deps.ToDictionary(
                            kvp => kvp.Key,
                            kvp => (IReadOnlySet<string>)kvp.Value);

                        return new DependencyGraph(nodesDict, readOnlyDeps);
                    });
                });
            });
        });
    }

    /// <summary>
    /// Generator for ParsedDiagram that includes both valid edges and dangling edges
    /// (edges referencing non-existent node IDs).
    /// </summary>
    public static Gen<ParsedDiagram> GenParsedDiagram()
    {
        return Gen.Choose(1, 50).SelectMany(nodeCount =>
        {
            var nodeGens = Enumerable.Range(0, nodeCount)
                .Select(i => GenAlphanumericString().Select(label =>
                    new ActivityNodeDef($"n{i}", label, i * 120.0, 0.0, 100.0, 40.0)));

            return Gen.CollectToArray(nodeGens).SelectMany(nodes =>
            {
                var nodeIds = nodes.Select(n => n.Id).ToArray();

                // Dangling edge generator: edges referencing non-existent IDs
                var danglingIdGen = GenAlphanumericString().Select(s => $"dangling_{s}");
                Gen<DependencyEdgeDef> danglingEdgeGen;

                if (nodeIds.Length > 0)
                {
                    danglingEdgeGen = danglingIdGen.SelectMany(danglingId =>
                        Gen.Elements(true, false).SelectMany(sourceIsDangling =>
                            Gen.Elements(nodeIds).Select(existingId =>
                                sourceIsDangling
                                    ? new DependencyEdgeDef(danglingId, existingId)
                                    : new DependencyEdgeDef(existingId, danglingId))));
                }
                else
                {
                    danglingEdgeGen = danglingIdGen.SelectMany(id1 =>
                        danglingIdGen.Select(id2 =>
                            new DependencyEdgeDef(id1, id2)));
                }

                if (nodeIds.Length < 2)
                {
                    // Only dangling edges
                    return Gen.Choose(1, 5).SelectMany(danglingCount =>
                        danglingEdgeGen.ArrayOf(danglingCount).Select(danglingEdges =>
                            new ParsedDiagram(
                                Array.Empty<SwimlaneDef>(),
                                Array.Empty<QuarterColumnDef>(),
                                nodes.ToList(),
                                danglingEdges.ToList())));
                }

                // Valid edge generator
                var validEdgeGen = Gen.Two(Gen.Elements(nodeIds))
                    .Where(pair => !string.Equals(pair.Item1, pair.Item2, StringComparison.Ordinal))
                    .Select(pair => new DependencyEdgeDef(pair.Item1, pair.Item2));

                var maxValidEdges = Math.Min(nodeCount * 2, nodeCount * (nodeCount - 1));

                return Gen.Choose(0, maxValidEdges).SelectMany(validCount =>
                    Gen.Choose(1, 5).SelectMany(danglingCount =>
                        validEdgeGen.ArrayOf(validCount).SelectMany(validEdges =>
                            danglingEdgeGen.ArrayOf(danglingCount).Select(danglingEdges =>
                            {
                                var allEdges = validEdges.Concat(danglingEdges).ToList();
                                return new ParsedDiagram(
                                    Array.Empty<SwimlaneDef>(),
                                    Array.Empty<QuarterColumnDef>(),
                                    nodes.ToList(),
                                    allEdges);
                            }))));
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
    /// Arbitrary instance for DependencyGraph.
    /// </summary>
    public static Arbitrary<DependencyGraph> ArbDependencyGraph() =>
        GenDependencyGraph().ToArbitrary();

    /// <summary>
    /// Arbitrary instance for ParsedDiagram.
    /// </summary>
    public static Arbitrary<ParsedDiagram> ArbParsedDiagram() =>
        GenParsedDiagram().ToArbitrary();

    /// <summary>
    /// Arbitrary instance for file path strings.
    /// </summary>
    public static Arbitrary<string> ArbFilePath() =>
        GenFilePath().ToArbitrary();

    /// <summary>
    /// Generator for a valid RoadmapModel with:
    /// - 1–20 activities with unique non-empty alphanumeric labels
    /// - Quarters in "Q{1-4} {2020-2030}" format
    /// - Categories from a random pool
    /// - 0–5 dependency labels referencing other activity labels in the model
    /// </summary>
    public static Gen<RoadmapModel> GenRoadmapModel()
    {
        var quarters = new[] { "Q1 2024", "Q2 2024", "Q3 2024", "Q4 2024", "Q1 2025", "Q2 2025" };
        var categories = new[] { "Infrastructure", "Platform", "Security", "Data", "Frontend" };

        return Gen.Choose(1, 20).SelectMany(activityCount =>
        {
            // Generate unique labels
            var labelGens = Enumerable.Range(0, activityCount)
                .Select(i => GenAlphanumericString().Select(s => $"{s}{i}"));

            return Gen.CollectToArray(labelGens).SelectMany(labels =>
            {
                var activityGens = labels.Select(label =>
                    Gen.Elements(quarters).SelectMany(quarter =>
                        Gen.Elements(categories).SelectMany(category =>
                        {
                            // Generate 0–5 dependency labels from other labels in the model
                            var otherLabels = labels.Where(l => l != label).ToArray();
                            if (otherLabels.Length == 0)
                            {
                                return Gen.Constant(new RoadmapActivity(label, quarter, category, Array.Empty<string>()));
                            }
                            var maxDeps = Math.Min(5, otherLabels.Length);
                            return Gen.Choose(0, maxDeps).SelectMany(depCount =>
                                Gen.Elements(otherLabels).ArrayOf(depCount).Select(deps =>
                                    new RoadmapActivity(label, quarter, category, deps.Distinct().ToArray())));
                        })));

                return Gen.CollectToArray(activityGens).Select(activities =>
                    new RoadmapModel(activities.ToList()));
            });
        });
    }

    /// <summary>
    /// Arbitrary instance for RoadmapModel.
    /// </summary>
    public static Arbitrary<RoadmapModel> ArbRoadmapModel() =>
        GenRoadmapModel().ToArbitrary();
}
