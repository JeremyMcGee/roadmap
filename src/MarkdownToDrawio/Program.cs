using System.CommandLine;
using System.CommandLine.Invocation;
using MarkdownToDrawio.Cli;
using MarkdownToDrawio.Output;
using MarkdownToDrawio.Parsing;
using MarkdownToDrawio.Validation;

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

    // Parse Markdown into RoadmapModel
    var parser = new MarkdownParser();
    var model = parser.Parse(content);

    // Validate parsed model has activities
    if (model.Activities.Count == 0)
    {
        await Console.Error.WriteLineAsync($"Error: No activities found in: {inputPath}");
        context.ExitCode = 1;
        return;
    }

    // Run validator and report all errors
    var validator = new RoadmapValidator();
    var errors = validator.Validate(model);

    if (errors.Count > 0)
    {
        foreach (var error in errors)
        {
            await Console.Error.WriteLineAsync($"Error: {error.Message}");
        }
        context.ExitCode = 1;
        return;
    }

    // Compute output path if not specified
    var resolvedOutputPath = outputPath ?? CliConfiguration.GetDefaultOutputPath(inputPath);

    // Validate output directory exists
    var outputDir = Path.GetDirectoryName(Path.GetFullPath(resolvedOutputPath));
    if (outputDir is not null && !Directory.Exists(outputDir))
    {
        await Console.Error.WriteLineAsync($"Error: Output directory not found: {outputDir}");
        context.ExitCode = 1;
        return;
    }

    // Generate Draw.IO XML
    var generator = new DiagramGenerator();
    var xml = generator.Generate(model);

    // Write output file, handling permission errors
    try
    {
        await File.WriteAllTextAsync(resolvedOutputPath, xml);
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
