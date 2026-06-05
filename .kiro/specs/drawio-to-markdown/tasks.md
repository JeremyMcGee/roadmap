# Implementation Plan: drawio-to-markdown

## Overview

A .NET 10 console application implementing a pipeline (Parse → Build Graph → Generate Output) that converts draw.io XML diagram files into Markdown documentation of activity dependencies. Includes an XML pretty-printer for round-trip testing. Uses `System.Xml.Linq` for XML parsing, `System.CommandLine` for CLI, and immutable record types for the domain model.

## Tasks

- [x] 1. Set up project structure and core data models
  - [x] 1.1 Create the DrawioToMarkdown console project with .NET 10 target framework, add `System.CommandLine` package reference, and create the directory structure (`Parsing/`, `Graph/`, `Output/`, `Cli/`)
    - _Requirements: 4.1_

  - [x] 1.2 Define core data model records: `ActivityNode`, `DependencyEdge`, `ParsedDiagram`, and `DependencyGraph` class as specified in the design
    - Create `Graph/ActivityNode.cs`, `Graph/DependencyGraph.cs`, `Parsing/ParsedDiagram.cs`
    - Define interface files: `Parsing/IDrawioParser.cs`, `Graph/IGraphBuilder.cs`, `Output/IMarkdownGenerator.cs`, `Output/IXmlPrettyPrinter.cs`
    - _Requirements: 1.2, 1.3, 2.1, 2.2_

  - [x] 1.3 Create the test project (`tests/DrawioToMarkdown.Tests/`) with xUnit, FsCheck.Xunit, and a project reference to the main application
    - Set up directory structure: `Parsing/`, `Graph/`, `Output/`, `Cli/`, `Generators/`
    - _Requirements: 5.2_

- [x] 2. Implement XML Parser
  - [x] 2.1 Implement `DrawioParser.cs` that uses `XDocument.Parse()` to read draw.io XML, find all `mxCell` elements under `mxfile/diagram/mxGraphModel/root`, classify each as vertex (node) or edge, and return a `ParsedDiagram`
    - Extract node ID from `id` attribute and label from `value` attribute for vertex cells
    - Extract source/target IDs from `source`/`target` attributes for edge cells
    - Throw on malformed XML (non-valid XML content)
    - _Requirements: 1.1, 1.2, 1.3, 1.5_

  - [x]* 2.2 Write unit tests for `DrawioParser`
    - Test parsing a valid draw.io XML file with known nodes and edges
    - Test that malformed XML throws an appropriate exception
    - Test XML with no vertex nodes
    - Test that edge cells without source/target are handled gracefully
    - _Requirements: 1.1, 1.2, 1.3, 1.5, 1.6_

- [x] 3. Implement Graph Builder
  - [x] 3.1 Implement `GraphBuilder.cs` that takes a `ParsedDiagram` and constructs a `DependencyGraph`: filter out dangling edges (edges referencing non-existent node IDs), deduplicate edges with the same source→target pair, and build per-node dependency sets
    - _Requirements: 2.1, 2.2, 2.3, 2.4_

  - [x]* 3.2 Write unit tests for `GraphBuilder`
    - Test building graph from a diagram with valid nodes and edges
    - Test that dangling edges (referencing non-existent nodes) are filtered out
    - Test that duplicate edges are deduplicated
    - Test empty edge list produces graph with nodes but no dependencies
    - _Requirements: 2.1, 2.2, 2.3, 2.4_

  - [x]* 3.3 Write property test for dangling edge filtering
    - **Property 2: Dangling Edge Filtering**
    - **Validates: Requirements 2.3**

  - [x]* 3.4 Write property test for edge deduplication
    - **Property 3: Edge Deduplication (Idempotence)**
    - **Validates: Requirements 2.4**

