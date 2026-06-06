using System.Xml;
using DrawioToMarkdown.Parsing;
using Xunit;

namespace DrawioToMarkdown.Tests.Parsing;

public class DrawioParserTests
{
    private readonly DrawioParser _parser = new();

    [Fact]
    public void Parse_ValidXml_ExtractsNodesAndEdges()
    {
        // Arrange — activity nodes need the rounded=1;whiteSpace=wrap;html=1 style
        var xml = """
            <mxfile><diagram name="Page-1"><mxGraphModel><root>
              <mxCell id="0" />
              <mxCell id="1" parent="0" />
              <mxCell id="2" value="Design API" style="rounded=1;whiteSpace=wrap;html=1" vertex="1" parent="1"><mxGeometry x="10" y="10" width="100" height="40" /></mxCell>
              <mxCell id="3" value="Implement Backend" style="rounded=1;whiteSpace=wrap;html=1" vertex="1" parent="1"><mxGeometry x="10" y="60" width="100" height="40" /></mxCell>
              <mxCell id="4" value="Write Tests" style="rounded=1;whiteSpace=wrap;html=1" vertex="1" parent="1"><mxGeometry x="10" y="110" width="100" height="40" /></mxCell>
              <mxCell id="5" edge="1" source="2" target="3" parent="1" />
              <mxCell id="6" edge="1" source="3" target="4" parent="1" />
            </root></mxGraphModel></diagram></mxfile>
            """;

        // Act
        var result = _parser.Parse(xml);

        // Assert
        Assert.Equal(3, result.ActivityNodes.Count);
        Assert.Contains(result.ActivityNodes, n => n.Id == "2" && n.Label == "Design API");
        Assert.Contains(result.ActivityNodes, n => n.Id == "3" && n.Label == "Implement Backend");
        Assert.Contains(result.ActivityNodes, n => n.Id == "4" && n.Label == "Write Tests");

        Assert.Equal(2, result.Edges.Count);
        Assert.Contains(result.Edges, e => e.SourceId == "2" && e.TargetId == "3");
        Assert.Contains(result.Edges, e => e.SourceId == "3" && e.TargetId == "4");
    }

    [Fact]
    public void Parse_MalformedXml_ThrowsException()
    {
        // Arrange
        var malformedXml = "<mxfile><diagram><not-closed>";

        // Act & Assert
        Assert.Throws<XmlException>(() => _parser.Parse(malformedXml));
    }

    [Fact]
    public void Parse_NoVertexNodes_ReturnsEmptyNodes()
    {
        // Arrange — valid structure with only infrastructure cells, no vertex cells
        var xml = """
            <mxfile><diagram name="Page-1"><mxGraphModel><root>
              <mxCell id="0" />
              <mxCell id="1" parent="0" />
            </root></mxGraphModel></diagram></mxfile>
            """;

        // Act
        var result = _parser.Parse(xml);

        // Assert
        Assert.Empty(result.ActivityNodes);
        Assert.Empty(result.Edges);
    }

    [Fact]
    public void Parse_HtmlFormattedLabels_StripsHtmlTags()
    {
        // Arrange — draw.io often stores labels with HTML formatting
        var xml = """
            <mxfile><diagram name="Page-1"><mxGraphModel><root>
              <mxCell id="0" />
              <mxCell id="1" parent="0" />
              <mxCell id="2" value="&lt;b&gt;Design API&lt;/b&gt;" style="rounded=1;whiteSpace=wrap;html=1" vertex="1" parent="1"><mxGeometry x="0" y="0" width="100" height="40" /></mxCell>
              <mxCell id="3" value="&lt;div&gt;&lt;span style=&quot;font-size: 12px;&quot;&gt;Implement Backend&lt;/span&gt;&lt;/div&gt;" style="rounded=1;whiteSpace=wrap;html=1" vertex="1" parent="1"><mxGeometry x="0" y="50" width="100" height="40" /></mxCell>
              <mxCell id="4" value="Write &amp;amp; Test" style="rounded=1;whiteSpace=wrap;html=1" vertex="1" parent="1"><mxGeometry x="0" y="100" width="100" height="40" /></mxCell>
            </root></mxGraphModel></diagram></mxfile>
            """;

        // Act
        var result = _parser.Parse(xml);

        // Assert — HTML tags stripped, entities decoded
        Assert.Contains(result.ActivityNodes, n => n.Id == "2" && n.Label == "Design API");
        Assert.Contains(result.ActivityNodes, n => n.Id == "3" && n.Label == "Implement Backend");
        Assert.Contains(result.ActivityNodes, n => n.Id == "4" && n.Label == "Write & Test");
    }

