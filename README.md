# roadmap

Create and maintain an application roadmap. This repository contains two complementary CLI tools for round-tripping between Draw.io diagrams and Markdown documentation.

Requires .NET 10 SDK.

```bash
dotnet build src/DrawioToMarkdown
dotnet build src/MarkdownToDrawio
dotnet test tests/DrawioToMarkdown.Tests
dotnet test tests/MarkdownToDrawio.Tests
```

---

## DrawioToMarkdown

Converts Draw.io XML diagram files into structured Markdown documentation of activity dependencies. Feed it a `.drawio` file containing your roadmap and it produces a `.md` file showing what depends on what.

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

1. **Parse** — Reads Draw.io XML (mxGraph format), extracts activity nodes and dependency edges
2. **Build Graph** — Constructs a dependency graph, filtering dangling edges and deduplicating
3. **Generate Markdown** — Produces a structured document with nodes sorted alphabetically, each listing its prerequisites

### Example

Given a Draw.io diagram with activities "Design API", "Implement Backend", and "Write Tests" where Design → Implement → Tests, the tool outputs:

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

### Project structure

```
src/DrawioToMarkdown/
├── Program.cs              # CLI entry point
├── Cli/                    # System.CommandLine configuration
├── Parsing/                # Draw.io XML parser
├── Graph/                  # Dependency graph builder and data models
└── Output/                 # Markdown generator and XML pretty-printer
```

---

## MarkdownToDrawio

The reverse tool — converts Markdown roadmap documentation (with Quarter and Category metadata) back into Draw.io XML diagram files. The output features horizontal swimlanes, vertical quarter columns, activity nodes at the correct intersections, and dependency arrows.

### Usage

```bash
# Basic usage — outputs roadmap.drawio alongside the input file
dotnet run --project src/MarkdownToDrawio -- path/to/roadmap.md

# Specify an output path
dotnet run --project src/MarkdownToDrawio -- path/to/roadmap.md output/diagram.drawio

# Show help
dotnet run --project src/MarkdownToDrawio -- --help
```

### How it works

1. **Parse Markdown** — Extracts activities with their Quarter, Category, and dependency metadata
2. **Validate** — Checks all activities have required metadata and all dependency references resolve
3. **Generate Diagram** — Produces Draw.io XML with swimlanes, quarter columns, activity nodes, and edges

### Diagram features

- **Horizontal swimlanes** — one per category, ordered alphabetically, with grey separator lines between them
- **Vertical quarter columns** — sorted chronologically, each with a distinct pastel background colour
- **Same-quarter dependencies** — activities are placed side-by-side (antecedent left, dependent right) and the column widens to fit
- **Red backward arrows** — dependency edges pointing from a later quarter to an earlier quarter are coloured red to highlight scheduling conflicts
- **Dynamic sizing** — swimlane heights and column widths scale to fit content without overlap

### Sample input

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

## Write Tests

### Depends on

