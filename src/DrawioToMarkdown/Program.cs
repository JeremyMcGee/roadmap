using System.CommandLine;
using System.CommandLine.Invocation;
using System.Xml;
using DrawioToMarkdown.Cli;
using DrawioToMarkdown.Model;
using DrawioToMarkdown.Output;
using DrawioToMarkdown.Parsing;
using DrawioToMarkdown.Resolution;

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

return await rootCommand.InvokeAsync(args);
