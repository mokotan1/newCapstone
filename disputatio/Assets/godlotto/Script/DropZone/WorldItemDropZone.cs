using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using Fungus;

public class WorldItemDropZone : MonoBehaviour, IDropHandler
{
    [Header("필요한 아이템")]
    public Item requiredItem;

    [Header("성공 시 실행될 이벤트")]
    public UnityEvent onUnlock;

    [Header("Fungus 연동")]
    [Tooltip("비우면 FlowchartLocator(Variablemanager)를 사용합니다.")]
    public Flowchart flowchart;
    [Tooltip("이 Bool이 true면 대사 중으로 보고 드롭을 막습니다.")]
    public string dialogBoolName = "isTalking";

    [Header("씬 재진입 시 복원")]
    [Tooltip("비우면 알려진 방 열쇠 아이템(itemName)에 대해 Used*Key 글로벌 bool을 자동 추론합니다.")]
    [SerializeField] private string completedGlobalBoolKeyOverride;

    private void Start()
    {
        ApplyPersistedUnlockIfNeeded();
    }

    /// <summary>
    /// 열쇠 사용 후 씬을 다시 로드하면 컴포넌트가 다시 켜지므로,
    /// 글로벌 Used*Key가 true면 상호작용·콜라이더를 끄고 열린 상태로 둡니다.
    /// </summary>
    private void ApplyPersistedUnlockIfNeeded()
    {
        if (requiredItem == null)
            return;

        string persistKey = !string.IsNullOrEmpty(completedGlobalBoolKeyOverride)
            ? completedGlobalBoolKeyOverride
            : PersistBoolKeyForItem(requiredItem);

        if (string.IsNullOrEmpty(persistKey) || !IsPersistedFungusBoolTrue(persistKey))
            return;

        ApplyDropZoneCompletedVisuals();
    }

    /// <summary>Variablemanager에 변수 항목이 없어도 Fungus 전역 저장소를 조회합니다.</summary>
    private bool IsPersistedFungusBoolTrue(string key)
    {
        Flowchart fc = FlowchartLocator.Resolve(flowchart);
        if (fc != null && fc.GetBooleanVariable(key))
            return true;

        return FlowchartLocator.GetFungusGlobalBoolean(key);
    }

    private void ApplyDropZoneCompletedVisuals()
    {
        // 잠금 해제 후에는 드롭 처리만 끈다. Fungus Clickable2D(입장 클릭)는 같은 오브젝트 또는 부모에 있을 수 있다.
        // WorldItemDropZone이 켜진 채로 두면 IDropHandler 등으로 입력이 꼬여 클릭이 안 되는 경우가 있어 항상 비활성화한다.
        bool keepColliderAndSpriteForDoorClick =
            GetComponent<Clickable2D>() != null || GetComponentInParent<Clickable2D>() != null;
        bool keepColliderForSnapTarget = GetComponent<SnapTarget>() != null;

        enabled = false;

        var col = GetComponent<Collider2D>();
        // Some ChildRoom seal targets are both inventory drop zones and snap targets.
        // After the inventory item is accepted, the same collider must remain active
        // so DragManager2D can still find the SnapTarget for the revealed world item.
        if (col != null)
        {
            if (keepColliderForSnapTarget)
                col.enabled = true;
            else if (!keepColliderAndSpriteForDoorClick)
                col.enabled = false;
        }

        var sr = GetComponent<SpriteRenderer>();
        if (sr != null && !keepColliderAndSpriteForDoorClick)
            sr.enabled = false;
    }

    /// <summary>인벤에서 월드로 드롭할 때: 콜라이더는 문(부모)에 있고 드롭존은 자식에만 둔 경우를 포함합니다.</summary>
    public static WorldItemDropZone FindFromHitCollider(Collider2D hitCollider)
    {
        if (hitCollider == null)
            return null;

        return hitCollider.GetComponent<WorldItemDropZone>()
            ?? hitCollider.GetComponentInChildren<WorldItemDropZone>();
    }

    /// <summary><see cref="Item.itemName"/> → Fungus 글로벌 bool 키 (열쇠·인장·주방 등).</summary>
    private static string PersistBoolKeyForItem(Item item)
    {
        if (item == null)
            return null;

        switch (item.itemName)
        {
            case "StudyRoomKey":
                return FungusVariableKeys.UsedStudyKey;
            case "MaidRoom_Key":
            case "MadeRoom_Key":
                return FungusVariableKeys.UsedMaidKey;
            case "TutorRoomKey":
                return FungusVariableKeys.UsedTutorKey;
            case "ChildRoomKey":
                return FungusVariableKeys.UsedChildKey;
            case "WifeRoomKey":
                return FungusVariableKeys.UsedWifeKey;
            case "BedRoomKey":
                return FungusVariableKeys.UsedBedKey;
            case "PrisonKey":
                return "UsedPrisonKey";
            case "BasementKey":
                return "UsedBasementKey";
            case "5th seal":
                return "seal5";
            case "6th seal":
                return "seal6";
            case "7th seal":
                return "seal7";
            // GetBottle = 병 획득(인벤). 싱크 드롭존 완료는 Fungus `Bottle_Dragged` 블록이 켜는 전역 bool.
            case "Bottle":
                return "BottleDragged";
            case "Food":
                return "giveFood";
            default:
                return null;
        }
    }

    public void OnDrop(PointerEventData eventData)
    {
        TryApplyDroppedItem(InventorySlot.draggedItem);
    }

    /// <summary>
    /// UI(IDropHandler)·인벤 슬롯(월드 레이캐스트) 공통: 올바른 아이템이고 대사 중이 아니면 소비합니다.
    /// </summary>
    /// <returns>실제로 사용 처리(이벤트·제거·비활성)를 했으면 true</returns>
    public bool TryApplyDroppedItem(Item dropped)
    {
        if (!CanUseWhileDialog())
            return false;

        if (dropped == null)
            return false;

        if (dropped != requiredItem)
        {
            GameLog.Log("잘못된 아이템입니다.");
            return false;
        }

        GameLog.Log($"올바른 아이템({requiredItem.itemName})을 사용했습니다!");

        onUnlock.Invoke();
        WorldSpriteDepthSorter2D.SortActiveSceneSprites();

        if (InventoryManager.instance != null)
            InventoryManager.instance.RemoveItem(requiredItem);

        ApplyDropZoneCompletedVisuals();
        return true;
    }

    private bool CanUseWhileDialog()
    {
        Flowchart fc = FlowchartLocator.Resolve(flowchart);
        if (fc == null || string.IsNullOrEmpty(dialogBoolName))
            return true;

        if (fc.GetBooleanVariable(dialogBoolName))
        {
            GameLog.Log("대사가 진행 중이라 아이템을 사용할 수 없습니다.");
            return false;
        }

        return true;
    }
}