    [Fact]
    public void Parse_BlankLabels_SkipsActivities()
    {
        // Arrange — activities with empty, whitespace-only, or HTML-only labels should be excluded
        var xml = """
            <mxfile><diagram name="Page-1"><mxGraphModel><root>
              <mxCell id="0" />
              <mxCell id="1" parent="0" />
              <mxCell id="2" value="Real Task" style="rounded=1;whiteSpace=wrap;html=1" vertex="1" parent="1"><mxGeometry x="0" y="0" width="100" height="40" /></mxCell>
              <mxCell id="3" value="" style="rounded=1;whiteSpace=wrap;html=1" vertex="1" parent="1"><mxGeometry x="0" y="50" width="100" height="40" /></mxCell>
              <mxCell id="4" value="   " style="rounded=1;whiteSpace=wrap;html=1" vertex="1" parent="1"><mxGeometry x="0" y="100" width="100" height="40" /></mxCell>
              <mxCell id="5" value="&lt;br&gt;" style="rounded=1;whiteSpace=wrap;html=1" vertex="1" parent="1"><mxGeometry x="0" y="150" width="100" height="40" /></mxCell>
              <mxCell id="6" value="&lt;div&gt;&lt;/div&gt;" style="rounded=1;whiteSpace=wrap;html=1" vertex="1" parent="1"><mxGeometry x="0" y="200" width="100" height="40" /></mxCell>
            </root></mxGraphModel></diagram></mxfile>
            """;

        // Act
        var result = _parser.Parse(xml);

        // Assert — only the node with an actual label is kept
        Assert.Single(result.ActivityNodes);
        Assert.Contains(result.ActivityNodes, n => n.Id == "2" && n.Label == "Real Task");
    }

    [Fact]
    public void Parse_EdgeWithoutSourceOrTarget_SkipsEdge()
    {
        // Arrange — edges missing source and/or target attributes should be skipped
        var xml = """
            <mxfile><diagram name="Page-1"><mxGraphModel><root>
              <mxCell id="0" />
              <mxCell id="1" parent="0" />
              <mxCell id="2" value="Task A" style="rounded=1;whiteSpace=wrap;html=1" vertex="1" parent="1"><mxGeometry x="0" y="0" width="100" height="40" /></mxCell>
              <mxCell id="3" value="Task B" style="rounded=1;whiteSpace=wrap;html=1" vertex="1" parent="1"><mxGeometry x="0" y="50" width="100" height="40" /></mxCell>
              <mxCell id="10" edge="1" target="3" parent="1" />
              <mxCell id="11" edge="1" source="2" parent="1" />
              <mxCell id="12" edge="1" parent="1" />
            </root></mxGraphModel></diagram></mxfile>
            """;

        // Act
        var result = _parser.Parse(xml);

        // Assert — nodes are still extracted, but all incomplete edges are skipped
        Assert.Equal(2, result.ActivityNodes.Count);
        Assert.Empty(result.Edges);
    }

    [Fact]
    public void Parse_SelfReferencingEdge_IsDiscarded()
    {
        // Arrange — an edge where source == target should be discarded (Req 3.5)
        var xml = """
            <mxfile><diagram name="Page-1"><mxGraphModel><root>
              <mxCell id="0" />
              <mxCell id="1" parent="0" />
              <mxCell id="2" value="Task A" style="rounded=1;whiteSpace=wrap;html=1" vertex="1" parent="1"><mxGeometry x="0" y="0" width="100" height="40" /></mxCell>
              <mxCell id="3" value="Task B" style="rounded=1;whiteSpace=wrap;html=1" vertex="1" parent="1"><mxGeometry x="0" y="50" width="100" height="40" /></mxCell>
              <mxCell id="10" edge="1" source="2" target="2" parent="1" />
              <mxCell id="11" edge="1" source="2" target="3" parent="1" />
            </root></mxGraphModel></diagram></mxfile>
            """;

        // Act
        var result = _parser.Parse(xml);

        // Assert — self-referencing edge discarded, valid edge kept
        Assert.Single(result.Edges);
        Assert.Contains(result.Edges, e => e.SourceId == "2" && e.TargetId == "3");
    }

