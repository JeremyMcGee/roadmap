using DrawioToMarkdown.Parsing;

namespace DrawioToMarkdown.Graph;

/// <summary>
/// Builds a DependencyGraph from a parsed diagram.
/// </summary>
public interface IGraphBuilder
{
    DependencyGraph Build(ParsedDiagram diagram);
}
