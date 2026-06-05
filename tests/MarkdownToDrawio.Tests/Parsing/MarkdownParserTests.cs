using MarkdownToDrawio.Parsing;
using Xunit;

namespace MarkdownToDrawio.Tests.Parsing;

public class MarkdownParserTests
{
    private readonly MarkdownParser _parser = new();

    [Fact]
    public void Parse_ValidMarkdownWithMultipleActivities_ProducesCorrectModel()
    {
        var markdown = """
            # Dependency Documentation

            ## Design API

            ### Depends on

            No dependencies

            ### Quarter

            Q1 2025

            ### Category

            Infrastructure

            ## Implement Backend

            ### Depends on

            - [Design API](#design-api)

            ### Quarter

            Q2 2025

            ### Category

            Development

            ## Write Documentation

            ### Depends on

            - [Design API](#design-api)
            - [Implement Backend](#implement-backend)

            ### Quarter

            Q3 2025

            ### Category

            Documentation
            """;

        var model = _parser.Parse(markdown);

        Assert.Equal(3, model.Activities.Count);

        var designApi = model.Activities[0];
        Assert.Equal("Design API", designApi.Label);
        Assert.Equal("Q1 2025", designApi.Quarter);
        Assert.Equal("Infrastructure", designApi.Category);
        Assert.Empty(designApi.DependencyLabels);

        var implementBackend = model.Activities[1];
        Assert.Equal("Implement Backend", implementBackend.Label);
        Assert.Equal("Q2 2025", implementBackend.Quarter);
        Assert.Equal("Development", implementBackend.Category);
        Assert.Single(implementBackend.DependencyLabels);
        Assert.Equal("Design API", implementBackend.DependencyLabels[0]);

        var writeDocs = model.Activities[2];
        Assert.Equal("Write Documentation", writeDocs.Label);
        Assert.Equal("Q3 2025", writeDocs.Quarter);
        Assert.Equal("Documentation", writeDocs.Category);
        Assert.Equal(2, writeDocs.DependencyLabels.Count);
        Assert.Equal("Design API", writeDocs.DependencyLabels[0]);
        Assert.Equal("Implement Backend", writeDocs.DependencyLabels[1]);
    }

    [Fact]
    public void Parse_ActivityWithNoDependencies_HasEmptyDependencyList()
    {
        var markdown = """
            # Dependency Documentation

            ## Standalone Task

            ### Depends on

            No dependencies

            ### Quarter

            Q4 2024

            ### Category

            Planning
            """;

        var model = _parser.Parse(markdown);

        Assert.Single(model.Activities);
        var activity = model.Activities[0];
        Assert.Equal("Standalone Task", activity.Label);
        Assert.Empty(activity.DependencyLabels);
    }

    [Fact]
    public void Parse_ActivityWithMultipleDependencyLinks_ExtractsAllLabels()
    {
        var markdown = """
            # Dependency Documentation

            ## Final Review

            ### Depends on

            - [Alpha Task](#alpha-task)
            - [Beta Task](#beta-task)
            - [Gamma Task](#gamma-task)

            ### Quarter

            Q2 2025

            ### Category

            Review
            """;

        var model = _parser.Parse(markdown);

        Assert.Single(model.Activities);
        var activity = model.Activities[0];
        Assert.Equal(3, activity.DependencyLabels.Count);
        Assert.Equal("Alpha Task", activity.DependencyLabels[0]);
        Assert.Equal("Beta Task", activity.DependencyLabels[1]);
        Assert.Equal("Gamma Task", activity.DependencyLabels[2]);
    }

    [Fact]
    public void Parse_MarkdownWithNoLevel2Headings_ProducesEmptyActivityList()
    {
        var markdown = """
            # Dependency Documentation

            Some introductory text that has no activities.

            ### This is a level-3 heading but no level-2

            More text here.
            """;

        var model = _parser.Parse(markdown);

        Assert.Empty(model.Activities);
    }
}