    [Fact]
    public void Parse_DuplicateEdges_AreDeduped()
    {
        // Arrange — multiple edges with the same source-target pair (Req 3.3)
        var xml = """
            <mxfile><diagram name="Page-1"><mxGraphModel><root>
              <mxCell id="0" />
              <mxCell id="1" parent="0" />
              <mxCell id="2" value="Task A" style="rounded=1;whiteSpace=wrap;html=1" vertex="1" parent="1"><mxGeometry x="0" y="0" width="100" height="40" /></mxCell>
              <mxCell id="3" value="Task B" style="rounded=1;whiteSpace=wrap;html=1" vertex="1" parent="1"><mxGeometry x="0" y="50" width="100" height="40" /></mxCell>
              <mxCell id="10" edge="1" source="2" target="3" parent="1" />
              <mxCell id="11" edge="1" source="2" target="3" parent="1" />
              <mxCell id="12" edge="1" source="2" target="3" parent="1" />
            </root></mxGraphModel></diagram></mxfile>
            """;

        // Act
        var result = _parser.Parse(xml);

        // Assert — only one edge for the 2→3 pair
        Assert.Single(result.Edges);
        Assert.Contains(result.Edges, e => e.SourceId == "2" && e.TargetId == "3");
    }

    [Fact]
    public void Parse_DanglingEdges_AreDiscarded()
    {
        // Arrange — edges referencing non-activity node IDs (Req 3.2)
        var xml = """
            <mxfile><diagram name="Page-1"><mxGraphModel><root>
              <mxCell id="0" />
              <mxCell id="1" parent="0" />
              <mxCell id="2" value="Task A" style="rounded=1;whiteSpace=wrap;html=1" vertex="1" parent="1"><mxGeometry x="0" y="0" width="100" height="40" /></mxCell>
              <mxCell id="3" value="Task B" style="rounded=1;whiteSpace=wrap;html=1" vertex="1" parent="1"><mxGeometry x="0" y="50" width="100" height="40" /></mxCell>
              <mxCell id="10" edge="1" source="2" target="99" parent="1" />
              <mxCell id="11" edge="1" source="88" target="3" parent="1" />
              <mxCell id="12" edge="1" source="2" target="3" parent="1" />
            </root></mxGraphModel></diagram></mxfile>
            """;

        // Act
        var result = _parser.Parse(xml);

        // Assert — only the valid edge (2→3) survives; dangling edges are discarded
        Assert.Single(result.Edges);
        Assert.Contains(result.Edges, e => e.SourceId == "2" && e.TargetId == "3");
    }

    [Fact]
    public void Parse_DuplicateLabels_MergesActivityNodesAndEdges()
    {
        // Arrange — two nodes with the same label "Task A" should be merged (Req 6.5)
        // Node "2" (Task A) has edge to Node "4" (Task C)
        // Node "3" (Task A duplicate) has edge from Node "4" (Task C)
        // After merge, single "Task A" entry should have both edges
        var xml = """
            <mxfile><diagram name="Page-1"><mxGraphModel><root>
              <mxCell id="0" />
              <mxCell id="1" parent="0" />
              <mxCell id="2" value="Task A" style="rounded=1;whiteSpace=wrap;html=1" vertex="1" parent="1"><mxGeometry x="0" y="0" width="100" height="40" /></mxCell>
              <mxCell id="3" value="Task A" style="rounded=1;whiteSpace=wrap;html=1" vertex="1" parent="1"><mxGeometry x="0" y="50" width="100" height="40" /></mxCell>
              <mxCell id="4" value="Task C" style="rounded=1;whiteSpace=wrap;html=1" vertex="1" parent="1"><mxGeometry x="0" y="100" width="100" height="40" /></mxCell>
              <mxCell id="10" edge="1" source="2" target="4" parent="1" />
              <mxCell id="11" edge="1" source="4" target="3" parent="1" />
            </root></mxGraphModel></diagram></mxfile>
            """;

        // Act
        var result = _parser.Parse(xml);

        // Assert — only 2 unique activity labels (Task A + Task C)
        Assert.Equal(2, result.ActivityNodes.Count);
        Assert.Contains(result.ActivityNodes, n => n.Label == "Task A");
        Assert.Contains(result.ActivityNodes, n => n.Label == "Task C");

        // After merge: node 3 becomes node 2 (representative).
        // Edge source="2" target="4" remains as-is (2→4)
        // Edge source="4" target="3" becomes (4→2) — i.e., Task C → Task A
        Assert.Equal(2, result.Edges.Count);
        var repId = result.ActivityNodes.First(n => n.Label == "Task A").Id;
        var taskCId = result.ActivityNodes.First(n => n.Label == "Task C").Id;
        Assert.Contains(result.Edges, e => e.SourceId == repId && e.TargetId == taskCId);
        Assert.Contains(result.Edges, e => e.SourceId == taskCId && e.TargetId == repId);
    }

