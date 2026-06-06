using System.CommandLine;
using System.CommandLine.Invocation;
using System.Xml;
using DrawioToMarkdown.Cli;
using DrawioToMarkdown.Model;
using DrawioToMarkdown.Output;
using DrawioToMarkdown.Parsing;
using DrawioToMarkdown.Resolution;
using Xunit;

namespace DrawioToMarkdown.Tests.Cli;

/// <summary>
/// Unit tests for CLI behavior: help flag, missing input file, malformed XML,
/// unrecognized structure, missing swimlanes, missing quarter columns,
/// output directory not found, and successful conversion.
/// </summary>
public class CliTests : IDisposable
{
    private readonly List<string> _tempFiles = new();
    private readonly List<string> _tempDirs = new();

    /// <summary>
    /// Valid draw.io XML containing swimlanes, quarter columns, and an activity node
    /// so that it passes through the full pipeline (DrawioParser → PositionResolver → MarkdownGenerator).
    /// </summary>
    private const string ValidDrawioXml =
        """
        <mxfile><diagram name="Page-1"><mxGraphModel><root>
        <mxCell id="0" />
        <mxCell id="1" parent="0" />
        <mxCell id="lane1" value="Infrastructure" style="shape=swimlane;horizontal=0;startSize=30;" vertex="1" parent="1"><mxGeometry x="0" y="0" width="800" height="200" /></mxCell>
        <mxCell id="qlabel_q1" value="Q1 2025" vertex="1" parent="1"><mxGeometry x="100" y="0" width="200" height="30" /></mxCell>
        <mxCell id="act1" value="Task A" style="rounded=1;whiteSpace=wrap;html=1" vertex="1" parent="1"><mxGeometry x="150" y="80" width="100" height="40" /></mxCell>
        </root></mxGraphModel></diagram></mxfile>
        """;

    /// <summary>
    /// Valid draw.io XML with an activity and a swimlane but NO quarter columns (no qlabel_ elements).
    /// This should trigger the "No quarter columns" error from PositionResolver.
    /// </summary>
    private const string XmlWithSwimlaneButNoQuarters =
        """
        <mxfile><diagram name="Page-1"><mxGraphModel><root>
        <mxCell id="0" />
        <mxCell id="1" parent="0" />
        <mxCell id="lane1" value="Infrastructure" style="shape=swimlane;horizontal=0;startSize=30;" vertex="1" parent="1"><mxGeometry x="0" y="0" width="800" height="200" /></mxCell>
        <mxCell id="act1" value="Task A" style="rounded=1;whiteSpace=wrap;html=1" vertex="1" parent="1"><mxGeometry x="150" y="80" width="100" height="40" /></mxCell>
        </root></mxGraphModel></diagram></mxfile>
        """;

    /// <summary>
    /// Valid draw.io XML with an activity and a quarter column but NO swimlanes.
    /// This should trigger the "No category swimlanes" error from PositionResolver.
    /// </summary>
    private const string XmlWithQuarterButNoSwimlanes =
        """
        <mxfile><diagram name="Page-1"><mxGraphModel><root>
        <mxCell id="0" />
        <mxCell id="1" parent="0" />
        <mxCell id="qlabel_q1" value="Q1 2025" vertex="1" parent="1"><mxGeometry x="100" y="0" width="200" height="30" /></mxCell>
        <mxCell id="act1" value="Task A" style="rounded=1;whiteSpace=wrap;html=1" vertex="1" parent="1"><mxGeometry x="150" y="80" width="100" height="40" /></mxCell>
        </root></mxGraphModel></diagram></mxfile>
        """;

    /// <summary>
    /// Valid XML but without the expected mxfile/diagram/mxGraphModel/root structure.
    /// This should trigger the "Not a recognized draw.io diagram" error.
    /// </summary>
    private const string XmlWithoutMxfileStructure =
        """<root><data>Not a draw.io file</data></root>""";

    /// <summary>
    /// Malformed XML content that cannot be parsed.
    /// </summary>
    private const string MalformedXml = """<mxfile><broken><unclosed""";

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
            catch (InvalidOperationException)
            {
                await Console.Error.WriteLineAsync($"Error: Not a recognized draw.io diagram: {inputPath}");
                context.ExitCode = 1;
                return;
            }

            // Run PositionResolver
            RoadmapModel model;
            try
            {
                var resolver = new PositionResolver();
                model = resolver.Resolve(parsed);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("No category swimlanes"))
            {
                await Console.Error.WriteLineAsync($"Error: No category swimlanes detected in: {inputPath}");
                context.ExitCode = 1;
                return;
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("No quarter columns"))
            {
                await Console.Error.WriteLineAsync($"Error: No quarter columns detected in: {inputPath}");
                context.ExitCode = 1;
                return;
            }

