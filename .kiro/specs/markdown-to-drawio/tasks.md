# Implementation Plan: markdown-to-drawio

## Overview

Implement a .NET 10 console application that converts Markdown roadmap documentation into Draw.IO XML diagram files. The tool parses Markdown with activity metadata (Quarter, Category, Dependencies), validates completeness, and generates Draw.IO XML with horizontal swimlanes and vertical quarter columns. The architecture follows a pipeline pattern: Parse → Validate → Generate.

## Tasks

- [x] 1. Set up project structure and core data models
  - [x] 1.1 Create the MarkdownToDrawio project with csproj and directory structure
    - Create `src/MarkdownToDrawio/MarkdownToDrawio.csproj` targeting `net10.0` with `System.CommandLine` 2.0.0-beta4.22272.1
    - Create directory structure: `Cli/`, `Parsing/`, `Validation/`, `Model/`, `Output/`
    - Create `Program.cs` with minimal entry point
    - _Requirements: 4.1_

  - [x] 1.2 Define domain model records
    - Create `Model/Activity.cs` with `sealed record Activity(string Label, string? Quarter, string? Category, IReadOnlyList<string> DependencyLabels)`
    - Create `Model/RoadmapModel.cs` with `sealed record RoadmapModel(IReadOnlyList<Activity> Activities)`
    - Create `Validation/ValidationError.cs` with `sealed record ValidationError(string ActivityLabel, string Message)`
    - _Requirements: 1.1, 1.2, 2.1_

  - [x] 1.3 Define interfaces for pipeline stages
    - Create `Parsing/IMarkdownParser.cs` with `RoadmapModel Parse(string markdownContent)` method
    - Create `Validation/IValidator.cs` with `IReadOnlyList<ValidationError> Validate(RoadmapModel model)` method
    - Create `Output/IDiagramGenerator.cs` with `string Generate(RoadmapModel model)` method
    - _Requirements: 1.1, 2.1, 3.1_

  - [x] 1.4 Create the test project with csproj and directory structure
    - Create `tests/MarkdownToDrawio.Tests/MarkdownToDrawio.Tests.csproj` with xunit 2.9.3, FsCheck.Xunit 3.1.0, Microsoft.NET.Test.Sdk 17.13.0, xunit.runner.visualstudio 2.8.2
    - Add project reference to `src/MarkdownToDrawio/MarkdownToDrawio.csproj`
    - Create directory structure: `Parsing/`, `Validation/`, `Output/`, `Cli/`, `Generators/`
    - _Requirements: 5.1, 5.2_

- [x] 2. Implement Markdown Parser
  - [x] 2.1 Implement MarkdownParser
    - Create `Parsing/MarkdownParser.cs` implementing `IMarkdownParser`
    - Parse level-2 headings (`## `) as Activity boundaries, extract heading text as Activity label
    - Within each activity section, parse level-3 headings: `### Depends on` (extract bullet link texts or "No dependencies"), `### Quarter` (extract following line), `### Category` (extract following line)
    - Return `RoadmapModel` with all extracted activities
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6_

  - [ ]* 2.2 Write unit tests for MarkdownParser
    - Create `tests/MarkdownToDrawio.Tests/Parsing/MarkdownParserTests.cs`
    - Test: valid Markdown with multiple activities produces correct model
    - Test: activity with "No dependencies" has empty dependency list
    - Test: activity with multiple dependency links extracts all labels
    - Test: Markdown with no level-2 headings produces empty activity list
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6_

- [x] 3. Implement Markdown Pretty-Printer and Round-Trip Support
  - [x] 3.1 Implement MarkdownPrettyPrinter
    - Create `Parsing/MarkdownPrettyPrinter.cs`
    - Produce canonical Markdown: `# Dependency Documentation` heading, activities sorted lexicographically by label
    - Within each activity: `### Depends on` (bullet links sorted lexicographically, or "No dependencies"), `### Quarter`, `### Category`
    - Ensure `Parse(Print(model))` reproduces the same model
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5_

  - [ ]* 3.2 Write property test for Parse/Print round-trip
    - Create `tests/MarkdownToDrawio.Tests/Parsing/RoundTripPropertyTests.cs`
    - **Property 1: Parse/Print Round-Trip**
    - Generate arbitrary valid `RoadmapModel` instances (unique non-empty labels, non-empty quarter/category, valid dependency references)
    - Assert `Parse(Print(model))` produces equivalent model
    - Minimum 100 iterations
    - **Validates: Requirements 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 5.1, 5.2, 5.3, 5.4, 5.5**

