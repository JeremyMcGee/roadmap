using System.CommandLine;

namespace MarkdownToDrawio.Cli;

/// <summary>
/// Configures the System.CommandLine root command for the markdown-to-drawio CLI.
/// Defines the required input file path argument, optional output file path argument,
/// and exposes them for handler wiring in Program.cs.
/// </summary>
public static class CliConfiguration
{
    /// <summary>
    /// Creates and returns a configured RootCommand with:
    /// - A required positional argument for the input Markdown file path
    /// - An optional positional argument for the output Draw.IO file path
    /// The help flag (-h, --help) is automatically provided by System.CommandLine.
    /// </summary>
    public static (RootCommand Command, Argument<string> InputArgument, Argument<string?> OutputArgument) CreateRootCommand()
    {
        var inputArgument = new Argument<string>(
            name: "input",
            description: "Path to the Markdown file to convert");

        var outputArgument = new Argument<string?>(
            name: "output",
            getDefaultValue: () => null,
            description: "Path for the output Draw.IO file (defaults to input file with .drawio extension)");

        var rootCommand = new RootCommand("Converts Markdown roadmap documentation into Draw.IO XML diagram files.")
        {
            inputArgument,
            outputArgument
        };

        return (rootCommand, inputArgument, outputArgument);
    }

    /// <summary>
    /// Computes the default output path by replacing the input file's extension with .drawio.
    /// </summary>
    /// <param name="inputPath">The input file path.</param>
    /// <returns>The input path with its extension replaced by .drawio.</returns>
    public static string GetDefaultOutputPath(string inputPath)
    {
        return Path.ChangeExtension(inputPath, ".drawio");
    }
}
