using System;
using System.Collections;
using Fungus;
using UnityEngine;

/// <summary>
/// Fungus bool FaucetClicked가 true가 되면 delay 후 키 표시를 실행합니다.
/// Sink_Pannel의 Faucet 버튼 GO와 분리된 항상 활성 호스트(Flowchart 등)에 둡니다.
/// faucetClosed 비활성화 시 Update가 멈추는 것을 방지합니다.
/// </summary>
public class FaucetKeyReleaseController : MonoBehaviour
{
    [SerializeField] Flowchart targetFlowchart;
    [SerializeField] string faucetBoolName = "FaucetClicked";
    [SerializeField] string bottleDraggedBoolName = "BottleDragged";
    [SerializeField] string keySpawnBlockName = "addKey";
    [SerializeField] float delaySeconds = 1f;
    [SerializeField] GameObject keyObject;
    [SerializeField] string keyObjectName = "MaidRoomKey";
    [SerializeField] Animator keyAnimator;
    [SerializeField] string keyAnimatorTriggerName = "MoveTrigger";
    [SerializeField] bool enableDebugLogging;

    bool hasTriggered;

    internal static Action<string> ExecuteBlockHandlerForTests;

    void Update()
    {
        TryTriggerKeySpawn();
    }

    void OnEnable()
    {
        // 씬 재진입·패널 토글 후에도 이미 true인 FaucetClicked를 놓치지 않습니다.
        TryTriggerKeySpawn();
    }

    internal void ResetForTests()
    {
        hasTriggered = false;
        StopAllCoroutines();
    }

    internal bool HasTriggeredForTests => hasTriggered;

    internal void TryTriggerKeySpawnForTests() => TryTriggerKeySpawn();

    /// <summary>
    /// QA/autorun: spawn immediately without the Play Mode delay coroutine
    /// (sync CLI exec cannot wait for Update + WaitForSeconds).
    /// </summary>
    public void TriggerImmediateKeySpawnForQa()
    {
        float previousDelay = delaySeconds;
        delaySeconds = 0f;
        hasTriggered = false;
        StopAllCoroutines();
        TryTriggerKeySpawn();
        delaySeconds = previousDelay;
    }

    void TryTriggerKeySpawn()
    {
        if (hasTriggered)
            return;

        Flowchart flowchart = ResolveFlowchart();
        if (flowchart == null)
            return;

        if (!flowchart.GetBooleanVariable(faucetBoolName))
            return;

        if (!string.IsNullOrEmpty(bottleDraggedBoolName)
            && !flowchart.GetBooleanVariable(bottleDraggedBoolName))
            return;

        hasTriggered = true;

        if (enableDebugLogging)
        {
            GameLog.Log(
                $"[FaucetKeyReleaseController] {faucetBoolName}=true detected; "
                + $"{bottleDraggedBoolName}=true detected; "
                + $"spawn key in {delaySeconds:0.##}s");
        }

        if (delaySeconds <= 0f)
            SpawnKey(flowchart);
        else
            StartCoroutine(SpawnKeyAfterDelay(flowchart));
    }

    IEnumerator SpawnKeyAfterDelay(Flowchart flowchart)
    {
        yield return new WaitForSeconds(delaySeconds);
        SpawnKey(flowchart);
    }

    void SpawnKey(Flowchart flowchart)
    {
        if (TryActivateDirectKeyTarget())
            return;

        ExecuteKeySpawnBlock(flowchart);
    }

    bool TryActivateDirectKeyTarget()
    {
        GameObject targetKeyObject = ResolveKeyObject();
        if (targetKeyObject == null)
            return false;

        targetKeyObject.SetActive(true);
        EnsureActiveInHierarchy(targetKeyObject);

        Animator targetAnimator = ResolveKeyAnimator(targetKeyObject);
        if (targetAnimator != null && !string.IsNullOrEmpty(keyAnimatorTriggerName))
            targetAnimator.SetTrigger(keyAnimatorTriggerName);

        if (enableDebugLogging)
            GameLog.Log($"[FaucetKeyReleaseController] Activated direct key target '{targetKeyObject.name}'.");

        return true;
    }

    GameObject ResolveKeyObject()
    {
        if (keyObject != null)
            return keyObject;

        if (string.IsNullOrWhiteSpace(keyObjectName))
            return null;

        GameObject activeObject = GameObject.Find(keyObjectName);
        if (activeObject != null)
        {
            keyObject = activeObject;
            return keyObject;
        }

        foreach (GameObject candidate in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (candidate == null || candidate.name != keyObjectName)
                continue;

            if (!candidate.scene.IsValid())
                continue;

            keyObject = candidate;
            return keyObject;
        }

        return null;
    }

    static void EnsureActiveInHierarchy(GameObject target)
    {
        if (target == null)
            return;

        Transform current = target.transform;
        while (current != null)
        {
            if (!current.gameObject.activeSelf)
                current.gameObject.SetActive(true);
            current = current.parent;
        }
    }

    Animator ResolveKeyAnimator(GameObject targetKeyObject)
    {
        if (keyAnimator != null)
            return keyAnimator;

        if (targetKeyObject == null)
            return null;

        keyAnimator = targetKeyObject.GetComponent<Animator>();
        return keyAnimator;
    }

    void ExecuteKeySpawnBlock(Flowchart flowchart)
    {
        if (string.IsNullOrEmpty(keySpawnBlockName))
            return;

        LogPreSpawnPickupState(flowchart);

        if (ExecuteBlockHandlerForTests != null)
        {
            ExecuteBlockHandlerForTests(keySpawnBlockName);
            return;
        }

        flowchart.ExecuteBlock(keySpawnBlockName);

        if (enableDebugLogging)
            GameLog.Log($"[FaucetKeyReleaseController] ExecuteBlock('{keySpawnBlockName}') completed");
    }

    Flowchart ResolveFlowchart()
    {
        if (targetFlowchart != null)
            return targetFlowchart;

        targetFlowchart = FindFirstObjectByType<Flowchart>();
        return targetFlowchart;
    }

    void LogPreSpawnPickupState(Flowchart flowchart)
    {
        const int maidRoomKeyItemId = 8;
        bool haveMaidKey = flowchart.GetBooleanVariable(FungusVariableKeys.HaveMaidKey);
        bool itemAcquired = ItemAcquisitionTracker.IsAcquired(flowchart, maidRoomKeyItemId);

        if (haveMaidKey || itemAcquired)
        {
            GameLog.LogWarning(
                $"[FaucetKeyReleaseController] addKey will SetActive MaidRoomKey but ItemPickup.Start may suppress: "
                + $"HaveMaidKey={haveMaidKey}, MaidRoom_Key acquired={itemAcquired}, "
                + $"{ItemAcquisitionTracker.FungusVariableKey}={flowchart.GetIntegerVariable(ItemAcquisitionTracker.FungusVariableKey)}");
            return;
        }

        if (enableDebugLogging)
        {
            GameLog.Log(
                "[FaucetKeyReleaseController] addKey pre-spawn: HaveMaidKey=false, MaidRoom_Key not acquired.");
        }
    }
}
