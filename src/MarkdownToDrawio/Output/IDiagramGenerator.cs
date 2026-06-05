using MarkdownToDrawio.Model;

namespace MarkdownToDrawio.Output;

/// <summary>
/// Generates Draw.IO XML from a validated RoadmapModel.
/// </summary>
public interface IDiagramGenerator
{
    string Generate(RoadmapModel model);
}