    [Fact]
    public void Parse_DuplicateLabels_MergingCausesSelfRef_IsDiscarded()
    {
        // Arrange — two nodes with the same label that have an edge between them
        // After merging, the edge becomes a self-ref and should be discarded
        var xml = """
            <mxfile><diagram name="Page-1"><mxGraphModel><root>
              <mxCell id="0" />
              <mxCell id="1" parent="0" />
              <mxCell id="2" value="Task A" style="rounded=1;whiteSpace=wrap;html=1" vertex="1" parent="1"><mxGeometry x="0" y="0" width="100" height="40" /></mxCell>
              <mxCell id="3" value="Task A" style="rounded=1;whiteSpace=wrap;html=1" vertex="1" parent="1"><mxGeometry x="0" y="50" width="100" height="40" /></mxCell>
              <mxCell id="10" edge="1" source="2" target="3" parent="1" />
            </root></mxGraphModel></diagram></mxfile>
            """;

        // Act
        var result = _parser.Parse(xml);

        // Assert — after merging, the edge 2→3 becomes a self-ref (both map to representative "2") and is discarded
        Assert.Single(result.ActivityNodes);
        Assert.Empty(result.Edges);
    }

    [Fact]
    public void Parse_SwimlaneExtraction_ExtractsGeometryCorrectly()
    {
        // Arrange — swimlane with shape=swimlane;horizontal=0 style (Req 1.2)
        var xml = """
            <mxfile><diagram name="Page-1"><mxGraphModel><root>
              <mxCell id="0" />
              <mxCell id="1" parent="0" />
              <mxCell id="lane1" value="Infrastructure" style="shape=swimlane;horizontal=0;fillColor=#dae8fc;" vertex="1" parent="1"><mxGeometry x="20" y="30" width="800" height="200" /></mxCell>
            </root></mxGraphModel></diagram></mxfile>
            """;

        // Act
        var result = _parser.Parse(xml);

        // Assert — swimlane extracted with correct Id, Label, X, Y, Width, Height
        Assert.Single(result.Swimlanes);
        var lane = result.Swimlanes[0];
        Assert.Equal("lane1", lane.Id);
        Assert.Equal("Infrastructure", lane.Label);
        Assert.Equal(20.0, lane.X);
        Assert.Equal(30.0, lane.Y);
        Assert.Equal(800.0, lane.Width);
        Assert.Equal(200.0, lane.Height);
    }

    [Fact]
    public void Parse_QuarterColumnExtraction_ExtractsWithQlabelPrefix()
    {
        // Arrange — quarter column label with id starting "qlabel_" (Req 1.3)
        var xml = """
            <mxfile><diagram name="Page-1"><mxGraphModel><root>
              <mxCell id="0" />
              <mxCell id="1" parent="0" />
              <mxCell id="qlabel_q1" value="Q1 2025" style="text;align=center;" vertex="1" parent="1"><mxGeometry x="100" y="5" width="200" height="20" /></mxCell>
            </root></mxGraphModel></diagram></mxfile>
            """;

        // Act
        var result = _parser.Parse(xml);

        // Assert — quarter column extracted with correct Id, Label, X, Width
        Assert.Single(result.QuarterColumns);
        var col = result.QuarterColumns[0];
        Assert.Equal("qlabel_q1", col.Id);
        Assert.Equal("Q1 2025", col.Label);
        Assert.Equal(100.0, col.X);
        Assert.Equal(200.0, col.Width);
    }

    [Fact]
    public void Parse_MissingMxfileStructure_ThrowsInvalidOperationException()
    {
        // Arrange — valid XML but missing the expected mxfile/diagram/mxGraphModel/root hierarchy (Req 1.8)
        var xmlMissingDiagram = "<mxfile><notdiagram /></mxfile>";
        var xmlMissingRoot = "<mxfile><diagram><mxGraphModel></mxGraphModel></diagram></mxfile>";
        var xmlMissingModel = "<mxfile><diagram><notGraphModel /></diagram></mxfile>";

        // Act & Assert — each should throw InvalidOperationException
        Assert.Throws<InvalidOperationException>(() => _parser.Parse(xmlMissingDiagram));
        Assert.Throws<InvalidOperationException>(() => _parser.Parse(xmlMissingRoot));
        Assert.Throws<InvalidOperationException>(() => _parser.Parse(xmlMissingModel));
    }

