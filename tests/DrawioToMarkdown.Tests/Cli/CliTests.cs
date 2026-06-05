using System.CommandLine;
using System.CommandLine.Invocation;
using System.Xml;
using DrawioToMarkdown.Cli;
using DrawioToMarkdown.Graph;
using DrawioToMarkdown.Output;
using DrawioToMarkdown.Parsing;
using Xunit;

namespace DrawioToMarkdown.Tests.Cli;

/// <summary>
/// Unit tests for CLI behavior: help flag, missing input file, default output path,
/// and non-existent output directory.
/// </summary>
public class CliTests : IDisposable
{
    private readonly List<string> _tempFiles = new();
    private readonly List<string> _tempDirs = new();

    private const string ValidDrawioXml =
        """<mxfile><diagram name="Page-1"><mxGraphModel><root><mxCell id="0" /><mxCell id="1" parent="0" /><mxCell id="2" value="Task A" vertex="1" parent="1" /></root></mxGraphModel></diagram></mxfile>""";

    /// <summary>
    /// Sets up a RootCommand with the same handler wiring as Program.cs
    /// so we can test the full CLI pipeline programmatically.
    /// </summary>
    private static RootCommand CreateWiredCommand()
    {
        var (rootCommand, inputArgument, outputArgument) = CliConfiguration.CreateRootCommand();

        rootCommand.SetHandler(async (InvocationContext context) =>
        {
            var inputPath = context.ParseResult.GetValueForArgument(inputArgument);
            var outputPath = context.ParseResult.GetValueForArgument(outputArgument);

            // Validate input file exists
            if (!File.Exists(inputPath))
            {
                await Console.Error.WriteLineAsync($"Error: File not found: {inputPath}");
                context.ExitCode = 1;
                return;
            }

            // Read file content
            var content = await File.ReadAllTextAsync(inputPath);

            // Parse XML
            ParsedDiagram parsed;
            try
            {
                var parser = new DrawioParser();
                parsed = parser.Parse(content);
            }
            catch (XmlException)
            {
                await Console.Error.WriteLineAsync($"Error: Malformed XML in file: {inputPath}");
                context.ExitCode = 1;
                return;
            }

            // Validate parsed diagram has nodes
            if (parsed.Nodes.Count == 0)
            {
                await Console.Error.WriteLineAsync($"Error: No activities found in: {inputPath}");
                context.ExitCode = 1;
                return;
            }

            // Build graph
            var graphBuilder = new GraphBuilder();
            var graph = graphBuilder.Build(parsed);

            // Generate markdown
            var generator = new MarkdownGenerator();
            var markdown = generator.Generate(graph);

            // Compute output path if not specified
            var resolvedOutputPath = outputPath ?? Path.ChangeExtension(inputPath, ".md");

            // Validate output directory exists
            var outputDir = Path.GetDirectoryName(Path.GetFullPath(resolvedOutputPath));
            if (outputDir is not null && !Directory.Exists(outputDir))
            {
                await Console.Error.WriteLineAsync($"Error: Output directory not found: {outputDir}");
                context.ExitCode = 1;
                return;
            }

            // Write markdown to output file
            await File.WriteAllTextAsync(resolvedOutputPath, markdown);
            context.ExitCode = 0;
        });

        return rootCommand;
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public async Task HelpFlag_PrintsUsageAndExits0(string helpArg)
    {
        // The help flag is handled automatically by System.CommandLine
        var (rootCommand, _, _) = CliConfiguration.CreateRootCommand();

        var exitCode = await rootCommand.InvokeAsync(new[] { helpArg });

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task MissingInputFile_ExitsWithNonZeroCode()
    {
        var rootCommand = CreateWiredCommand();
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "missing.drawio");

        var exitCode = await rootCommand.InvokeAsync(new[] { nonExistentPath });

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task DefaultOutputPath_ReplacesExtensionWithMd()
    {
        var rootCommand = CreateWiredCommand();

        // Create a temp directory and a .drawio file with valid content
        var tempDir = Path.Combine(Path.GetTempPath(), $"cli_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        _tempDirs.Add(tempDir);

        var inputPath = Path.Combine(tempDir, "diagram.drawio");
        await File.WriteAllTextAsync(inputPath, ValidDrawioXml);
        _tempFiles.Add(inputPath);

        var expectedOutputPath = Path.Combine(tempDir, "diagram.md");

        var exitCode = await rootCommand.InvokeAsync(new[] { inputPath });

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(expectedOutputPath), $"Expected output file at: {expectedOutputPath}");
        _tempFiles.Add(expectedOutputPath);
    }

    [Fact]
    public async Task NonExistentOutputDirectory_ExitsWithNonZeroCode()
    {
        var rootCommand = CreateWiredCommand();

        // Create a temp .drawio file with valid content in an existing directory
        var tempDir = Path.Combine(Path.GetTempPath(), $"cli_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        _tempDirs.Add(tempDir);

        var inputPath = Path.Combine(tempDir, "diagram.drawio");
        await File.WriteAllTextAsync(inputPath, ValidDrawioXml);
        _tempFiles.Add(inputPath);

        // Specify an output path in a directory that doesn't exist
        var nonExistentOutputPath = Path.Combine(tempDir, "nonexistent_subdir", "output.md");

        var exitCode = await rootCommand.InvokeAsync(new[] { inputPath, nonExistentOutputPath });

        Assert.Equal(1, exitCode);
    }

    public void Dispose()
    {
        foreach (var file in _tempFiles)
        {
            try { if (File.Exists(file)) File.Delete(file); } catch { }
        }
        foreach (var dir in _tempDirs)
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); } catch { }
        }
    }
}
