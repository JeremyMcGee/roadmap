# Implementation Plan: DrawIO to Roadmap Markdown

## Overview

Extend the existing `DrawioToMarkdown` CLI tool to support the full roadmap format used by `MarkdownToDrawio`. This involves extending the parser to extract swimlanes and quarter columns, introducing a position resolver to assign categories and quarters based on geometry, replacing the markdown generator and XML pretty-printer to produce the full roadmap format, and updating the CLI pipeline to wire the new components together.

## Tasks

- [x] 1. Define new data models and interfaces
  - [x] 1.1 Create extended ParsedDiagram records and update IDrawioParser
    - Replace existing `ParsedDiagram` record with the extended version containing `SwimlaneDef`, `QuarterColumnDef`, `ActivityNodeDef`, and `DependencyEdgeDef` records
    - Update `IDrawioParser` interface to return the new `ParsedDiagram`
    - Add geometry fields (X, Y, Width, Height) to the activity node representation
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5_

  - [x] 1.2 Create RoadmapModel and RoadmapActivity in DrawioToMarkdown.Model namespace
    - Create `DrawioToMarkdown.Model.RoadmapModel` and `DrawioToMarkdown.Model.RoadmapActivity` records matching the design specification
    - `RoadmapActivity` has Label, Quarter, Category, and DependencyLabels properties
    - _Requirements: 4.1, 4.6, 4.7_

  - [x] 1.3 Create IPositionResolver interface in DrawioToMarkdown.Resolution namespace
    - Define the `IPositionResolver` interface with a `Resolve(ParsedDiagram diagram)` method returning `RoadmapModel`
    - _Requirements: 2.1, 2.2_

  - [x] 1.4 Update IMarkdownGenerator and IXmlPrettyPrinter interfaces to accept RoadmapModel
    - Change `IMarkdownGenerator.Generate` to accept `DrawioToMarkdown.Model.RoadmapModel` and return `string`
    - Change `IXmlPrettyPrinter.Print` to accept `DrawioToMarkdown.Model.RoadmapModel` and return `string`
    - _Requirements: 4.1, 6.1_

- [x] 2. Implement extended DrawioParser
  - [x] 2.1 Extend DrawioParser to extract swimlanes and quarter columns
    - Parse mxCell elements with style containing `shape=swimlane;horizontal=0` and `vertex="1"` as swimlanes, extracting value, x, y, width, height from mxGeometry
    - Parse mxCell elements with id starting `qlabel_` and `vertex="1"` as quarter column labels, extracting value, x, width from mxGeometry
    - Validate the mxfile/diagram/mxGraphModel/root structure and throw if missing
    - _Requirements: 1.1, 1.2, 1.3, 1.8_

  - [x] 2.2 Extend DrawioParser to extract activity nodes with geometry
    - Parse mxCell elements with style containing `rounded=1;whiteSpace=wrap;html=1` and `vertex="1"` as activity nodes
    - Extract value attribute (strip HTML tags, decode HTML entities), x, y, width, height from mxGeometry
    - _Requirements: 1.4_

  - [x] 2.3 Extend DrawioParser to extract dependency edges with validation
    - Parse mxCell elements with `edge="1"` and both source/target attributes as dependency edges
    - Discard self-referencing edges (source == target)
    - Deduplicate edges with same source-target pair
    - Discard edges referencing non-activity node IDs
    - Handle duplicate activity labels by merging dependency sets
    - _Requirements: 1.5, 3.1, 3.2, 3.3, 3.4, 3.5, 6.5_

  - [x]* 2.4 Write unit tests for extended DrawioParser
    - Test swimlane extraction with various geometries
    - Test quarter column extraction with qlabel_ prefix
    - Test activity node HTML stripping and entity decoding edge cases
    - Test edge filtering (dangling, self-referencing, duplicate)
    - Test malformed XML and missing structure error cases
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 1.7, 1.8_

- [x] 3. Implement PositionResolver
  - [x] 3.1 Implement PositionResolver with category assignment logic
    - Compute each activity's vertical center (Y + Height / 2)
    - Find all swimlanes whose y-range contains the vertical center
    - If exactly one: assign that swimlane's label
    - If zero or multiple: assign the swimlane with minimum distance from its vertical midpoint to the node's vertical center, breaking ties by document order (first in list)
    - _Requirements: 2.1, 2.3, 2.5_

  - [x] 3.2 Implement PositionResolver with quarter assignment logic
    - Compute each activity's horizontal center (X + Width / 2)
    - Find all quarter columns whose x-range contains the horizontal center
    - If exactly one: assign that column's label
    - If zero or multiple: assign the column with minimum distance from its horizontal midpoint to the node's horizontal center, breaking ties by document order (first in list)
    - _Requirements: 2.2, 2.4, 2.6_

  - [x] 3.3 Add validation in PositionResolver for missing swimlanes and quarter columns
    - Throw/return error when no swimlanes are found in the parsed diagram
    - Throw/return error when no quarter columns are found in the parsed diagram
    - _Requirements: 2.7, 2.8_

  - [x]* 3.4 Write property test for position resolver category assignment
    - **Property 1: Position resolver assigns correct category**
    - **Validates: Requirements 2.1, 2.3, 2.5**

  - [x]* 3.5 Write property test for position resolver quarter assignment
    - **Property 2: Position resolver assigns correct quarter**
    - **Validates: Requirements 2.2, 2.4, 2.6**

