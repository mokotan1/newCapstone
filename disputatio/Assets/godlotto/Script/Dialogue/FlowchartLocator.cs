using Fungus;
using UnityEngine;

/// <summary>
/// "Variablemanager" 글로벌 Flowchart를 찾아 반환하는 공유 유틸.
/// 여러 스크립트에서 <c>GameObject.Find("Variablemanager")</c>를 반복하지 않도록 합니다.
/// </summary>
public static class FlowchartLocator
{
    private const string GlobalFlowchartName = "Variablemanager";

    /// <summary>
    /// 씬에서 "Variablemanager" 이름의 GameObject를 찾아 Flowchart를 반환합니다.
    /// 없으면 null을 반환하고 경고 로그를 남깁니다.
    /// </summary>
    public static Flowchart Find()
    {
        return FindByGameObjectName(GlobalFlowchartName);
    }

    /// <summary>
    /// 인스펙터에 연결된 Flowchart가 있으면 그것을, 없으면 글로벌 Variablemanager Flowchart를 반환합니다.
    /// </summary>
    public static Flowchart Resolve(Flowchart preferred)
    {
        return preferred != null ? preferred : Find();
    }

    /// <summary>
    /// <paramref name="gameObjectName"/>이 비어 있으면 <see cref="Find"/>와 같고,
    /// 있으면 해당 이름의 오브젝트에서 Flowchart를 찾습니다.
    /// </summary>
    public static Flowchart FindByGameObjectName(string gameObjectName)
    {
        string name = string.IsNullOrEmpty(gameObjectName) ? GlobalFlowchartName : gameObjectName;
        GameObject go = GameObject.Find(name);
        if (go == null)
        {
            GameLog.LogWarning($"[FlowchartLocator] '{name}' GameObject를 찾을 수 없습니다.");
            return null;
        }

        Flowchart fc = go.GetComponent<Flowchart>();
        if (fc == null)
            GameLog.LogWarning($"[FlowchartLocator] '{name}'에 Flowchart 컴포넌트가 없습니다.");

        return fc;
    }

    /// <summary>
    /// Fungus <see cref="GlobalVariables"/> 저장소의 bool 값.
    /// Variablemanager Flowchart의 변수 목록에 해당 키가 없어도, 씬 Flowchart가 글로벌로 켠 값을 읽을 수 있습니다.
    /// </summary>
    public static bool GetFungusGlobalBoolean(string key)
    {
        if (string.IsNullOrEmpty(key))
            return false;

        if (FungusManager.Instance == null)
            return false;

        GlobalVariables globals = FungusManager.Instance.GlobalVariables;
        if (globals == null)
            return false;

        Variable v = globals.GetVariable(key);
        return v is BooleanVariable b && b.Value;
    }
}
