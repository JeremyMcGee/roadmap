# roadmap

Create and maintain an application roadmap. This repository contains two complementary CLI tools for converting between Draw.io diagrams and Markdown documentation.

## DrawioToMarkdown

A command-line tool that converts draw.io XML diagram files into Markdown documentation of activity dependencies. Feed it a `.drawio` file containing your roadmap, and it produces a structured `.md` file showing what depends on what.

### Usage

```bash
# Basic usage — outputs diagram.md alongside the input file
dotnet run --project src/DrawioToMarkdown -- path/to/diagram.drawio

# Specify an output path
dotnet run --project src/DrawioToMarkdown -- path/to/diagram.drawio output/dependencies.md

# Show help
dotnet run --project src/DrawioToMarkdown -- --help
```

### How it works

The tool follows a pipeline architecture:

1. **Parse** — Reads draw.io XML (mxGraph format), extracts activity nodes and dependency edges
2. **Build Graph** — Constructs a dependency graph, filtering dangling edges and deduplicating
3. **Generate Markdown** — Produces a structured document with nodes sorted alphabetically, each listing its prerequisites

### Example

Given a draw.io diagram with activities "Design API", "Implement Backend", and "Write Tests" where Design → Implement → Tests, the output looks like:

```markdown
# Dependency Documentation

## Design API

### Depends on

No dependencies

## Implement Backend

### Depends on

- Design API

## Write Tests

### Depends on

- Implement Backend
```

### Building

Requires .NET 10 SDK.

```bash
dotnet build src/DrawioToMarkdown
```

### Testing

```bash
dotnet test tests/DrawioToMarkdown.Tests
```

The test suite includes unit tests and property-based tests (FsCheck) validating:
- Parse/print round-trip correctness
- Dangling edge filtering
- Edge deduplication
- Markdown output completeness
- Default output path derivation

### Project structure

```
src/DrawioToMarkdown/
├── Program.cs              # CLI entry point
├── Cli/                    # System.CommandLine configuration
├── Parsing/                # Draw.io XML parser
├── Graph/                  # Dependency graph builder and data models
└── Output/                 # Markdown generator and XML pretty-printer

tests/DrawioToMarkdown.Tests/
├── Parsing/                # Parser unit and property tests
├── Graph/                  # Graph builder unit and property tests
├── Output/                 # Markdown generator unit and property tests
├── Cli/                    # CLI behavior and output path tests
└── Generators/             # FsCheck generators for domain types
```

---

## MarkdownToDrawio

The reverse tool — converts Markdown roadmap documentation (with Quarter and Category metadata) back into Draw.io XML diagram files. The output features horizontal swimlanes (one per category) and vertical quarter columns, with activity nodes placed at the correct intersections and dependency arrows connecting them.

### Usage

```bash
# Basic usage — outputs diagram.drawio alongside the input file
dotnet run --project src/MarkdownToDrawio -- path/to/roadmap.md

# Specify an output path
dotnet run --project src/MarkdownToDrawio -- path/to/roadmap.md output/diagram.drawio

# Show help
dotnet run --project src/MarkdownToDrawio -- --help
```

### How it works

The tool follows the same pipeline architecture as its counterpart:

1. **Parse Markdown** — Reads the Markdown file, extracts activities with their Quarter, Category, and dependency metadata
2. **Validate** — Checks all activities have required metadata and all dependency references resolve
3. **Generate Diagram** — Produces Draw.io-compatible XML with swimlanes, quarter columns, activity nodes, and dependency edges

### Expected input format

The Markdown input should include Quarter and Category metadata for each activity:

```markdown
# Dependency Documentation

## Design API

### Depends on

No dependencies

### Quarter

Q1 2025

### Category

Infrastructure

## Implement Backend

### Depends on

- [Design API](#design-api)

### Quarter

Q2 2025

### Category

Infrastructure
```

### Output

The tool generates a `.drawio` file that opens in Draw.io with:
- **Horizontal swimlanes** — one per category, ordered alphabetically
- **Vertical quarter columns** — sorted chronologically (Q1 2025 → Q2 2025 → …)
- **Activity nodes** — placed at the intersection of their category row and quarter column
- **Dependency edges** — directed arrows from prerequisite to dependent activity

### Building

Requires .NET 10 SDK.

```bash
dotnet build src/MarkdownToDrawio
```

### Testing

```bash
dotnet test tests/MarkdownToDrawio.Tests
```

The test suite includes unit tests and property-based tests (FsCheck) validating:
- Parse/print round-trip correctness
- Metadata validation completeness
- Unresolved dependency detection
- XML validity of generated output
- Swimlane structure matches categories
- Quarter column ordering
- Activity placement correctness
- Dependency edge accuracy
- Default output path derivation

### Project structure

```
src/MarkdownToDrawio/
├── Program.cs              # CLI entry point and pipeline orchestration
├── Cli/                    # System.CommandLine configuration
├── Parsing/                # Markdown parser and pretty-printer
├── Validation/             # Metadata and dependency validator
├── Model/                  # Domain models (Activity, RoadmapModel)
└── Output/                 # Draw.io XML diagram generator

tests/MarkdownToDrawio.Tests/
├── Parsing/                # Parser unit and round-trip property tests
├── Validation/             # Validator unit and property tests
├── Output/                 # Diagram generator unit and property tests
├── Cli/                    # CLI behavior and output path tests
└── Generators/             # FsCheck generators for domain types
```
