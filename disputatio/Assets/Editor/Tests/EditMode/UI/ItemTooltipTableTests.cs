using NUnit.Framework;

public class ItemTooltipTableTests
{
    [Test]
    public void FromCsv_ReturnsRowsForMatchingItemId()
    {
        const string csv =
            "item_id,unity_item_name,display_name_ko,tooltip_key,tooltip_value_ko,source_area,source_evidence,asset_path,apply_status\n" +
            "1,Bottle,불투명한 병,획득 장소,현관/중앙홀의 화분 안,2막,근거,path,초안\n" +
            "1,Bottle,불투명한 병,사용처,주방 싱크대에서 물을 채운다,2막,근거,path,초안\n" +
            "2,Food,썩은 육포,획득 장소,가정부 방,2막,근거,path,초안\n";

        ItemTooltipTable table = ItemTooltipTable.FromCsv(csv);
        ItemTooltipContent content = table.GetContent(1, "Bottle", "");

        Assert.AreEqual("불투명한 병", content.itemName);
        Assert.AreEqual(2, content.rows.Count);
        Assert.AreEqual("획득 장소", content.rows[0].key);
        Assert.AreEqual("현관/중앙홀의 화분 안", content.rows[0].value);
        Assert.AreEqual("사용처", content.rows[1].key);
    }

    [Test]
    public void GetContent_FallsBackToItemAssetValues_WhenTableHasNoRows()
    {
        ItemTooltipTable table = ItemTooltipTable.FromCsv("item_id,display_name_ko,tooltip_key,tooltip_value_ko\n");

        ItemTooltipContent content = table.GetContent(99, "Lantern", "Old description");

        Assert.AreEqual("Lantern", content.itemName);
        Assert.AreEqual("Old description", content.itemDescription);
        Assert.AreEqual(0, content.rows.Count);
    }
}