            // Generate markdown
            string markdown;
            try
            {
                var generator = new MarkdownGenerator();
                markdown = generator.Generate(model);
            }
            catch (InvalidOperationException ex)
            {
                await Console.Error.WriteLineAsync(ex.Message);
                context.ExitCode = 1;
                return;
            }

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
            try
            {
                await File.WriteAllTextAsync(resolvedOutputPath, markdown);
            }
            catch (UnauthorizedAccessException)
            {
                await Console.Error.WriteLineAsync($"Error: Cannot write to file: {resolvedOutputPath}");
                context.ExitCode = 1;
                return;
            }

            context.ExitCode = 0;
        });

        return rootCommand;
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public async Task HelpFlag_PrintsUsageAndExits0(string helpArg)
    {
        var (rootCommand, _, _) = CliConfiguration.CreateRootCommand();

        var exitCode = await rootCommand.InvokeAsync(new[] { helpArg });

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task MissingInputFile_ExitsWithCode1()
    {
        var rootCommand = CreateWiredCommand();
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "missing.drawio");

        var exitCode = await rootCommand.InvokeAsync(new[] { nonExistentPath });

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task MalformedXml_ExitsWithCode1()
    {
        var rootCommand = CreateWiredCommand();

        var tempDir = CreateTempDir();
        var inputPath = Path.Combine(tempDir, "bad.drawio");
        await File.WriteAllTextAsync(inputPath, MalformedXml);
        _tempFiles.Add(inputPath);

        var exitCode = await rootCommand.InvokeAsync(new[] { inputPath });

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task MissingMxfileStructure_ExitsWithCode1()
    {
        var rootCommand = CreateWiredCommand();

        var tempDir = CreateTempDir();
        var inputPath = Path.Combine(tempDir, "not_drawio.xml");
        await File.WriteAllTextAsync(inputPath, XmlWithoutMxfileStructure);
        _tempFiles.Add(inputPath);

        var exitCode = await rootCommand.InvokeAsync(new[] { inputPath });

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task MissingSwimlanes_ExitsWithCode1()
    {
        var rootCommand = CreateWiredCommand();

        var tempDir = CreateTempDir();
        var inputPath = Path.Combine(tempDir, "no_swimlanes.drawio");
        await File.WriteAllTextAsync(inputPath, XmlWithQuarterButNoSwimlanes);
        _tempFiles.Add(inputPath);

        var exitCode = await rootCommand.InvokeAsync(new[] { inputPath });

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task MissingQuarterColumns_ExitsWithCode1()
    {
        var rootCommand = CreateWiredCommand();

        var tempDir = CreateTempDir();
        var inputPath = Path.Combine(tempDir, "no_quarters.drawio");
        await File.WriteAllTextAsync(inputPath, XmlWithSwimlaneButNoQuarters);
        _tempFiles.Add(inputPath);

        var exitCode = await rootCommand.InvokeAsync(new[] { inputPath });

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task NonExistentOutputDirectory_ExitsWithCode1()
    {
        var rootCommand = CreateWiredCommand();

        var tempDir = CreateTempDir();
        var inputPath = Path.Combine(tempDir, "diagram.drawio");
        await File.WriteAllTextAsync(inputPath, ValidDrawioXml);
        _tempFiles.Add(inputPath);

        // Specify an output path in a directory that doesn't exist
        var nonExistentOutputPath = Path.Combine(tempDir, "nonexistent_subdir", "output.md");

        var exitCode = await rootCommand.InvokeAsync(new[] { inputPath, nonExistentOutputPath });

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task SuccessfulConversion_ExitsWithCode0AndCreatesOutputFile()
    {
        var rootCommand = CreateWiredCommand();

        var tempDir = CreateTempDir();
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
    public async Task DefaultOutputPath_ReplacesExtensionWithMd()
    {
        var rootCommand = CreateWiredCommand();

        var tempDir = CreateTempDir();
        var inputPath = Path.Combine(tempDir, "roadmap.drawio");
        await File.WriteAllTextAsync(inputPath, ValidDrawioXml);
        _tempFiles.Add(inputPath);

        var expectedOutputPath = Path.Combine(tempDir, "roadmap.md");

        var exitCode = await rootCommand.InvokeAsync(new[] { inputPath });

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(expectedOutputPath), $"Expected output file at: {expectedOutputPath}");
        _tempFiles.Add(expectedOutputPath);
    }

    private string CreateTempDir()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"cli_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        _tempDirs.Add(tempDir);
        return tempDir;
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
