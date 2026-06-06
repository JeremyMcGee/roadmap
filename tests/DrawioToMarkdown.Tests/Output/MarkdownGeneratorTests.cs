using Xunit;
using DrawioToMarkdown.Model;
using DrawioToMarkdown.Output;

namespace DrawioToMarkdown.Tests.Output;

public class MarkdownGeneratorTests
{
    private readonly MarkdownGenerator _sut = new();

    [Fact]
    public void Generate_ContainsCorrectHeading()
    {
        var model = new RoadmapModel(new[]
        {
            new RoadmapActivity("Task A", "Q1 2025", "Infrastructure", Array.Empty<string>())
        });

        var result = _sut.Generate(model);

        Assert.StartsWith("# Dependency Documentation", result);
    }

    [Fact]
    public void Generate_NodesWithDependencies_ListsPrerequisites()
    {
        var model = new RoadmapModel(new[]
        {
            new RoadmapActivity("Task A", "Q1 2025", "Infrastructure", Array.Empty<string>()),
            new RoadmapActivity("Task B", "Q2 2025", "Platform", new[] { "Task A" })
        });

        var result = _sut.Generate(model);

        Assert.Contains("### Depends on", result);
        Assert.Contains("- [Task A](#task-a)", result);
    }

    [Fact]
    public void Generate_NodesWithoutDependencies_ShowsNoDependencies()
    {
        var model = new RoadmapModel(new[]
        {
            new RoadmapActivity("Solo Task", "Q1 2025", "Infrastructure", Array.Empty<string>())
        });

        var result = _sut.Generate(model);

        Assert.Contains("No dependencies", result);
    }

    [Fact]
    public void Generate_NodesSortedAlphabetically()
    {
        var model = new RoadmapModel(new[]
        {
            new RoadmapActivity("Zebra", "Q1 2025", "Infrastructure", Array.Empty<string>()),
            new RoadmapActivity("Alpha", "Q2 2025", "Platform", Array.Empty<string>()),
            new RoadmapActivity("Middle", "Q3 2025", "Security", Array.Empty<string>())
        });

        var result = _sut.Generate(model);

        var alphaIndex = result.IndexOf("## Alpha");
        var middleIndex = result.IndexOf("## Middle");
        var zebraIndex = result.IndexOf("## Zebra");

        Assert.True(alphaIndex < middleIndex, "Alpha should appear before Middle");
        Assert.True(middleIndex < zebraIndex, "Middle should appear before Zebra");
    }

    // --- New tests for task 5.3 ---

    /// <summary>
    /// Validates Requirement 4.3: Each activity has sub-sections in the fixed order
    /// "### Depends on", "### Quarter", "### Category".
    /// </summary>
    [Fact]
    public void Generate_SectionOrdering_DependsOnThenQuarterThenCategory()
    {
        var model = new RoadmapModel(new[]
        {
            new RoadmapActivity("My Activity", "Q2 2025", "Platform", new[] { "Other" })
        });

        var result = _sut.Generate(model);

        var dependsOnIndex = result.IndexOf("### Depends on");
        var quarterIndex = result.IndexOf("### Quarter");
        var categoryIndex = result.IndexOf("### Category");

        Assert.True(dependsOnIndex >= 0, "### Depends on heading should be present");
        Assert.True(quarterIndex >= 0, "### Quarter heading should be present");
        Assert.True(categoryIndex >= 0, "### Category heading should be present");
        Assert.True(dependsOnIndex < quarterIndex, "### Depends on should appear before ### Quarter");
        Assert.True(quarterIndex < categoryIndex, "### Quarter should appear before ### Category");
    }

    /// <summary>
    /// Validates Requirement 4.4: Anchor generation lowercases the label and replaces spaces with hyphens.
    /// </summary>
    [Theory]
    [InlineData("Simple Task", "simple-task")]
    [InlineData("ALL CAPS LABEL", "all-caps-label")]
    [InlineData("MiXeD CaSe", "mixed-case")]
    [InlineData("Multiple   Spaces", "multiple---spaces")] // each space replaced individually
    [InlineData("Already-Hyphenated", "already-hyphenated")]
    [InlineData("Special!Chars@Here", "special!chars@here")] // non-alpha kept, only spaces become hyphens
    public void Generate_AnchorGeneration_LowercasesAndReplacesSpaces(string label, string expectedAnchor)
    {
        var model = new RoadmapModel(new[]
        {
            new RoadmapActivity("Dependent", "Q1 2025", "Infra", new[] { label }),
            new RoadmapActivity(label, "Q1 2025", "Infra", Array.Empty<string>())
        });

        var result = _sut.Generate(model);

        Assert.Contains($"(#{expectedAnchor})", result);
    }

    /// <summary>
    /// Validates Requirements 4.6, 4.7: Quarter and Category values are rendered after their headings.
    /// </summary>
    [Fact]
    public void Generate_QuarterAndCategoryValuesRendered()
    {
        var model = new RoadmapModel(new[]
        {
            new RoadmapActivity("Build Pipeline", "Q3 2026", "DevOps", Array.Empty<string>())
        });

        var result = _sut.Generate(model);

        // Quarter value appears after ### Quarter heading
        var quarterHeadingIndex = result.IndexOf("### Quarter");
        var quarterValueIndex = result.IndexOf("Q3 2026", quarterHeadingIndex);
        Assert.True(quarterValueIndex > quarterHeadingIndex, "Quarter value should appear after ### Quarter heading");

        // Category value appears after ### Category heading
        var categoryHeadingIndex = result.IndexOf("### Category");
        var categoryValueIndex = result.IndexOf("DevOps", categoryHeadingIndex);
        Assert.True(categoryValueIndex > categoryHeadingIndex, "Category value should appear after ### Category heading");
    }