- [x] 4. Implement Roadmap Validator
  - [x] 4.1 Implement RoadmapValidator
    - Create `Validation/RoadmapValidator.cs` implementing `IValidator`
    - Check all activities for: missing Quarter heading (null), missing Category heading (null), empty/whitespace Quarter value, empty/whitespace Category value
    - Check all dependency labels resolve to an existing activity label in the model
    - Collect all errors into a list (do not short-circuit)
    - Return `IReadOnlyList<ValidationError>` with descriptive messages
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6_

  - [ ]* 4.2 Write property test for metadata validation
    - Create `tests/MarkdownToDrawio.Tests/Validation/MetadataValidationPropertyTests.cs`
    - **Property 2: Validation Detects All Metadata Errors**
    - Generate `RoadmapModel` with known subset of activities having null/whitespace Quarter or Category
    - Assert validator returns an error for every such activity
    - Assert total metadata error count matches expected count
    - Minimum 100 iterations
    - **Validates: Requirements 2.1, 2.2, 2.3, 2.4, 2.6**

  - [ ]* 4.3 Write property test for unresolved dependency validation
    - Create `tests/MarkdownToDrawio.Tests/Validation/DependencyValidationPropertyTests.cs`
    - **Property 3: Validation Detects All Unresolved Dependencies**
    - Generate `RoadmapModel` with some dependency labels referencing non-existent activities
    - Assert validator returns an error for each unresolved reference
    - Assert each error identifies both the referring activity and the unresolved target
    - Minimum 100 iterations
    - **Validates: Requirements 2.5, 2.6**

  - [ ]* 4.4 Write unit tests for RoadmapValidator
    - Create `tests/MarkdownToDrawio.Tests/Validation/RoadmapValidatorTests.cs`
    - Test: fully valid model returns empty error list
    - Test: activity with null Quarter returns error naming the activity
    - Test: activity with whitespace Category returns error naming the activity
    - Test: unresolved dependency returns error with both activity and target label
    - Test: multiple errors across multiple activities are all reported
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6_

- [x] 5. Checkpoint - Ensure parser and validator tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 6. Implement Diagram Generator
  - [x] 6.1 Implement DiagramGenerator core XML structure
    - Create `Output/DiagramGenerator.cs` implementing `IDiagramGenerator`
    - Generate mxGraphModel XML with standard root cells (id="0", id="1")
    - Implement quarter sorting logic: parse "Q{n} {year}" format, sort by year then quarter number ascending; place unparseable values after valid quarters sorted alphabetically
    - Use `System.Xml.Linq` (`XDocument`, `XElement`) for XML construction
    - _Requirements: 3.1, 3.9_

  - [x] 6.2 Implement swimlane generation
    - Create one swimlane per distinct category, ordered alphabetically top to bottom
    - Set swimlane style with `horizontal=0` for vertical text (rotated 90° CCW)
    - Calculate swimlane geometry: y-position based on alphabetical order, width spanning all quarter columns, height based on content
    - _Requirements: 3.2, 3.3_

  - [x] 6.3 Implement quarter column layout
    - Create vertical separator lines for quarter column boundaries
    - Create quarter labels at the top of each column
    - Order columns left-to-right by sorted quarter values
    - Handle unparseable quarter values per sorting rules
    - _Requirements: 3.4, 3.5, 3.6, 3.10_

  - [x] 6.4 Implement activity node placement
    - Place each activity node within its category swimlane at the x-position of its quarter column
    - Stack multiple activities sharing the same category and quarter vertically without overlapping
    - Use rounded rectangle style for activity nodes
    - _Requirements: 3.7_

  - [x] 6.5 Implement dependency edge generation
    - Create directed edges for each dependency relationship
    - Set `source` to antecedent activity element ID, `target` to dependent activity element ID
    - Edges are parented to the root cell (id="1") for cross-swimlane support
    - _Requirements: 3.8_

  - [ ]* 6.6 Write property test for XML validity
    - Create `tests/MarkdownToDrawio.Tests/Output/XmlValidityPropertyTests.cs`
    - **Property 4: Generated XML Is Valid**
    - Generate arbitrary valid `RoadmapModel` instances
    - Assert `XDocument.Parse(output)` does not throw
    - Minimum 100 iterations
    - **Validates: Requirements 3.1, 3.9**

  - [ ]* 6.7 Write property test for swimlane structure
    - Create `tests/MarkdownToDrawio.Tests/Output/SwimlanePropertyTests.cs`
    - **Property 5: Swimlane Structure Matches Categories**
    - Assert exactly one swimlane per distinct category
    - Assert each swimlane `value` matches category label
    - Assert swimlane style contains `horizontal=0`
    - Assert swimlanes ordered alphabetically by ascending y-coordinate
    - Minimum 100 iterations
    - **Validates: Requirements 3.2, 3.3**

  - [ ]* 6.8 Write property test for quarter column ordering
    - Create `tests/MarkdownToDrawio.Tests/Output/QuarterOrderingPropertyTests.cs`
    - **Property 6: Quarter Column Ordering**
    - Assert quarter columns ordered by year then quarter number ascending
    - Assert unparseable quarter values appear after valid quarters, sorted alphabetically
    - Minimum 100 iterations
    - **Validates: Requirements 3.4, 3.5, 3.6, 3.10**

  - [ ]* 6.9 Write property test for activity placement
    - Create `tests/MarkdownToDrawio.Tests/Output/PlacementPropertyTests.cs`
    - **Property 7: Activity Placement Correctness**
    - Assert each activity node is within its category swimlane
    - Assert each activity node is at its quarter column x-position
    - Assert activities sharing category and quarter have distinct y-coordinates
    - Minimum 100 iterations
    - **Validates: Requirements 3.7**

  - [ ]* 6.10 Write property test for dependency edges
    - Create `tests/MarkdownToDrawio.Tests/Output/EdgePropertyTests.cs`
    - **Property 8: Dependency Edges Match Model**
    - Assert one directed edge per dependency relationship
    - Assert edge `source` references antecedent element ID and `target` references dependent element ID
    - Minimum 100 iterations
    - **Validates: Requirements 3.8**

  - [ ]* 6.11 Write unit tests for DiagramGenerator
    - Create `tests/MarkdownToDrawio.Tests/Output/DiagramGeneratorTests.cs`
    - Test: single activity produces valid XML with one swimlane and one node
    - Test: two activities in different categories produce two swimlanes
    - Test: dependency between activities produces edge element
    - Test: multiple activities in same category/quarter are stacked vertically
    - Test: unparseable quarter value is placed after valid quarters
    - _Requirements: 3.1, 3.2, 3.7, 3.8, 3.10_

