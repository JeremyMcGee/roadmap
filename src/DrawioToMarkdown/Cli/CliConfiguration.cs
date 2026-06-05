using System.CommandLine;

namespace DrawioToMarkdown.Cli;

/// <summary>
/// Configures the System.CommandLine root command for the drawio-to-markdown CLI.
/// Defines the required input file path argument, optional output file path argument,
/// and exposes them for handler wiring in Program.cs.
/// </summary>
public static class CliConfiguration
{
    /// <summary>
    /// Creates and returns a configured RootCommand with:
    /// - A required positional argument for the input draw.io file path
    /// - An optional positional argument for the output Markdown file path
    /// The help flag (-h, --help) is automatically provided by System.CommandLine.
    /// </summary>
    public static (RootCommand Command, Argument<string> InputArgument, Argument<string?> OutputArgument) CreateRootCommand()
    {
        var inputArgument = new Argument<string>(
            name: "input",
            description: "Path to the draw.io XML file to convert");

        var outputArgument = new Argument<string?>(
            name: "output",
            getDefaultValue: () => null,
            description: "Path for the output Markdown file (defaults to input file with .md extension)");

        var rootCommand = new RootCommand("Converts draw.io XML diagram files into Markdown dependency documentation")
        {
            inputArgument,
            outputArgument
        };

        return (rootCommand, inputArgument, outputArgument);
    }
}