- [x] 4. Checkpoint - Ensure core pipeline builds and tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Implement Markdown Generator
  - [x] 5.1 Implement `MarkdownGenerator.cs` that iterates nodes sorted alphabetically by label, emits `# Dependency Documentation` heading, a `## {label}` section per node, a `### Depends on` sub-section with bullet list of prerequisite labels (or "No dependencies" text)
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6_

  - [x]* 5.2 Write unit tests for `MarkdownGenerator`
    - Test output contains the correct heading
    - Test nodes with dependencies list them correctly
    - Test nodes without dependencies show "No dependencies" indicator
    - Test nodes are sorted alphabetically
    - _Requirements: 3.2, 3.3, 3.4, 3.5, 3.6_

  - [x]* 5.3 Write property test for Markdown output completeness
    - **Property 4: Markdown Output Completeness**
    - **Validates: Requirements 3.2, 3.3, 3.4, 3.5**

- [x] 6. Implement XML Pretty-Printer
  - [x] 6.1 Implement `XmlPrettyPrinter.cs` that reconstructs draw.io-compatible XML from a `DependencyGraph`: emits `mxGraphModel/root` wrapper, standard root cells (id="0" and id="1"), each node as a vertex `mxCell` with `value` attribute set to label, and each edge as an edge `mxCell` with `source`/`target` attributes
    - _Requirements: 5.1_

  - [x]* 6.2 Write property test for parse/print round-trip
    - **Property 1: Parse/Print Round-Trip**
    - **Validates: Requirements 1.1, 1.2, 1.3, 2.1, 2.2, 5.1, 5.2**

- [x] 7. Implement FsCheck generators for domain types
  - [x] 7.1 Create `Generators/ArbitraryGraphs.cs` with FsCheck `Arbitrary` generators for `DependencyGraph` (random node count 1–50, random valid edges, random alphanumeric labels), `ParsedDiagram` (including dangling edges), and file path strings with various extensions
    - _Requirements: 5.2_

- [x] 8. Checkpoint - Ensure all property tests and unit tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 9. Implement CLI and wire pipeline together
  - [x] 9.1 Implement `CliConfiguration.cs` using `System.CommandLine` to define the root command with a required input file path positional argument, an optional output file path second argument, and a help flag
    - _Requirements: 4.1, 4.2, 4.4_

  - [x] 9.2 Implement `Program.cs` entry point that: validates input file exists (exit code 1 if not), reads file content, calls Parser (exit code 1 on malformed XML), validates parsed diagram has nodes (exit code 1 if none), calls GraphBuilder, calls MarkdownGenerator, computes default output path (replace extension with `.md`) when not specified, validates output directory exists (exit code 1 if not), writes Markdown to output file
    - Error messages to stderr, help text to stdout
    - _Requirements: 1.1, 1.4, 1.5, 1.6, 3.1, 4.1, 4.2, 4.3, 4.4, 4.5_

  - [x]* 9.3 Write unit tests for CLI behavior
    - Test help flag prints usage and exits 0
    - Test missing input file exits with non-zero code
    - Test default output path derivation (extension replaced with .md)
    - Test non-existent output directory exits with non-zero code
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5_

  - [x]* 9.4 Write property test for default output path derivation
    - **Property 5: Default Output Path Derivation**
    - **Validates: Requirements 4.3**

- [x] 10. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- The FsCheck generators (task 7.1) should be created before running property tests; ensure this task is complete before executing property test tasks
- All code targets .NET 10 with C# and uses `System.CommandLine` for CLI parsing

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["1.2", "1.3"] },
    { "id": 2, "tasks": ["2.1", "7.1"] },
    { "id": 3, "tasks": ["2.2", "3.1"] },
    { "id": 4, "tasks": ["3.2", "3.3", "3.4", "5.1", "6.1"] },
    { "id": 5, "tasks": ["5.2", "5.3", "6.2"] },
    { "id": 6, "tasks": ["9.1"] },
    { "id": 7, "tasks": ["9.2"] },
    { "id": 8, "tasks": ["9.3", "9.4"] }
  ]
}
```