- [Implement Backend](#implement-backend)

### Quarter

Q2 2025

### Category

Quality

## Deploy

### Depends on

- [Implement Backend](#implement-backend)
- [Write Tests](#write-tests)

### Quarter

Q3 2025

### Category

Infrastructure
```

### Sample output

The command:

```bash
dotnet run --project src/MarkdownToDrawio -- roadmap.md roadmap.drawio
```

Produces a `.drawio` file containing:

```xml
<mxfile>
  <diagram name="Page-1">
    <mxGraphModel>
      <root>
        <mxCell id="0" />
        <mxCell id="1" parent="0" />
        <!-- Pastel shading behind each quarter column -->
        <mxCell id="qshade_0" value=""
                style="rounded=0;whiteSpace=wrap;html=1;fillColor=#E8F4FD;strokeColor=none;opacity=50;"
                vertex="1" parent="1">
          <mxGeometry x="30" y="40" width="200" height="300" as="geometry" />
        </mxCell>
        <mxCell id="qshade_1" value=""
                style="rounded=0;whiteSpace=wrap;html=1;fillColor=#FFF3E0;strokeColor=none;opacity=50;"
                vertex="1" parent="1">
          <mxGeometry x="230" y="40" width="200" height="300" as="geometry" />
        </mxCell>
        <mxCell id="qshade_2" value=""
                style="rounded=0;whiteSpace=wrap;html=1;fillColor=#E8F5E9;strokeColor=none;opacity=50;"
                vertex="1" parent="1">
          <mxGeometry x="430" y="40" width="200" height="300" as="geometry" />
        </mxCell>
        <!-- Grey line between category swimlanes -->
        <mxCell id="catsep_0" value=""
                style="line;strokeWidth=1;strokeColor=#999999;"
                vertex="1" parent="1">
          <mxGeometry x="0" y="190" width="630" height="1" as="geometry" />
        </mxCell>
        <!-- Swimlanes (one per category) -->
        <mxCell id="cat_Infrastructure" value="Infrastructure"
                style="shape=swimlane;horizontal=0;startSize=30;..."
                vertex="1" parent="1">
          <mxGeometry x="0" y="40" width="630" height="150" as="geometry" />
        </mxCell>
        <mxCell id="cat_Quality" value="Quality"
                style="shape=swimlane;horizontal=0;startSize=30;..."
                vertex="1" parent="1">
          <mxGeometry x="0" y="190" width="630" height="150" as="geometry" />
        </mxCell>
        <!-- Quarter column labels -->
        <mxCell id="qlabel_0" value="Q1 2025" ... />
        <mxCell id="qlabel_1" value="Q2 2025" ... />
        <mxCell id="qlabel_2" value="Q3 2025" ... />
        <!-- Activity nodes placed at category/quarter intersections -->
        <mxCell id="act_0" value="Design API"
                style="rounded=1;whiteSpace=wrap;html=1;"
                vertex="1" parent="cat_Infrastructure">
          <mxGeometry x="50" y="20" width="120" height="40" as="geometry" />
        </mxCell>
        <mxCell id="act_1" value="Implement Backend"
                vertex="1" parent="cat_Infrastructure">
          <mxGeometry x="250" y="20" width="120" height="40" as="geometry" />
        </mxCell>
        <mxCell id="act_2" value="Write Tests"
                vertex="1" parent="cat_Quality">
          <mxGeometry x="250" y="20" width="120" height="40" as="geometry" />
        </mxCell>
        <mxCell id="act_3" value="Deploy"
                vertex="1" parent="cat_Infrastructure">
          <mxGeometry x="450" y="20" width="120" height="40" as="geometry" />
        </mxCell>
        <!-- Dependency arrows -->
        <mxCell id="edge_0" edge="1" source="act_0" target="act_1" parent="1">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="edge_1" edge="1" source="act_1" target="act_2" parent="1">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="edge_2" edge="1" source="act_1" target="act_3" parent="1">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="edge_3" edge="1" source="act_2" target="act_3" parent="1">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
      </root>
    </mxGraphModel>
  </diagram>
</mxfile>
```

Open the `.drawio` file in Draw.io to see the rendered diagram with swimlanes, coloured quarter columns, and dependency arrows.

### Project structure

```
src/MarkdownToDrawio/
├── Program.cs              # CLI entry point and pipeline orchestration
├── Cli/                    # System.CommandLine configuration
├── Parsing/                # Markdown parser and pretty-printer
├── Validation/             # Metadata and dependency validator
├── Model/                  # Domain models (Activity, RoadmapModel)
└── Output/                 # Draw.io XML diagram generator
```

---

## Testing

Both projects have comprehensive test suites combining unit tests and property-based tests (FsCheck, 100 iterations per property).

```bash
# Run all tests
dotnet test tests/DrawioToMarkdown.Tests
dotnet test tests/MarkdownToDrawio.Tests
```

**DrawioToMarkdown** tests validate: parse/print round-trip, dangling edge filtering, edge deduplication, markdown completeness, default output path derivation.

**MarkdownToDrawio** tests validate: parse/print round-trip, metadata validation completeness, unresolved dependency detection, XML validity, swimlane structure, quarter column ordering, activity placement, dependency edge accuracy, default output path derivation.
