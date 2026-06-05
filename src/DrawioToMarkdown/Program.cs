using System.CommandLine;
using System.CommandLine.Invocation;
using System.Xml;
using DrawioToMarkdown.Cli;
using DrawioToMarkdown.Graph;
using DrawioToMarkdown.Output;
using DrawioToMarkdown.Parsing;

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

return await rootCommand.InvokeAsync(args);
