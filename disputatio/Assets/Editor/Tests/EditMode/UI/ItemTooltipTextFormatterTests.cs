using NUnit.Framework;

public class ItemTooltipTextFormatterTests
{
    [Test]
    public void Build_ReturnsTrimmedNameAndDescription_WhenBothValuesExist()
    {
        string result = ItemTooltipTextFormatter.Build("  Key  ", "  Opens hidden room. ");

        Assert.AreEqual("Key\nOpens hidden room.", result);
    }

    [Test]
    public void Build_UsesFallbackName_WhenNameIsMissing()
    {
        string result = ItemTooltipTextFormatter.Build("", "Readable description");

        Assert.AreEqual("Unknown Item\nReadable description", result);
    }

    [Test]
    public void Build_UsesFallbackDescription_WhenDescriptionIsMissing()
    {
        string result = ItemTooltipTextFormatter.Build("Lantern", " ");

        Assert.AreEqual("Lantern\n설명이 없습니다.", result);
    }

    [Test]
    public void Build_OmitsFallbackDescription_WhenDescriptionIsMissingButRowsExist()
    {
        var rows = new[]
        {
            new ItemTooltipRow { key = "획득 장소", value = "서재" }
        };

        string result = ItemTooltipTextFormatter.Build("Lantern", " ", rows);

        Assert.AreEqual("Lantern\n획득 장소: 서재", result);
    }

    [Test]
    public void Build_AppendsTooltipTableRows_WhenRowsHaveKeysAndValues()
    {
        var rows = new[]
        {
            new ItemTooltipRow { key = "획득 장소", value = "서재" },
            new ItemTooltipRow { key = "사용처", value = "2층 복도" }
        };

        string result = ItemTooltipTextFormatter.Build("Lantern", "어두운 곳을 밝힌다.", rows);

        Assert.AreEqual("Lantern\n어두운 곳을 밝힌다.\n획득 장소: 서재\n사용처: 2층 복도", result);
    }

    [Test]
    public void Build_SkipsTooltipTableRows_WhenRowsAreBlank()
    {
        var rows = new[]
        {
            new ItemTooltipRow { key = "획득 장소", value = "" },
            new ItemTooltipRow { key = " ", value = "서재" },
            null
        };

        string result = ItemTooltipTextFormatter.Build("Lantern", "어두운 곳을 밝힌다.", rows);

        Assert.AreEqual("Lantern\n어두운 곳을 밝힌다.", result);
    }
}
