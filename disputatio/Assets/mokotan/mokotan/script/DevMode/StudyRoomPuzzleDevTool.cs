using System.Linq;
using Fungus;
using Godlotto.Interaction;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>서재 거울 퍼즐의 QA 디버그 스냅샷.</summary>
public struct StudyRoomPuzzleDebugInfo
{
    public bool IsStudyRoomScene;
    public bool HasBookmarkMirror;
    public bool DiarySolved;
    public bool HaveTutorKey;
    public bool HasPlacement;
    public MirrorPlacementDebug Placement;
}

/// <summary>
/// 서재 책갈피 거울 퍼즐(7337) QA용 개발자 모드 도구.
/// 새 지급/성공 루트를 만들지 않고 기존 <see cref="DeveloperModeItemGrantService"/>,
/// <see cref="StudyRoomMirrorPuzzleSuccessRouter"/>, <see cref="FlowchartLocator"/> 흐름을 재사용한다.
/// 변경 동작은 모두 개발자 모드 게이트(<see cref="CanUse"/>) 뒤에 둔다.
/// </summary>
public static class StudyRoomPuzzleDevTool
{
    public const string DiarySolvedKey = FungusVariableKeys.DiarySolved;
    public const string HaveTutorKeyKey = FungusVariableKeys.HaveTutorKey;
    public const string BookmarkMirrorItemName = "BookmarkMirror";
    public const string UnlockInteractionId = "unlock";
    public const string UnlockFungusBlockName = "UnlockSuccess";

    public static bool CanUse =>
        DeveloperModeController.CanUseDeveloperModeRuntime && DeveloperModeController.IsDeveloperModeEnabled;

    /// <summary>1) BookmarkMirror 즉시 지급 — 기존 단일 아이템 지급 흐름 재사용.</summary>
    public static DeveloperModeItemSelectionGrantResult GrantBookmarkMirror()
    {
        Item mirror = ResolveBookmarkMirrorItem();
        if (mirror == null)
        {
            var miss = new DeveloperModeItemSelectionGrantResult
            {
                ItemName = BookmarkMirrorItemName,
                RequestedQuantity = 1,
                FailureReason = "BookmarkMirror 아이템을 카탈로그에서 찾을 수 없습니다.",
            };
            GameLog.LogWarning($"[StudyRoomPuzzleDevTool] {miss}");
            return miss;
        }

        return DeveloperModeItemGrantService.GrantSelectedItem(mirror, 1);
    }

    /// <summary>2) 퍼즐 반복 테스트용 상태 초기화 — DiarySolved/HaveTutorKey를 false로.</summary>
    public static bool ResetPuzzle(Flowchart flowchart = null)
    {
        if (!CanUse)
        {
            GameLog.LogWarning("[StudyRoomPuzzleDevTool] 리셋 거부: 개발자 모드가 꺼져 있습니다.");
            return false;
        }

        Flowchart fc = FlowchartLocator.Resolve(flowchart);
        if (fc == null)
        {
            GameLog.LogWarning("[StudyRoomPuzzleDevTool] 리셋 실패: Flowchart(Variablemanager)를 찾을 수 없습니다.");
            return false;
        }

        EnsureBool(fc, DiarySolvedKey, false);
        EnsureBool(fc, HaveTutorKeyKey, false);
        GameLog.Log("[StudyRoomPuzzleDevTool] 서재 거울 퍼즐 초기화: DiarySolved=false, HaveTutorKey=false");
        return true;
    }

