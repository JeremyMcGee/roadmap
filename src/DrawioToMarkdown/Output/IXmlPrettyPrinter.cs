using DrawioToMarkdown.Model;

namespace DrawioToMarkdown.Output;

/// <summary>
/// Converts a RoadmapModel to draw.io-compatible XML with swimlanes and quarter columns.
/// </summary>
public interface IXmlPrettyPrinter
{
    string Print(RoadmapModel model);
}