- [x] 7. Checkpoint - Ensure diagram generator tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 8. Implement CLI and wire pipeline together
  - [x] 8.1 Implement CliConfiguration
    - Create `Cli/CliConfiguration.cs` using `System.CommandLine`
    - Define required positional argument for input Markdown file path
    - Define optional second positional argument for output file path
    - Set up `-h`/`--help` flag behavior (prints usage to stdout, exits with code 0)
    - _Requirements: 4.1, 4.2, 4.4_

  - [x] 8.2 Implement default output path derivation
    - When no output path is provided, compute default as input path with extension replaced by `.drawio`
    - _Requirements: 4.3_

  - [x] 8.3 Implement pipeline orchestration in Program.cs
    - Wire CLI argument parsing → file reading → MarkdownParser → RoadmapValidator → DiagramGenerator → file writing
    - Validate input file exists (exit code 1 with error to stderr if not)
    - Validate parsed model has activities (exit code 1 if no level-2 headings found)
    - Run validator and report all errors to stderr if any exist (exit code 1)
    - Validate output directory exists (exit code 1 with error to stderr if not)
    - Write output file, handling permission errors (exit code 1 with error to stderr)
    - Overwrite existing output file without prompting
    - Exit with code 0 on success
    - _Requirements: 1.7, 1.8, 2.6, 4.3, 4.5, 4.6, 4.7, 4.8_

  - [ ]* 8.4 Write property test for default output path derivation
    - Create `tests/MarkdownToDrawio.Tests/Cli/OutputPathPropertyTests.cs`
    - **Property 9: Default Output Path Derivation**
    - Generate arbitrary file path strings with various extensions
    - Assert default output path is identical to input with extension replaced by `.drawio`
    - Minimum 100 iterations
    - **Validates: Requirements 4.3**

  - [ ]* 8.5 Write unit tests for CLI behavior
    - Create `tests/MarkdownToDrawio.Tests/Cli/CliTests.cs`
    - Test: no arguments prints usage to stdout and exits with code 0
    - Test: `-h` flag prints help and exits with code 0
    - Test: non-existent input file prints error to stderr and exits with code 1
    - Test: valid input produces output file and exits with code 0
    - Test: missing output directory prints error to stderr and exits with code 1
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.7, 4.8_

- [x] 9. Create FsCheck generators for domain types
  - [x] 9.1 Implement ArbitraryRoadmaps generator
    - Create `tests/MarkdownToDrawio.Tests/Generators/ArbitraryRoadmaps.cs`
    - Generator for valid `RoadmapModel`: random activity count 1–20, unique non-empty alphanumeric labels, random quarter values in "Q{n} {year}" format (n in 1–4, year in 2020–2030), random category strings, dependency labels referencing only existing activity labels
    - Generator for invalid `RoadmapModel`: same but with some activities having null/whitespace quarter or category, and some dependency labels referencing non-existent activities
    - Generator for file paths: random valid path strings with various extensions
    - _Requirements: 5.1, 5.2_

- [x] 10. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- The project uses .NET 10, matching the existing DrawioToMarkdown project
- FsCheck.Xunit 3.1.0 and xunit 2.9.3 match the existing test project versions
- `System.CommandLine` 2.0.0-beta4.22272.1 matches the existing project version

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["1.2", "1.3", "1.4"] },
    { "id": 2, "tasks": ["2.1", "9.1"] },
    { "id": 3, "tasks": ["2.2", "3.1", "4.1"] },
    { "id": 4, "tasks": ["3.2", "4.2", "4.3", "4.4"] },
    { "id": 5, "tasks": ["6.1"] },
    { "id": 6, "tasks": ["6.2", "6.3"] },
    { "id": 7, "tasks": ["6.4", "6.5"] },
    { "id": 8, "tasks": ["6.6", "6.7", "6.8", "6.9", "6.10", "6.11"] },
    { "id": 9, "tasks": ["8.1", "8.2"] },
    { "id": 10, "tasks": ["8.3"] },
    { "id": 11, "tasks": ["8.4", "8.5"] }
  ]
}
```
