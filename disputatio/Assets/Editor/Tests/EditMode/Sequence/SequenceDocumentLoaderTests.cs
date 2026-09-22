using Godlotto.Sequence;
using NUnit.Framework;

[TestFixture]
public class SequenceDocumentLoaderTests
{
    [Test]
    public void Parse_ValidJson_ReturnsDocument()
    {
        const string json =
            "{\"schemaVersion\":1,\"blocks\":[{\"id\":\"start\",\"commands\":[{\"command\":\"set_bool\",\"key\":\"x\",\"bool_value\":true}]}]}";

        SequenceDocument document = SequenceDocumentLoader.Parse(json);

        Assert.AreEqual(1, document.schemaVersion);
        Assert.AreEqual(1, document.blocks.Length);
        Assert.AreEqual("start", document.blocks[0].id);
    }

    [Test]
    public void Parse_Empty_Throws()
    {
        SequencePlayException ex = Assert.Throws<SequencePlayException>(() => SequenceDocumentLoader.Parse(" "));
        Assert.AreEqual("invalid_document", ex.Code);
    }
}
