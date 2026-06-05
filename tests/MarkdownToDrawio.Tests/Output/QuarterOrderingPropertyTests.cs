// Feature: markdown-to-drawio, Property 6: Quarter Column Ordering
using System.Xml.Linq;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using MarkdownToDrawio.Model;
using MarkdownToDrawio.Output;
using MarkdownToDrawio.Tests.Generators;

namespace MarkdownToDrawio.Tests.Output;

/// <summary>
/// Property-based tests verifying that quarter columns are ordered correctly in generated XML.
/// Parseable "Q{n} {year}" quarters are sorted by year ascending then quarter number ascending.
/// Unparseable quarter values appear after valid quarters, sorted alphabetically.
/// **Validates: Requirements 3.4, 3.5, 3.6, 3.10**
/// </summary>
public class QuarterOrderingPropertyTests
{
    public static class Arbitraries
    {
        public static Arbitrary<RoadmapModel> RoadmapModelArbitrary() =>
            ArbitraryRoadmaps.GenValidRoadmapModel().ToArbitrary();
    }

    [Property(MaxTest = 100, Arbitrary = new[] { typeof(Arbitraries) })]
    public bool QuarterColumns_AreOrderedByYearThenQuarterNumber_WithUnparseableAfterValid(RoadmapModel model)
    {
        var generator = new DiagramGenerator();
        var xml = generator.Generate(model);
        var doc = XDocument.Parse(xml);

        // Find all quarter label mxCell elements (id starting with "qlabel_")
        var quarterLabels = doc.Descendants("mxCell")
            .Where(cell => cell.Attribute("id")?.Value.StartsWith("qlabel_") == true)
            .Select(cell =>
            {
                var value = cell.Attribute("value")?.Value ?? "";
                var geometry = cell.Element("mxGeometry");
                var x = double.Parse(geometry!.Attribute("x")!.Value);
                return (Value: value, X: x);
            })
            .OrderBy(q => q.X)
            .ToList();

        if (quarterLabels.Count <= 1)
            return true;

        // Separate into parseable and unparseable using the internal method
        var parseable = new List<(string Value, double X, ParsedQuarter Parsed)>();
        var unparseable = new List<(string Value, double X)>();

        foreach (var ql in quarterLabels)
        {
            var parsed = DiagramGenerator.TryParseQuarter(ql.Value);
            if (parsed is not null)
            {
                parseable.Add((ql.Value, ql.X, parsed));
            }
            else
            {
                unparseable.Add((ql.Value, ql.X));
            }
        }

        // Verify parseable quarters are sorted by year then quarter number ascending
        for (int i = 1; i < parseable.Count; i++)
        {
            if (parseable[i].Parsed.CompareTo(parseable[i - 1].Parsed) < 0)
                return false;
        }

        // Verify unparseable quarters appear after all valid quarters (higher x positions)
        if (parseable.Count > 0 && unparseable.Count > 0)
        {
            var maxParseableX = parseable.Max(p => p.X);
            var minUnparseableX = unparseable.Min(u => u.X);
            if (minUnparseableX <= maxParseableX)
                return false;
        }

        // Verify unparseable quarters are sorted alphabetically among themselves
        for (int i = 1; i < unparseable.Count; i++)
        {
            if (StringComparer.OrdinalIgnoreCase.Compare(unparseable[i].Value, unparseable[i - 1].Value) < 0)
                return false;
        }

        return true;
    }
}
