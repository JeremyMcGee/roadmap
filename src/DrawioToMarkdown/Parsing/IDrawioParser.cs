namespace DrawioToMarkdown.Parsing;

/// <summary>
/// Parses draw.io XML content into an intermediate representation.
/// </summary>
public interface IDrawioParser
{
    ParsedDiagram Parse(string xmlContent);
}