    /// <summary>
    /// Validates Requirement 4.8: Null quarter throws InvalidOperationException.
    /// </summary>
    [Fact]
    public void Generate_NullQuarter_ThrowsInvalidOperationException()
    {
        var model = new RoadmapModel(new[]
        {
            new RoadmapActivity("Broken Task", null!, "Platform", Array.Empty<string>())
        });

        var ex = Assert.Throws<InvalidOperationException>(() => _sut.Generate(model));
        Assert.Contains("Broken Task", ex.Message);
        Assert.Contains("quarter", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Validates Requirement 4.8: Empty quarter throws InvalidOperationException.
    /// </summary>
    [Fact]
    public void Generate_EmptyQuarter_ThrowsInvalidOperationException()
    {
        var model = new RoadmapModel(new[]
        {
            new RoadmapActivity("No Quarter Task", "", "Security", Array.Empty<string>())
        });

        var ex = Assert.Throws<InvalidOperationException>(() => _sut.Generate(model));
        Assert.Contains("No Quarter Task", ex.Message);
        Assert.Contains("quarter", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Validates Requirement 4.9: Null category throws InvalidOperationException.
    /// </summary>
    [Fact]
    public void Generate_NullCategory_ThrowsInvalidOperationException()
    {
        var model = new RoadmapModel(new[]
        {
            new RoadmapActivity("Bad Category", "Q1 2025", null!, Array.Empty<string>())
        });

        var ex = Assert.Throws<InvalidOperationException>(() => _sut.Generate(model));
        Assert.Contains("Bad Category", ex.Message);
        Assert.Contains("category", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Validates Requirement 4.9: Empty category throws InvalidOperationException.
    /// </summary>
    [Fact]
    public void Generate_EmptyCategory_ThrowsInvalidOperationException()
    {
        var model = new RoadmapModel(new[]
        {
            new RoadmapActivity("Empty Cat", "Q2 2025", "", Array.Empty<string>())
        });

        var ex = Assert.Throws<InvalidOperationException>(() => _sut.Generate(model));
        Assert.Contains("Empty Cat", ex.Message);
        Assert.Contains("category", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Validates Requirement 4.4: Dependencies within an activity are sorted lexicographically (ordinal).
    /// </summary>
    [Fact]
    public void Generate_DependenciesSortedLexicographically()
    {
        var model = new RoadmapModel(new[]
        {
            new RoadmapActivity("Alpha", "Q1 2025", "Infra", Array.Empty<string>()),
            new RoadmapActivity("Beta", "Q1 2025", "Infra", Array.Empty<string>()),
            new RoadmapActivity("Gamma", "Q1 2025", "Infra", Array.Empty<string>()),
            new RoadmapActivity("Main Task", "Q2 2025", "Platform",
                new[] { "Gamma", "Alpha", "Beta" }) // unsorted input
        });

        var result = _sut.Generate(model);

        // Find the "## Main Task" section and extract dependency list
        var mainTaskIndex = result.IndexOf("## Main Task");
        var dependsOnIndex = result.IndexOf("### Depends on", mainTaskIndex);
        var quarterIndex = result.IndexOf("### Quarter", mainTaskIndex);

        // Extract the dependencies block between ### Depends on and ### Quarter
        var depsBlock = result.Substring(dependsOnIndex, quarterIndex - dependsOnIndex);

        var alphaIdx = depsBlock.IndexOf("[Alpha]");
        var betaIdx = depsBlock.IndexOf("[Beta]");
        var gammaIdx = depsBlock.IndexOf("[Gamma]");

        Assert.True(alphaIdx < betaIdx, "Alpha should appear before Beta in dependency list");
        Assert.True(betaIdx < gammaIdx, "Beta should appear before Gamma in dependency list");
    }

    /// <summary>
    /// Full output format verification: verifies exact byte-for-byte output for a known input
    /// to catch any formatting regressions.
    /// Validates Requirements 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 4.7.
    /// </summary>
    [Fact]
    public void Generate_FullOutputFormat_ExactMatch()
    {
        var model = new RoadmapModel(new[]
        {
            new RoadmapActivity("Setup CI", "Q1 2025", "Infrastructure",
                new[] { "Design API" }),
            new RoadmapActivity("Design API", "Q1 2025", "Platform",
                Array.Empty<string>())
        });

        var result = _sut.Generate(model);

        var expected =
            "# Dependency Documentation\r\n" +
            "\r\n" +
            "## Design API\r\n" +
            "\r\n" +
            "### Depends on\r\n" +
            "\r\n" +
            "No dependencies\r\n" +
            "\r\n" +
            "### Quarter\r\n" +
            "\r\n" +
            "Q1 2025\r\n" +
            "\r\n" +
            "### Category\r\n" +
            "\r\n" +
            "Platform\r\n" +
            "\r\n" +
            "## Setup CI\r\n" +
            "\r\n" +
            "### Depends on\r\n" +
            "\r\n" +
            "- [Design API](#design-api)\r\n" +
            "\r\n" +
            "### Quarter\r\n" +
            "\r\n" +
            "Q1 2025\r\n" +
            "\r\n" +
            "### Category\r\n" +
            "\r\n" +
            "Infrastructure\r\n";

        Assert.Equal(expected, result);
    }
}
