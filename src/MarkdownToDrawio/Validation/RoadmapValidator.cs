using MarkdownToDrawio.Model;

namespace MarkdownToDrawio.Validation;

/// <summary>
/// Validates a RoadmapModel for completeness and consistency.
/// Collects all errors (does not short-circuit on first failure).
/// </summary>
public sealed class RoadmapValidator : IValidator
{
    public IReadOnlyList<ValidationError> Validate(RoadmapModel model)
    {
        var errors = new List<ValidationError>();

        // Build a HashSet of all activity labels for O(1) dependency lookups
        var knownLabels = new HashSet<string>(
            model.Activities.Select(a => a.Label),
            StringComparer.Ordinal);

        foreach (var activity in model.Activities)
        {
            // Check for missing Quarter heading (null means heading was absent)
            if (activity.Quarter is null)
            {
                errors.Add(new ValidationError(
                    activity.Label,
                    $"Activity \"{activity.Label}\" is missing a Quarter"));
            }
            else if (string.IsNullOrWhiteSpace(activity.Quarter))
            {
                // Non-null but whitespace-only means heading existed with no content
                errors.Add(new ValidationError(
                    activity.Label,
                    $"Activity \"{activity.Label}\" has an empty Quarter value"));
            }

            // Check for missing Category heading (null means heading was absent)
            if (activity.Category is null)
            {
                errors.Add(new ValidationError(
                    activity.Label,
                    $"Activity \"{activity.Label}\" is missing a Category"));
            }
            else if (string.IsNullOrWhiteSpace(activity.Category))
            {
                // Non-null but whitespace-only means heading existed with no content
                errors.Add(new ValidationError(
                    activity.Label,
                    $"Activity \"{activity.Label}\" has an empty Category value"));
            }

            // Check all dependency labels resolve to an existing activity
            foreach (var depLabel in activity.DependencyLabels)
            {
                if (!knownLabels.Contains(depLabel))
                {
                    errors.Add(new ValidationError(
                        activity.Label,
                        $"Activity \"{activity.Label}\" depends on \"{depLabel}\" which does not exist"));
                }
            }
        }

        return errors;
    }
}
