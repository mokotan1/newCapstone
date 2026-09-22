using Fungus;
using UnityEngine;

/// <summary>
/// UtilityRoom 전기 패널 스위치 UI를 Fungus <see cref="FungusVariableKeys.ElectricOn"/>과 동기화합니다.
/// 씬 재진입 시 저장된 전원 상태에 맞는 ON/OFF 스프라이트가 표시되도록 합니다.
/// </summary>
[DisallowMultipleComponent]
public class ElectricSwitchUiSync : MonoBehaviour
{
    const string OnSwitchChildName = "on_switch";
    const string OffSwitchChildName = "off_Switch";

    [SerializeField] private GameObject onSwitchGraphic;
    [SerializeField] private GameObject offSwitchGraphic;
    [Tooltip("비우면 FlowchartLocator(Variablemanager)를 사용합니다.")]
    [SerializeField] private Flowchart flowchartOverride;
    [Tooltip("On/Off 블록을 실행할 UtilityRoom Flowchart. 비우면 씬에서 첫 Flowchart를 사용합니다.")]
    [SerializeField] private Flowchart utilityFlowchart;
    [SerializeField] private string turnOnBlockName = "OnSwitch_Clicked";
    [SerializeField] private string turnOffBlockName = "OffSwitch_Clicked";
    [SerializeField] private string fungusBooleanKey = FungusVariableKeys.ElectricOn;
    [Tooltip("Variablemanager에 키가 없을 때 Fungus GlobalVariables도 조회합니다.")]
    [SerializeField] private bool checkGlobalVariables = true;

    private bool _initialized;
    private bool _lastElectricOn;

    private void Awake()
    {
        ResolveSwitchReferences();
    }

    private void OnEnable()
    {
        _initialized = false;
    }

    private void Start()
    {
        RefreshFromFungus();
    }

    private void LateUpdate()
    {
        if (onSwitchGraphic == null && offSwitchGraphic == null)
            return;

        bool electricOn = IsElectricOn();
        if (_initialized && electricOn == _lastElectricOn)
            return;

        _lastElectricOn = electricOn;
        _initialized = true;
        ApplySwitchVisibility(electricOn, onSwitchGraphic, offSwitchGraphic);
    }

    /// <summary>ON 스프라이트 클릭: 전원을 끕니다 (기존 Fungus OffSwitch 블록 재사용).</summary>
    public void HandleOnGraphicClicked()
    {
        ExecuteUtilityBlock(turnOffBlockName);
    }

    /// <summary>OFF 스프라이트 클릭: 전원을 켭니다 (기존 Fungus OnSwitch 블록 재사용).</summary>
    public void HandleOffGraphicClicked()
    {
        ExecuteUtilityBlock(turnOnBlockName);
    }

    /// <summary>UnityEvent·Fungus Call Method 등에서 즉시 반영할 때 호출합니다.</summary>
    public void RefreshFromFungus()
    {
        if (onSwitchGraphic == null && offSwitchGraphic == null)
            ResolveSwitchReferences();

        bool electricOn = IsElectricOn();
        _lastElectricOn = electricOn;
        _initialized = true;
        ApplySwitchVisibility(electricOn, onSwitchGraphic, offSwitchGraphic);
    }

    private void ResolveSwitchReferences()
    {
        if (onSwitchGraphic == null)
        {
            Transform on = transform.Find(OnSwitchChildName);
            if (on != null)
                onSwitchGraphic = on.gameObject;
        }

        if (offSwitchGraphic == null)
        {
            Transform off = transform.Find(OffSwitchChildName);
            if (off != null)
                offSwitchGraphic = off.gameObject;
        }
    }

    private void ExecuteUtilityBlock(string blockName)
    {
        if (string.IsNullOrWhiteSpace(blockName))
            return;

        Flowchart fc = ResolveUtilityFlowchart();
        if (fc == null)
            return;

        fc.ExecuteBlock(blockName);
        RefreshFromFungus();
    }

    private Flowchart ResolveUtilityFlowchart()
    {
        if (utilityFlowchart != null)
            return utilityFlowchart;

        Flowchart[] flowcharts = FindObjectsByType<Flowchart>(FindObjectsSortMode.None);
        for (int i = 0; i < flowcharts.Length; i++)
        {
            Flowchart candidate = flowcharts[i];
            if (candidate != null && candidate.HasBlock(turnOnBlockName))
                return candidate;
        }

        return FlowchartLocator.Resolve(flowchartOverride);
    }

    private bool IsElectricOn()
    {
        Flowchart fc = FlowchartLocator.Resolve(flowchartOverride);
        if (fc != null && !string.IsNullOrEmpty(fungusBooleanKey) && fc.GetBooleanVariable(fungusBooleanKey))
            return true;

        if (checkGlobalVariables && !string.IsNullOrEmpty(fungusBooleanKey)
            && FlowchartLocator.GetFungusGlobalBoolean(fungusBooleanKey))
            return true;

        return false;
    }

    /// <summary>
    /// <paramref name="isElectricOn"/>이 true면 <c>panel_switch_on</c>, false면 <c>panel_switch_off</c>를 표시합니다.
    /// </summary>
    public static void ApplySwitchVisibility(bool isElectricOn, GameObject onSwitchGraphic, GameObject offSwitchGraphic)
    {
        if (onSwitchGraphic != null)
            onSwitchGraphic.SetActive(isElectricOn);

        if (offSwitchGraphic != null)
            offSwitchGraphic.SetActive(!isElectricOn);
    }
}
