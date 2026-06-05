# Requirements Document

## Introduction

A command-line utility that reads Markdown files (in the format produced by the existing drawio-to-markdown tool, augmented with Quarter and Category metadata) and converts them back into Draw.IO XML diagram files. The generated diagram features horizontal swimlanes (one per category) and vertical quarter columns, with activity nodes placed at the intersection of their assigned swimlane and quarter. Dependency arrows connect activities to indicate prerequisite relationships. This tool is the reverse of the existing drawio-to-markdown tool and lives as a separate project in the same solution.

## Glossary

- **CLI**: The command-line interface application that accepts arguments and executes the Markdown-to-Draw.IO conversion
- **Markdown_Parser**: The component responsible for reading and interpreting the input Markdown file into a structured intermediate representation
- **Activity**: A named task or work item extracted from a level-2 heading in the Markdown file
- **Quarter**: A time period label (e.g., "Q1 2025") denoting when an Activity is scheduled, specified under a level-3 "Quarter" heading
- **Category**: A grouping label denoting the swimlane an Activity belongs to, specified under a level-3 "Category" heading
- **Dependency**: A directed relationship where one Activity (the dependent) requires another Activity (the antecedent/prerequisite) to be completed first
- **Swimlane**: A horizontal band in the output diagram representing a Category, with the Category name rendered as vertical text on the left label area
- **Quarter_Column**: A vertical section in the output diagram representing a Quarter, delimited by vertical lines and labelled at the top
- **Diagram_Generator**: The component responsible for producing Draw.IO-compatible XML from the parsed activities, dependencies, and layout metadata
- **Roadmap_Model**: The internal representation containing all Activities with their labels, quarters, categories, and dependency relationships

## Requirements

### Requirement 1: Parse Markdown Input

**User Story:** As an architect, I want the tool to parse the Markdown file produced by the drawio-to-markdown tool (with Quarter and Category additions), so that I can reconstruct a Draw.IO diagram from documentation.

#### Acceptance Criteria

1. WHEN the CLI receives a valid Markdown file path, THE Markdown_Parser SHALL read the file and produce a Roadmap_Model containing all Activities and Dependencies
2. WHEN the Markdown file contains a level-2 heading, THE Markdown_Parser SHALL extract the heading text as an Activity label
3. WHEN an Activity section contains a level-3 "Depends on" heading followed by a bullet list of links, THE Markdown_Parser SHALL extract each link text as a dependency on the named antecedent Activity
4. WHEN an Activity section contains a level-3 "Depends on" heading followed by the text "No dependencies", THE Markdown_Parser SHALL record zero dependencies for that Activity
5. WHEN an Activity section contains a level-3 "Quarter" heading, THE Markdown_Parser SHALL extract the text on the following line as the Activity quarter value
6. WHEN an Activity section contains a level-3 "Category" heading, THE Markdown_Parser SHALL extract the text on the following line as the Activity category value
7. IF the input file does not exist at the specified path, THEN THE CLI SHALL exit with a non-zero exit code and print an error message indicating the file was not found
8. IF the Markdown file contains no level-2 headings, THEN THE CLI SHALL exit with a non-zero exit code and print an error message indicating no activities were found

### Requirement 2: Validate Activity Metadata

**User Story:** As an architect, I want the tool to validate that every activity has a Quarter and Category, so that the diagram can be correctly laid out.

#### Acceptance Criteria

1. WHEN an Activity section does not contain a level-3 "Quarter" heading, THE CLI SHALL exit with a non-zero exit code and print an error message that includes the Activity label identifying the Activity that is missing a Quarter
2. WHEN an Activity section does not contain a level-3 "Category" heading, THE CLI SHALL exit with a non-zero exit code and print an error message that includes the Activity label identifying the Activity that is missing a Category
3. WHEN an Activity section contains a level-3 "Quarter" heading with empty or whitespace-only content, THE CLI SHALL exit with a non-zero exit code and print an error message that includes the Activity label identifying the Activity with an empty Quarter
4. WHEN an Activity section contains a level-3 "Category" heading with empty or whitespace-only content, THE CLI SHALL exit with a non-zero exit code and print an error message that includes the Activity label identifying the Activity with an empty Category
5. WHEN a dependency link references an Activity label that does not match any level-2 heading in the file, THE CLI SHALL exit with a non-zero exit code and print an error message that includes both the Activity label containing the dependency link and the unresolved target label
6. IF multiple validation errors exist across Activities, THEN THE CLI SHALL report all validation errors before exiting rather than stopping at the first error encountered

