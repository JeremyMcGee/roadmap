// Feature: markdown-to-drawio, Property 4: Generated XML Is Valid
using System.Xml.Linq;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using MarkdownToDrawio.Model;
using MarkdownToDrawio.Output;
using MarkdownToDrawio.Tests.Generators;

namespace MarkdownToDrawio.Tests.Output;

/// <summary>
/// Property-based tests verifying that generated Draw.IO XML is always valid.
/// **Validates: Requirements 3.1, 3.9**
/// </summary>
public class XmlValidityPropertyTests
{
    /// <summary>
    /// Provides the Arbitrary for valid RoadmapModel instances.
    /// </summary>
    public static class Arbitraries
    {
        public static Arbitrary<RoadmapModel> RoadmapModelArbitrary() =>
            ArbitraryRoadmaps.GenValidRoadmapModel().ToArbitrary();
    }

    /// <summary>
    /// For any valid RoadmapModel, the Diagram Generator SHALL produce a string
    /// that is parseable as valid XML by XDocument.Parse() without throwing an exception.
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(Arbitraries) })]
    public bool GeneratedXml_IsValid(RoadmapModel model)
    {
        var generator = new DiagramGenerator();
        var xml = generator.Generate(model);
        try
        {
            XDocument.Parse(xml);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
