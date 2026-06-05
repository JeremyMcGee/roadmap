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
        // Arrange
        var xml = """
            <mxfile><diagram name="Page-1"><mxGraphModel><root>
              <mxCell id="0" />
              <mxCell id="1" parent="0" />
              <mxCell id="2" value="Design API" vertex="1" parent="1" />
              <mxCell id="3" value="Implement Backend" vertex="1" parent="1" />
              <mxCell id="4" value="Write Tests" vertex="1" parent="1" />
              <mxCell id="5" edge="1" source="2" target="3" parent="1" />
              <mxCell id="6" edge="1" source="3" target="4" parent="1" />
            </root></mxGraphModel></diagram></mxfile>
            """;

        // Act
        var result = _parser.Parse(xml);

        // Assert
        Assert.Equal(3, result.Nodes.Count);
        Assert.Contains(result.Nodes, n => n.Id == "2" && n.Label == "Design API");
        Assert.Contains(result.Nodes, n => n.Id == "3" && n.Label == "Implement Backend");
        Assert.Contains(result.Nodes, n => n.Id == "4" && n.Label == "Write Tests");

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
        Assert.Empty(result.Nodes);
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
              <mxCell id="2" value="&lt;b&gt;Design API&lt;/b&gt;" vertex="1" parent="1" />
              <mxCell id="3" value="&lt;div&gt;&lt;span style=&quot;font-size: 12px;&quot;&gt;Implement Backend&lt;/span&gt;&lt;/div&gt;" vertex="1" parent="1" />
              <mxCell id="4" value="Write &amp;amp; Test" vertex="1" parent="1" />
            </root></mxGraphModel></diagram></mxfile>
            """;

        // Act
        var result = _parser.Parse(xml);

        // Assert — HTML tags stripped, entities decoded
        Assert.Contains(result.Nodes, n => n.Id == "2" && n.Label == "Design API");
        Assert.Contains(result.Nodes, n => n.Id == "3" && n.Label == "Implement Backend");
        Assert.Contains(result.Nodes, n => n.Id == "4" && n.Label == "Write & Test");
    }

    [Fact]
    public void Parse_BlankLabels_SkipsActivities()
    {
        // Arrange — activities with empty, whitespace-only, or HTML-only labels should be excluded
        var xml = """
            <mxfile><diagram name="Page-1"><mxGraphModel><root>
              <mxCell id="0" />
              <mxCell id="1" parent="0" />
              <mxCell id="2" value="Real Task" vertex="1" parent="1" />
              <mxCell id="3" value="" vertex="1" parent="1" />
              <mxCell id="4" value="   " vertex="1" parent="1" />
              <mxCell id="5" value="&lt;br&gt;" vertex="1" parent="1" />
              <mxCell id="6" value="&lt;div&gt;&lt;/div&gt;" vertex="1" parent="1" />
            </root></mxGraphModel></diagram></mxfile>
            """;

        // Act
        var result = _parser.Parse(xml);

        // Assert — only the node with an actual label is kept
        Assert.Single(result.Nodes);
        Assert.Contains(result.Nodes, n => n.Id == "2" && n.Label == "Real Task");
    }

    [Fact]
    public void Parse_EdgeWithoutSourceOrTarget_SkipsEdge()
    {
        // Arrange — edges missing source and/or target attributes should be skipped
        var xml = """
            <mxfile><diagram name="Page-1"><mxGraphModel><root>
              <mxCell id="0" />
              <mxCell id="1" parent="0" />
              <mxCell id="2" value="Task A" vertex="1" parent="1" />
              <mxCell id="3" value="Task B" vertex="1" parent="1" />
              <mxCell id="10" edge="1" target="3" parent="1" />
              <mxCell id="11" edge="1" source="2" parent="1" />
              <mxCell id="12" edge="1" parent="1" />
            </root></mxGraphModel></diagram></mxfile>
            """;

        // Act
        var result = _parser.Parse(xml);

        // Assert — nodes are still extracted, but all incomplete edges are skipped
        Assert.Equal(2, result.Nodes.Count);
        Assert.Empty(result.Edges);
    }
}
