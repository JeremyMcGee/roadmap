using DrawioToMarkdown.Model;
using DrawioToMarkdown.Parsing;

namespace DrawioToMarkdown.Resolution;

/// <summary>
/// Resolves geometric positions of activity nodes within swimlanes and quarter columns
/// to produce a fully categorized and quarter-assigned RoadmapModel.
/// </summary>
public interface IPositionResolver
{
    /// <summary>
    /// Assigns each activity in the parsed diagram a category (from swimlane containment)
    /// and quarter (from quarter column containment) based on geometric position,
    /// and resolves dependency edges to produce a complete RoadmapModel.
    /// </summary>
    RoadmapModel Resolve(ParsedDiagram diagram);
}
