# roadmap

Create and maintain an application roadmap.

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
