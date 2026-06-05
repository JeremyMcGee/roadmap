using DrawioToMarkdown.Graph;

namespace DrawioToMarkdown.Output;

/// <summary>
/// Generates Markdown from a DependencyGraph.
/// </summary>
public interface IMarkdownGenerator
{
    string Generate(DependencyGraph graph);
}
