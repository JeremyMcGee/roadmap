using MarkdownToDrawio.Model;

namespace MarkdownToDrawio.Validation;

/// <summary>
/// Validates a RoadmapModel for completeness and consistency.
/// </summary>
public interface IValidator
{
    IReadOnlyList<ValidationError> Validate(RoadmapModel model);
}
