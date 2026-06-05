// Feature: drawio-to-markdown, Property 5: Default Output Path Derivation
using DrawioToMarkdown.Tests.Generators;
using FsCheck;
using FsCheck.Xunit;

namespace DrawioToMarkdown.Tests.Cli;

/// <summary>
/// Property 5: Default Output Path Derivation
/// Validates: Requirements 4.3
///
/// For any valid file path string with any extension, the default output path computation
/// SHALL produce a path identical to the input path except with the extension replaced by `.md`.
/// </summary>
public class OutputPathPropertyTests
{
    /// <summary>
    /// Provides the custom Arbitrary for file path strings to FsCheck.
    /// </summary>
    public static class Arbitraries
    {
        public static Arbitrary<string> FilePathArbitrary() =>
            ArbitraryGraphs.ArbFilePath();
    }

    /// <summary>
    /// **Validates: Requirements 4.3**
    ///
    /// The default output path derived via Path.ChangeExtension(path, ".md") ends with ".md".
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(Arbitraries) })]
    public bool DefaultOutputPathEndsWithMd(string filePath)
    {
        var result = Path.ChangeExtension(filePath, ".md");
        return result.EndsWith(".md", StringComparison.Ordinal);
    }

    /// <summary>
    /// **Validates: Requirements 4.3**
    ///
    /// The default output path preserves the directory portion of the original path.
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(Arbitraries) })]
    public bool DefaultOutputPathPreservesDirectory(string filePath)
    {
        var result = Path.ChangeExtension(filePath, ".md");
        var originalDir = Path.GetDirectoryName(filePath) ?? string.Empty;
        var resultDir = Path.GetDirectoryName(result) ?? string.Empty;
        return string.Equals(originalDir, resultDir, StringComparison.Ordinal);
    }

    /// <summary>
    /// **Validates: Requirements 4.3**
    ///
    /// The default output path preserves the filename (without extension) of the original path.
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(Arbitraries) })]
    public bool DefaultOutputPathPreservesFileNameWithoutExtension(string filePath)
    {
        var result = Path.ChangeExtension(filePath, ".md");
        var originalName = Path.GetFileNameWithoutExtension(filePath);
        var resultName = Path.GetFileNameWithoutExtension(result);
        return string.Equals(originalName, resultName, StringComparison.Ordinal);
    }
}