    /// <summary>3) 퍼즐 강제 성공 — 기존 성공 라우터를 재사용해 DiarySolved=true 및 UnlockSuccess 흐름을 탄다.</summary>
    public static bool ForceSolve(
        StudyRoomPuzzleController roomController = null,
        Flowchart flowchart = null,
        bool runUnlockRouting = true)
    {
        if (!CanUse)
        {
            GameLog.LogWarning("[StudyRoomPuzzleDevTool] 강제 성공 거부: 개발자 모드가 꺼져 있습니다.");
            return false;
        }

        Flowchart fc = FlowchartLocator.Resolve(flowchart);

        if (!runUnlockRouting)
        {
            if (fc == null)
            {
                GameLog.LogWarning("[StudyRoomPuzzleDevTool] 강제 성공 실패: Flowchart를 찾을 수 없습니다.");
                return false;
            }

            EnsureBool(fc, DiarySolvedKey, true);
            GameLog.Log("[StudyRoomPuzzleDevTool] 서재 거울 퍼즐 강제 성공(변수만): DiarySolved=true");
            return true;
        }

        if (roomController == null)
            roomController = Object.FindFirstObjectByType<StudyRoomPuzzleController>();

        // Flowchart도 controller도 없으면 ApplySuccess가 DiarySolved 설정·라우팅 어느 것도 수행하지 못한다.
        // 실제 상태 변경이 일어나지 않는 경우에는 성공으로 보고하지 않는다.
        if (fc == null && roomController == null)
        {
            GameLog.LogWarning(
                "[StudyRoomPuzzleDevTool] 강제 성공 실패: Flowchart(Variablemanager)와 StudyRoomPuzzleController를 모두 찾을 수 없습니다.");
            return false;
        }

        // DiarySolved 변수를 미리 보장해 라우터가 안전하게 set 할 수 있게 한다(기존 값 보존).
        if (fc != null && !fc.HasVariable(DiarySolvedKey))
            EnsureBool(fc, DiarySolvedKey, false);

        StudyRoomMirrorPuzzleSuccessRouter.ApplySuccess(
            roomController,
            fc,
            UnlockInteractionId,
            UnlockFungusBlockName,
            DiarySolvedKey,
            setSolvedBoolBeforeSuccess: true,
            preferInteractionController: true);

        GameLog.Log("[StudyRoomPuzzleDevTool] 서재 거울 퍼즐 강제 성공: SuccessRouter 경유");
        return true;
    }

    /// <summary>4) 개발자 패널 표시용 디버그 정보 수집(읽기 전용, 상태를 바꾸지 않음).</summary>
    public static StudyRoomPuzzleDebugInfo CaptureDebugInfo(Flowchart flowchart = null, string activeSceneName = null)
    {
        string sceneName = activeSceneName ?? SceneManager.GetActiveScene().name;
        Flowchart fc = FlowchartLocator.Resolve(flowchart);

        var info = new StudyRoomPuzzleDebugInfo
        {
            IsStudyRoomScene = string.Equals(sceneName, SceneNames.StudyRoom, System.StringComparison.Ordinal),
            HasBookmarkMirror = InventoryHasBookmarkMirror(),
            DiarySolved = ReadBool(fc, DiarySolvedKey),
            HaveTutorKey = ReadBool(fc, HaveTutorKeyKey),
        };

        var puzzle = Object.FindFirstObjectByType<StudyRoomDiaryMirrorPuzzleController>();
        if (puzzle != null && puzzle.TryGetPlacementDebug(out MirrorPlacementDebug placement))
        {
            info.HasPlacement = true;
            info.Placement = placement;
        }

        return info;
    }

    internal static Item ResolveBookmarkMirrorItem()
    {
        var items = ItemLookup.GetAllItems();
        if (items == null)
            return null;

        for (int i = 0; i < items.Count; i++)
        {
            Item item = items[i];
            if (item != null && string.Equals(item.itemName, BookmarkMirrorItemName, System.StringComparison.Ordinal))
                return item;
        }

        return null;
    }

    internal static bool InventoryHasBookmarkMirror()
    {
        InventoryManager inventory = InventoryManager.Instance;
        if (inventory == null || inventory.Items == null)
            return false;

        return inventory.Items.Any(item =>
            item != null && string.Equals(item.itemName, BookmarkMirrorItemName, System.StringComparison.Ordinal));
    }

    static bool ReadBool(Flowchart flowchart, string key)
    {
        if (flowchart != null && flowchart.HasVariable(key))
            return flowchart.GetBooleanVariable(key);

        return FlowchartLocator.GetFungusGlobalBoolean(key);
    }

    static void EnsureBool(Flowchart flowchart, string key, bool value)
    {
        if (flowchart == null || string.IsNullOrEmpty(key))
            return;

        if (!flowchart.HasVariable(key))
        {
            var variable = flowchart.gameObject.AddComponent<BooleanVariable>();
            variable.Key = key;
            variable.Scope = VariableScope.Public;
            variable.Value = value;
            flowchart.Variables.Add(variable);
            return;
        }

        flowchart.SetBooleanVariable(key, value);
    }
}