    [Fact]
    public void Parse_ActivityNodeGeometry_ExtractsPositionAndSize()
    {
        // Arrange — activity node with specific geometry values (Req 1.4)
        var xml = """
            <mxfile><diagram name="Page-1"><mxGraphModel><root>
              <mxCell id="0" />
              <mxCell id="1" parent="0" />
              <mxCell id="a1" value="Deploy Service" style="rounded=1;whiteSpace=wrap;html=1" vertex="1" parent="1"><mxGeometry x="150" y="75" width="120" height="60" /></mxCell>
            </root></mxGraphModel></diagram></mxfile>
            """;

        // Act
        var result = _parser.Parse(xml);

        // Assert — geometry values correctly parsed into ActivityNodeDef
        Assert.Single(result.ActivityNodes);
        var node = result.ActivityNodes[0];
        Assert.Equal("a1", node.Id);
        Assert.Equal("Deploy Service", node.Label);
        Assert.Equal(150.0, node.X);
        Assert.Equal(75.0, node.Y);
        Assert.Equal(120.0, node.Width);
        Assert.Equal(60.0, node.Height);
    }

    [Fact]
    public void Parse_MultipleSwimlanesAndQuarterColumns_ExtractsAllCorrectly()
    {
        // Arrange — multiple swimlanes and quarter columns with various geometries
        var xml = """
            <mxfile><diagram name="Page-1"><mxGraphModel><root>
              <mxCell id="0" />
              <mxCell id="1" parent="0" />
              <mxCell id="lane1" value="Infrastructure" style="shape=swimlane;horizontal=0;fillColor=#dae8fc;" vertex="1" parent="1"><mxGeometry x="20" y="30" width="800" height="200" /></mxCell>
              <mxCell id="lane2" value="Application" style="shape=swimlane;horizontal=0;fillColor=#d5e8d4;" vertex="1" parent="1"><mxGeometry x="20" y="230" width="800" height="180" /></mxCell>
              <mxCell id="lane3" value="&lt;b&gt;Testing&lt;/b&gt;" style="shape=swimlane;horizontal=0;strokeColor=#666;" vertex="1" parent="1"><mxGeometry x="20" y="410" width="800" height="150" /></mxCell>
              <mxCell id="qlabel_q1" value="Q1 2025" style="text;align=center;" vertex="1" parent="1"><mxGeometry x="100" y="5" width="200" height="20" /></mxCell>
              <mxCell id="qlabel_q2" value="Q2 2025" style="text;align=center;" vertex="1" parent="1"><mxGeometry x="300" y="5" width="200" height="20" /></mxCell>
              <mxCell id="qlabel_q3" value="&lt;i&gt;Q3 2025&lt;/i&gt;" style="text;align=center;" vertex="1" parent="1"><mxGeometry x="500" y="5" width="200" height="20" /></mxCell>
            </root></mxGraphModel></diagram></mxfile>
            """;

        // Act
        var result = _parser.Parse(xml);

        // Assert — all swimlanes extracted with correct geometry
        Assert.Equal(3, result.Swimlanes.Count);

        Assert.Contains(result.Swimlanes, s => s.Id == "lane1" && s.Label == "Infrastructure"
            && s.X == 20.0 && s.Y == 30.0 && s.Width == 800.0 && s.Height == 200.0);
        Assert.Contains(result.Swimlanes, s => s.Id == "lane2" && s.Label == "Application"
            && s.X == 20.0 && s.Y == 230.0 && s.Width == 800.0 && s.Height == 180.0);
        Assert.Contains(result.Swimlanes, s => s.Id == "lane3" && s.Label == "Testing"
            && s.X == 20.0 && s.Y == 410.0 && s.Width == 800.0 && s.Height == 150.0);

        // Assert — all quarter columns extracted with correct geometry
        Assert.Equal(3, result.QuarterColumns.Count);

        Assert.Contains(result.QuarterColumns, q => q.Id == "qlabel_q1" && q.Label == "Q1 2025"
            && q.X == 100.0 && q.Width == 200.0);
        Assert.Contains(result.QuarterColumns, q => q.Id == "qlabel_q2" && q.Label == "Q2 2025"
            && q.X == 300.0 && q.Width == 200.0);
        Assert.Contains(result.QuarterColumns, q => q.Id == "qlabel_q3" && q.Label == "Q3 2025"
            && q.X == 500.0 && q.Width == 200.0);
    }
}