- [x] 4. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Implement MarkdownGenerator for roadmap format
  - [x] 5.1 Implement MarkdownGenerator producing full roadmap markdown
    - Output begins with `# Dependency Documentation` followed by a blank line
    - Activities sorted lexicographically by label (ordinal comparison)
    - Each activity rendered as `## {Label}` with sub-sections `### Depends on`, `### Quarter`, `### Category` in that order, each preceded by a blank line
    - Dependencies rendered as `- [Antecedent Label](#anchor)` sorted lexicographically by label
    - Anchor derived by: lowercase → remove non-`[a-z0-9\s-]` chars → replace whitespace runs with single `-`
    - Activities with no dependencies show "No dependencies"
    - Quarter and Category values rendered as plain text lines
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 4.7_

  - [x] 5.2 Add validation in MarkdownGenerator for null/empty quarter and category
    - If any activity has null/empty quarter: report error with activity label, produce no output
    - If any activity has null/empty category: report error with activity label, produce no output
    - _Requirements: 4.8, 4.9_

  - [x]* 5.3 Write unit tests for MarkdownGenerator
    - Test heading structure and section ordering
    - Test anchor generation with special characters
    - Test "No dependencies" output
    - Test error reporting for missing quarter/category
    - Test lexicographic sorting of activities and dependencies
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 4.7, 4.8, 4.9_

- [x] 6. Implement XmlPrettyPrinter for roadmap format
  - [x] 6.1 Implement XmlPrettyPrinter producing draw.io XML with swimlanes and quarter columns
    - Generate mxfile/diagram/mxGraphModel/root structure
    - Emit swimlane mxCells with `shape=swimlane;horizontal=0` style, category labels, and geometry
    - Emit quarter label mxCells with `qlabel_` id prefix and geometry
    - Emit activity node mxCells with `rounded=1;whiteSpace=wrap;html=1` style positioned within correct swimlane/quarter intersection
    - Emit dependency edge mxCells with source and target attributes
    - _Requirements: 6.1_

  - [x]* 6.2 Write property test for XML round-trip
    - **Property 3: XML round-trip preserves model**
    - **Validates: Requirements 6.1, 1.2, 1.3, 1.4, 1.5, 3.1, 3.3, 3.4**

- [x] 7. Update CLI pipeline to wire new components
  - [x] 7.1 Update Program.cs to use PositionResolver and new MarkdownGenerator
    - Replace the `GraphBuilder` → `DependencyGraph` → old `MarkdownGenerator` pipeline with `DrawioParser` → `PositionResolver` → new `MarkdownGenerator`
    - Add error handling for missing swimlanes, missing quarter columns, and resolution errors
    - Maintain existing CLI argument handling and file I/O error handling
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 5.7, 5.8, 1.6, 1.7, 1.8, 2.7, 2.8, 4.8, 4.9_

  - [x]* 7.2 Write unit tests for CLI error handling
    - Test file not found error message and exit code
    - Test malformed XML error message and exit code
    - Test unrecognized draw.io structure error
    - Test missing swimlanes error
    - Test missing quarter columns error
    - Test output directory not found error
    - Test successful conversion with zero exit code
    - _Requirements: 1.6, 1.7, 1.8, 2.7, 2.8, 5.4, 5.5, 5.7, 5.8_

- [x] 8. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 9. Implement round-trip property tests
  - [x]* 9.1 Add project reference to MarkdownToDrawio in test project
    - Add `<ProjectReference Include="../../src/MarkdownToDrawio/MarkdownToDrawio.csproj" />` to `DrawioToMarkdown.Tests.csproj`
    - This is a test-only dependency for round-trip validation
    - _Requirements: 6.2, 6.3, 6.4_

  - [x]* 9.2 Create FsCheck generators for RoadmapModel, SwimlaneDef, QuarterColumnDef, and ActivityNodeDef
    - Generate valid models with 1–500 activities, non-empty labels (alphanumeric + spaces, 1–200 chars)
    - Quarters in "Q{1-4} {2020-2030}" format
    - Categories from a random pool
    - 0–50 dependency labels referencing other activity labels in the model
    - Non-overlapping swimlane and quarter column generators
    - _Requirements: 6.1, 6.2, 6.4_

  - [x]* 9.3 Write property test for Markdown round-trip
    - **Property 4: Markdown round-trip preserves model**
    - **Validates: Requirements 6.4, 4.2, 4.3, 4.4, 4.6, 4.7**

  - [x]* 9.4 Write property test for full pipeline round-trip
    - **Property 5: Full pipeline round-trip**
    - **Validates: Requirements 6.2**

  - [x]* 9.5 Write property test for Markdown byte-for-byte compatibility
    - **Property 6: Markdown output byte-for-byte compatibility**
    - **Validates: Requirements 6.3**

  - [x]* 9.6 Write property test for duplicate label merging
    - **Property 7: Duplicate label merging**
    - **Validates: Requirements 6.5**

- [x] 10. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- The test project references `MarkdownToDrawio` only for round-trip validation (test-only dependency)
- The existing `DependencyGraph` and `GraphBuilder` classes can remain in the codebase for backward compatibility but are no longer used in the main pipeline
- All code is C# targeting .NET 10.0 with nullable reference types enabled

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3", "1.4"] },
    { "id": 1, "tasks": ["2.1", "2.2", "2.3"] },
    { "id": 2, "tasks": ["2.4", "3.1", "3.2"] },
    { "id": 3, "tasks": ["3.3", "3.4", "3.5"] },
    { "id": 4, "tasks": ["5.1", "5.2", "6.1"] },
    { "id": 5, "tasks": ["5.3", "6.2", "7.1"] },
    { "id": 6, "tasks": ["7.2"] },
    { "id": 7, "tasks": ["9.1"] },
    { "id": 8, "tasks": ["9.2"] },
    { "id": 9, "tasks": ["9.3", "9.4", "9.5", "9.6"] }
  ]
}
```
