# Requirements Document

## Introduction

A command-line utility that reads draw.io XML files (mxGraph XML format), extracts activity nodes and dependency edges, and generates a Markdown file documenting the dependencies between activities. This tool enables architects to maintain living documentation of roadmap dependencies directly from their draw.io diagrams.

## Glossary

- **CLI**: The command-line interface application that accepts arguments and executes the conversion
- **Parser**: The component responsible for reading and interpreting draw.io XML (mxGraph format) files
- **Node**: An mxCell element in the draw.io XML that represents an activity or task (has a vertex attribute)
- **Edge**: An mxCell element in the draw.io XML that represents a dependency arrow between two nodes (has an edge attribute, with source and target attributes). The arrowhead (target) points to the dependent activity; the tail (source) points to the antecedent/prerequisite activity
- **Dependency_Graph**: The internal representation of nodes and their dependency relationships extracted from the XML
- **Markdown_Generator**: The component responsible for producing the output Markdown file from the Dependency_Graph

## Requirements

### Requirement 1: Parse draw.io XML Files

**User Story:** As an architect, I want the tool to parse draw.io XML files, so that I can extract roadmap information from my existing diagrams.

#### Acceptance Criteria

1. WHEN the CLI receives a valid draw.io XML file path, THE Parser SHALL read the file and produce a Dependency_Graph containing all nodes and edges
2. WHEN the XML file contains mxCell elements with a vertex attribute, THE Parser SHALL extract each element as a Node with its identifier and label text
3. WHEN the XML file contains mxCell elements with an edge attribute, THE Parser SHALL extract each element as an Edge where the source attribute identifies the antecedent (prerequisite) Node and the target attribute identifies the dependent Node
4. IF the input file does not exist at the specified path, THEN THE CLI SHALL exit with a non-zero exit code and print an error message indicating the file was not found
5. IF the input file contains content that is not valid XML, THEN THE Parser SHALL exit with a non-zero exit code and print an error message indicating the XML is malformed
6. IF the XML file contains no recognizable nodes, THEN THE CLI SHALL exit with a non-zero exit code and print an error message indicating no activities were found

### Requirement 2: Build Dependency Graph

**User Story:** As an architect, I want the tool to correctly identify dependencies between activities, so that I can understand the ordering constraints on my roadmap.

#### Acceptance Criteria

1. THE Dependency_Graph SHALL contain every Node present in the parsed XML
2. THE Dependency_Graph SHALL represent each Edge as a directed dependency where the source Node is the antecedent (prerequisite) and the target Node is the dependent activity
3. WHEN an Edge references a source or target identifier that does not correspond to any Node, THE Parser SHALL skip that Edge and continue processing
4. WHEN multiple edges exist between the same source and target nodes, THE Dependency_Graph SHALL represent only one dependency between those nodes

### Requirement 3: Generate Markdown Output

**User Story:** As an architect, I want a Markdown file showing dependencies, so that I can share and version-control my roadmap documentation.

#### Acceptance Criteria

1. WHEN the CLI is invoked with a valid input file, THE Markdown_Generator SHALL produce a Markdown file at the specified output path
2. THE Markdown_Generator SHALL include a heading with the title "Dependency Documentation"
3. THE Markdown_Generator SHALL list each Node as a section with its label as the heading
4. WHEN a Node has upstream dependencies, THE Markdown_Generator SHALL list each dependency under a "Depends on" sub-section
5. WHEN a Node has no upstream dependencies, THE Markdown_Generator SHALL indicate that the Node has no dependencies
6. THE Markdown_Generator SHALL format the output as valid Markdown using headings and bullet lists

### Requirement 4: Command-Line Interface

**User Story:** As an architect, I want to run the tool from the command line with simple arguments, so that I can integrate it into my workflow and scripts.

#### Acceptance Criteria

1. THE CLI SHALL accept an input file path as a required positional argument
2. THE CLI SHALL accept an output file path as an optional second argument
3. WHEN no output file path is provided, THE CLI SHALL write the output to a file named the same as the input file with a .md extension in the same directory
4. WHEN the CLI is invoked with no arguments or with a help flag, THE CLI SHALL print usage instructions and exit with a zero exit code
5. IF the output directory does not exist, THEN THE CLI SHALL exit with a non-zero exit code and print an error message indicating the output directory was not found

### Requirement 5: Pretty-Print draw.io XML (Round-Trip Support)

**User Story:** As a developer, I want a pretty-printer for the internal dependency graph representation, so that I can validate parsing correctness via round-trip testing.

#### Acceptance Criteria

1. THE Parser SHALL support converting a Dependency_Graph back into a draw.io-compatible XML string
2. FOR ALL valid Dependency_Graph objects, parsing the pretty-printed XML SHALL produce an equivalent Dependency_Graph (round-trip property)
