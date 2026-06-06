using System.Collections.Generic;
using Fungus;
using UnityEngine;

/// <summary>
/// 다크 글래스 선택지 메뉴를 표시하는 Fungus 커맨드. 옵션 리스트의 길이가 선택지 개수이며,
/// 앵커 프리셋 + 오프셋으로 위치를 지정합니다. Fungus 원본 Menu 커맨드와 달리 커맨드 하나가
/// 여러 선택지를 모두 보유합니다. 블록의 마지막 커맨드로 사용하세요.
/// </summary>
[CommandInfo("Narrative",
             "Glass Menu",
             "다크 글래스 선택지 메뉴를 표시합니다(개수·문구·위치를 한 커맨드에서 지정).")]
[AddComponentMenu("")]
public class GlassMenu : Command, IBlockCaller
{
    [Tooltip("선택지 목록. 리스트 길이가 곧 선택지 개수입니다.")]
    [SerializeField] private List<GlassMenuOption> options = new List<GlassMenuOption>();

    [Tooltip("메뉴 패널을 화면 어디에 정렬할지.")]
    [SerializeField] private MenuAnchor anchor = MenuAnchor.BottomCenter;

    [Tooltip("앵커 기준 픽셀 오프셋.")]
    [SerializeField] private Vector2 menuOffset = Vector2.zero;

    [Tooltip("(선택) 특정 GlassMenuDialog로 override. 비우면 씬/Resources에서 자동 사용.")]
    [SerializeField] private GlassMenuDialog setMenuDialog;

    public override void OnEnter()
    {
        if (setMenuDialog != null)
            GlassMenuDialog.ActiveGlassMenuDialog = setMenuDialog;

        var dialog = GlassMenuDialog.GetMenuDialog();
        if (dialog == null)
        {
            GameLog.LogWarning("[GlassMenu] GlassMenuDialog를 찾거나 생성할 수 없습니다.");
            Continue();
            return;
        }

        dialog.SetActive(true);
        dialog.Clear();
        dialog.ApplyPlacement(anchor, menuOffset);

        var flowchart = GetFlowchart();
        foreach (var option in options)
        {
            if (option == null)
                continue;
            string displayText = flowchart.SubstituteVariables(option.text);
            dialog.AddOption(displayText, option.interactable, option.targetBlock);
        }

        Continue();
    }

    public override void GetConnectedBlocks(ref List<Block> connectedBlocks)
    {
        foreach (var option in options)
        {
            if (option != null && option.targetBlock != null)
                connectedBlocks.Add(option.targetBlock);
        }
    }

    public bool MayCallBlock(Block block)
    {
        foreach (var option in options)
        {
            if (option != null && option.targetBlock == block)
                return true;
        }
        return false;
    }

    public override string GetSummary()
    {
        if (options == null || options.Count == 0)
            return "Error: No options";
        return options.Count + " options";
    }

    public override Color GetButtonColor()
    {
        return new Color32(184, 210, 235, 255);
    }
}
