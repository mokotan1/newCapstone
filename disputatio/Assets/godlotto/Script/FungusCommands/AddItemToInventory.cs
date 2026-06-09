using UnityEngine;
using Fungus;

/// <summary>
/// <see cref="Item.itemId"/>로 아이템을 찾아 <see cref="InventoryManager"/>에 추가하는 Fungus 커맨드.
/// <c>Invoke Method</c>와 달리 씬 오브젝트 참조가 필요 없어 프리팹/씬 변경에 안전합니다.
/// </summary>
[CommandInfo("Inventory",
             "Add Item To Inventory",
             "아이템 ID로 인벤토리에 아이템을 추가합니다. 씬 오브젝트 참조 없이 싱글톤을 사용합니다.")]
[AddComponentMenu("")]
public class AddItemToInventory : Command
{
    [Tooltip("추가할 아이템의 ID (Item ScriptableObject의 itemId)")]
    [SerializeField] private int targetItemId = 1;

    public override void OnEnter()
    {
        try
        {
            Item item = FindItemById(targetItemId);
            if (item == null)
            {
                GameLog.LogWarning($"[AddItemToInventory] targetItemId={targetItemId}에 해당하는 Item을 찾을 수 없습니다.");
                Continue();
                return;
            }

            InventoryManager inv = InventoryManager.Instance;
            if (inv == null)
            {
                GameLog.LogWarning("[AddItemToInventory] InventoryManager.Instance가 null입니다.");
                Continue();
                return;
            }

            inv.AddItem(item);
            GameLog.Log($"[AddItemToInventory] {item.itemName}(id={targetItemId})을 인벤토리에 추가했습니다.");
        }
        catch (System.Exception ex)
        {
            GameLog.LogWarning($"[AddItemToInventory] 아이템 추가 중 예외 발생: {ex.Message}");
        }

        Continue();
    }

    public override string GetSummary()
    {
        return $"targetItemId = {targetItemId}";
    }

    public override Color GetButtonColor()
    {
        return new Color32(235, 191, 76, 255);
    }

    /// <summary>
    /// 프로젝트에 로드된 모든 <see cref="Item"/> SO 중 <paramref name="id"/>와 일치하는 것을 반환합니다.
    /// </summary>
    public static Item FindItemById(int id)
    {
        return ItemLookup.FindById(id);
    }
}
