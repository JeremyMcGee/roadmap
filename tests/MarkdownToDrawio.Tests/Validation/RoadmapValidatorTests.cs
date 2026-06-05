using MarkdownToDrawio.Model;
using MarkdownToDrawio.Validation;
using Xunit;

namespace MarkdownToDrawio.Tests.Validation;

public class RoadmapValidatorTests
{
    private readonly RoadmapValidator _validator = new();

    [Fact]
    public void Validate_ValidModel_ReturnsNoErrors()
    {
        // All fields present, all dependencies resolve
        var model = new RoadmapModel(new List<Activity>
        {
            new("Build API", "Q1 2025", "Infrastructure", new List<string>()),
            new("Design UI", "Q2 2025", "Frontend", new List<string> { "Build API" })
        });

        var errors = _validator.Validate(model);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_ActivityWithNullQuarter_ReturnsErrorNamingActivity()
    {
        var model = new RoadmapModel(new List<Activity>
        {
            new("Deploy Service", null, "Infrastructure", new List<string>())
        });

        var errors = _validator.Validate(model);

        Assert.Single(errors);
        Assert.Contains("Deploy Service", errors[0].Message);
        Assert.Contains("is missing a Quarter", errors[0].Message);
        Assert.Equal("Deploy Service", errors[0].ActivityLabel);
    }

    [Fact]
    public void Validate_ActivityWithWhitespaceCategory_ReturnsErrorNamingActivity()
    {
        var model = new RoadmapModel(new List<Activity>
        {
            new("Setup CI", "Q1 2025", " ", new List<string>())
        });

        var errors = _validator.Validate(model);

        Assert.Single(errors);
        Assert.Contains("Setup CI", errors[0].Message);
        Assert.Contains("has an empty Category value", errors[0].Message);
        Assert.Equal("Setup CI", errors[0].ActivityLabel);
    }

    [Fact]
    public void Validate_UnresolvedDependency_ReturnsErrorWithBothLabels()
    {
        var model = new RoadmapModel(new List<Activity>
        {
            new("Build Frontend", "Q2 2025", "Frontend", new List<string> { "Nonexistent Task" })
        });

        var errors = _validator.Validate(model);

        Assert.Single(errors);
        Assert.Contains("Build Frontend", errors[0].Message);
        Assert.Contains("Nonexistent Task", errors[0].Message);
        Assert.Contains("depends on", errors[0].Message);
        Assert.Contains("which does not exist", errors[0].Message);
    }

    [Fact]
    public void Validate_MultipleErrorsAcrossActivities_AllReported()
    {
        var model = new RoadmapModel(new List<Activity>
        {
            // Missing quarter
            new("Activity A", null, "Backend", new List<string>()),
            // Whitespace category
            new("Activity B", "Q1 2025", "  ", new List<string>()),
            // Unresolved dependency
            new("Activity C", "Q3 2025", "Frontend", new List<string> { "Ghost Task" })
        });

        var errors = _validator.Validate(model);

        // Should have exactly 3 errors - no short-circuiting
        Assert.Equal(3, errors.Count);

        // Verify each error is present
        Assert.Contains(errors, e => e.Message.Contains("Activity A") && e.Message.Contains("is missing a Quarter"));
        Assert.Contains(errors, e => e.Message.Contains("Activity B") && e.Message.Contains("has an empty Category value"));
        Assert.Contains(errors, e => e.Message.Contains("Activity C") && e.Message.Contains("depends on") && e.Message.Contains("Ghost Task"));
    }
}
