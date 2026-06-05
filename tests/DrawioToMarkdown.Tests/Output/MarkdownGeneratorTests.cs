using Xunit;
using DrawioToMarkdown.Graph;
using DrawioToMarkdown.Output;

namespace DrawioToMarkdown.Tests.Output;

public class MarkdownGeneratorTests
{
    private readonly MarkdownGenerator _sut = new();

    [Fact]
    public void Generate_ContainsCorrectHeading()
    {
        var graph = new DependencyGraph(
            nodes: new Dictionary<string, ActivityNode>
            {
                ["1"] = new ActivityNode("1", "Task A")
            },
            dependencies: new Dictionary<string, IReadOnlySet<string>>
            {
                ["1"] = new HashSet<string>()
            });

        var result = _sut.Generate(graph);

        Assert.StartsWith("# Dependency Documentation", result);
    }

    [Fact]
    public void Generate_NodesWithDependencies_ListsPrerequisites()
    {
        var graph = new DependencyGraph(
            nodes: new Dictionary<string, ActivityNode>
            {
                ["1"] = new ActivityNode("1", "Task A"),
                ["2"] = new ActivityNode("2", "Task B")
            },
            dependencies: new Dictionary<string, IReadOnlySet<string>>
            {
                ["1"] = new HashSet<string>(),
                ["2"] = new HashSet<string> { "1" }
            });

        var result = _sut.Generate(graph);

        Assert.Contains("### Depends on", result);
        Assert.Contains("- Task A", result);
    }

    [Fact]
    public void Generate_NodesWithoutDependencies_ShowsNoDependencies()
    {
        var graph = new DependencyGraph(
            nodes: new Dictionary<string, ActivityNode>
            {
                ["1"] = new ActivityNode("1", "Solo Task")
            },
            dependencies: new Dictionary<string, IReadOnlySet<string>>
            {
                ["1"] = new HashSet<string>()
            });

        var result = _sut.Generate(graph);

        Assert.Contains("No dependencies", result);
    }

    [Fact]
    public void Generate_NodesSortedAlphabetically()
    {
        var graph = new DependencyGraph(
            nodes: new Dictionary<string, ActivityNode>
            {
                ["1"] = new ActivityNode("1", "Zebra"),
                ["2"] = new ActivityNode("2", "Alpha"),
                ["3"] = new ActivityNode("3", "Middle")
            },
            dependencies: new Dictionary<string, IReadOnlySet<string>>
            {
                ["1"] = new HashSet<string>(),
                ["2"] = new HashSet<string>(),
                ["3"] = new HashSet<string>()
            });

        var result = _sut.Generate(graph);

        var alphaIndex = result.IndexOf("## Alpha");
        var middleIndex = result.IndexOf("## Middle");
        var zebraIndex = result.IndexOf("## Zebra");

        Assert.True(alphaIndex < middleIndex, "Alpha should appear before Middle");
        Assert.True(middleIndex < zebraIndex, "Middle should appear before Zebra");
    }
}
