using MarkdownToDrawio.Model;

namespace MarkdownToDrawio.Parsing;

/// <summary>
/// Parses Markdown text into a RoadmapModel.
/// </summary>
public interface IMarkdownParser
{
    RoadmapModel Parse(string markdownContent);
}
