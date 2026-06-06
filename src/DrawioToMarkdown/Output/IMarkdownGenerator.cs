using DrawioToMarkdown.Model;

namespace DrawioToMarkdown.Output;

/// <summary>
/// Generates roadmap Markdown from a RoadmapModel.
/// </summary>
public interface IMarkdownGenerator
{
    string Generate(RoadmapModel model);
}
