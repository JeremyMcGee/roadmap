// Feature: markdown-to-drawio, Property 9: Default Output Path Derivation
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using MarkdownToDrawio.Cli;
using MarkdownToDrawio.Tests.Generators;

namespace MarkdownToDrawio.Tests.Cli;

/// <summary>
/// Property-based tests verifying that default output path derivation correctly
/// replaces the input file extension with .drawio.
/// **Validates: Requirements 4.3**
/// </summary>
public class OutputPathPropertyTests
{
    /// <summary>
    /// Provides the Arbitrary for file path strings.
    /// </summary>
    public static class Arbitraries
    {
        public static Arbitrary<string> StringArbitrary() =>
            ArbitraryRoadmaps.GenFilePath().ToArbitrary();
    }

    /// <summary>
    /// For any valid file path string with any extension, the default output path
    /// computation SHALL produce a path identical to the input path except with
    /// the extension replaced by .drawio.
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(Arbitraries) })]
    public bool DefaultOutputPath_ReplacesExtensionWithDrawio(string inputPath)
    {
        var result = CliConfiguration.GetDefaultOutputPath(inputPath);
        var expected = Path.ChangeExtension(inputPath, ".drawio");
        return result == expected;
    }
}
