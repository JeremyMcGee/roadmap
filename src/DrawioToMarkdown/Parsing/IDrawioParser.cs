namespace DrawioToMarkdown.Parsing;

/// <summary>
/// Parses draw.io XML content into a <see cref="ParsedDiagram"/> containing
/// swimlanes, quarter columns, activity nodes, and dependency edges.
/// </summary>
public interface IDrawioParser
{
    ParsedDiagram Parse(string xmlContent);
}