### Requirement 3: Generate Draw.IO XML Output

**User Story:** As an architect, I want the tool to produce a valid Draw.IO XML file with swimlanes and quarter columns, so that I can open and edit the roadmap in Draw.IO.

#### Acceptance Criteria

1. WHEN the Roadmap_Model is valid, THE Diagram_Generator SHALL produce a Draw.IO-compatible XML file in the mxGraphModel format
2. THE Diagram_Generator SHALL create one horizontal Swimlane for each distinct Category in the Roadmap_Model, ordered alphabetically by Category label from top to bottom
3. THE Diagram_Generator SHALL render the Category label as vertical text (rotated 90 degrees counter-clockwise) in the Swimlane label area
4. THE Diagram_Generator SHALL create vertical Quarter_Column sections delimited by vertical lines, one for each distinct Quarter in the Roadmap_Model
5. THE Diagram_Generator SHALL label each Quarter_Column with the Quarter text at the top of the column
6. THE Diagram_Generator SHALL order Quarter_Columns from left to right by parsing Quarter values in "Q{n} {year}" format and sorting by year ascending then quarter number ascending
7. THE Diagram_Generator SHALL place each Activity node in the cell at the intersection of its Category Swimlane row and its Quarter_Column, and WHEN multiple Activities share the same Category and Quarter, THE Diagram_Generator SHALL stack them vertically within that cell without overlapping
8. WHEN a Dependency exists between two Activities, THE Diagram_Generator SHALL create a directed edge where the arrowhead points to the dependent Activity and the tail points to the antecedent Activity
9. THE Diagram_Generator SHALL produce output that is valid XML parseable by standard XML parsers
10. IF a Quarter value does not match the expected "Q{n} {year}" format, THEN THE Diagram_Generator SHALL place that Quarter_Column after all parseable quarters, ordered alphabetically among unparseable values

### Requirement 4: Command-Line Interface

**User Story:** As an architect, I want to run the tool from the command line with simple arguments, so that I can integrate it into my workflow and scripts.

#### Acceptance Criteria

1. THE CLI SHALL accept an input Markdown file path as a required positional argument
2. THE CLI SHALL accept an output file path as an optional second argument
3. WHEN no output file path is provided, THE CLI SHALL write the output to a file named the same as the input file with a .drawio extension in the same directory
4. WHEN the CLI is invoked with no arguments or with a help flag (-h or --help), THE CLI SHALL print usage instructions to standard output and exit with a zero exit code
5. IF the output directory does not exist, THEN THE CLI SHALL exit with a non-zero exit code and print an error message to standard error indicating the output directory was not found
6. WHEN the output file already exists at the resolved output path, THE CLI SHALL overwrite the existing file without prompting
7. WHEN the CLI completes conversion successfully, THE CLI SHALL write the output file and exit with a zero exit code
8. IF the output file path cannot be written due to a permission error, THEN THE CLI SHALL exit with a non-zero exit code and print an error message to standard error indicating the file could not be written

### Requirement 5: Round-Trip Parsing Support

**User Story:** As a developer, I want a pretty-printer for the Roadmap_Model, so that I can validate parsing correctness via round-trip testing.

#### Acceptance Criteria

1. THE Markdown_Parser SHALL support converting a Roadmap_Model back into a Markdown string that, when parsed, reproduces the same set of Activities (with identical labels, Quarter values, Category values, and dependency relationships)
2. THE Markdown_Parser SHALL produce a round-trip-stable output such that parsing the pretty-printed Markdown yields a Roadmap_Model with the same Activity labels, Quarter assignments, Category assignments, and dependency sets as the original model (parse(print(model)) equals model by value on these fields)
3. THE pretty-printed Markdown SHALL include a level-1 "Dependency Documentation" heading, followed by level-2 headings for each Activity sorted in ascending lexicographic order by label, and within each Activity section level-3 headings in the order "Depends on", "Quarter", "Category"
4. WHEN an Activity has one or more dependencies, THE Markdown_Parser SHALL render each dependency as a Markdown bullet-list link referencing the antecedent Activity label, with dependencies listed in ascending lexicographic order by label
5. WHEN an Activity has zero dependencies, THE Markdown_Parser SHALL render the text "No dependencies" under the "Depends on" heading

