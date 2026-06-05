using DrawioToMarkdown.Graph;

namespace DrawioToMarkdown.Output;

/// <summary>
/// Converts a DependencyGraph back to draw.io-compatible XML.
/// </summary>
public interface IXmlPrettyPrinter
{
    string Print(DependencyGraph graph);
}
