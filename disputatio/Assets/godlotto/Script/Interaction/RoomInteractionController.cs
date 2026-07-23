using System;
using System.Collections.Generic;
using Fungus;
using Godlotto.ModalInput;
using UnityEngine;

namespace Godlotto.Interaction
{
    /// <summary>
    /// 룸·복도 씬 공통: 월드/UI 클릭 진입, Fungus 블록 실행, 블록 종료 outcome 처리.
    /// </summary>
    public class RoomInteractionController : MonoBehaviour
    {
        const float GameplayPlaneZ = 0f;

        [SerializeField] Flowchart flowchart;
        [SerializeField] BackNavigator backNavigator;
        [SerializeField] bool enableDebugLogging;
        [SerializeField] WorldClickBinding[] worldClicks = Array.Empty<WorldClickBinding>();
        [SerializeField] InteractionRoute[] routes = Array.Empty<InteractionRoute>();
        [SerializeField] BlockOutcome[] blockOutcomes = Array.Empty<BlockOutcome>();
        [SerializeField] PanelCloseBinding[] panelCloses = Array.Empty<PanelCloseBinding>();

        readonly Dictionary<string, string> blockNameByInteractionId = new Dictionary<string, string>();
        readonly Dictionary<string, BlockOutcome> outcomeByBlockName = new Dictionary<string, BlockOutcome>();
        readonly Dictionary<string, GameObject> panelByCloseId = new Dictionary<string, GameObject>();

        protected Flowchart Flowchart => flowchart;
        protected virtual string LogPrefix => "[RoomInteraction]";

        protected virtual void Awake()
        {
            BuildLookupCaches();

            foreach (WorldClickBinding binding in worldClicks)
            {
                if (binding.clickable != null)
                    binding.clickable.enabled = false;
            }
        }

        void OnEnable()
        {
            BlockSignals.OnBlockEnd -= OnBlockEnd;
            BlockSignals.OnBlockEnd += OnBlockEnd;
        }

        void OnDisable()
        {
            BlockSignals.OnBlockEnd -= OnBlockEnd;
        }

        protected virtual bool ShouldProcessWorldClickAt(Vector2 screenPosition) => true;

        void Update()
        {
            if (!TryGetPrimaryPressAndScreenPoint(out Vector2 screenPosition))
                return;

            if (!ShouldProcessWorldClickAt(screenPosition))
                return;

            for (int i = 0; i < worldClicks.Length; i++)
            {
                WorldClickBinding binding = worldClicks[i];
                if (binding.collider == null || !binding.collider.enabled)
                    continue;

                if (!ShouldProcessWorldClickBinding(binding, screenPosition))
                    continue;

                if (!binding.collider.OverlapPoint(ScreenToWorldOnGameplayPlane(screenPosition)))
                    continue;

                OnInteraction(binding.interactionId, binding.collider.gameObject);
                return;
            }
        }

        protected virtual bool ShouldProcessWorldClickBinding(WorldClickBinding binding, Vector2 screenPosition) => true;

        protected void SetWorldClickColliderEnabled(string interactionId, bool enabled)
        {
            if (string.IsNullOrWhiteSpace(interactionId))
                return;

            for (int i = 0; i < worldClicks.Length; i++)
            {
                WorldClickBinding binding = worldClicks[i];
                if (binding.interactionId != interactionId || binding.collider == null)
                    continue;

                binding.collider.enabled = enabled;
                return;
            }
        }

        /// <summary>UI·드롭존·백스페이스 등에서 호출하는 공통 진입점.</summary>
        public virtual void OnInteraction(string interactionId)
        {
            OnInteraction(interactionId, null);
        }

        /// <summary>UI·드롭존·백스페이스 등에서 호출하는 공통 진입점.</summary>
        public virtual void OnInteraction(string interactionId, GameObject inputSource)
        {
            if (string.IsNullOrWhiteSpace(interactionId))
                return;

            if (!blockNameByInteractionId.TryGetValue(interactionId, out string blockName))
            {
                LogIgnored($"Unknown interaction id '{interactionId}'.");
                return;
            }

            if (ShouldBlockForModalInput(inputSource))
            {
                LogIgnored($"Modal input gate blocked '{interactionId}' -> '{blockName}'.");
                return;
            }

            if (ShouldUseSceneInteractionGate(interactionId, blockName))
            {
                if (!SceneInteractionController.TryInteract(interactionId))
                    return;
            }

            if (!ShouldExecuteInteraction(interactionId, blockName))
            {
                LogIgnored($"Gate blocked '{interactionId}' -> '{blockName}'.");
                OnInteractionGateBlocked(interactionId, blockName);
                return;
            }

            PrepareInteractionExecution(interactionId, blockName);

            if (!FungusDialogueBridge.ExecuteBlockSafely(flowchart, blockName))
                LogIgnored($"Failed to execute block '{blockName}' for '{interactionId}'.");
        }

        protected virtual bool ShouldBlockForModalInput(GameObject inputSource)
        {
            if (!ModalInputGate.IsBlockingWorldInput && !ModalInputGate.IsBlockingHudInput)
                return false;

            if (inputSource != null)
                return !ModalInputGate.IsAllowed(inputSource);

            return false;
        }

        /// <summary>SceneInteractionController(Fungus 대사/메뉴 차단) 적용 여부. UI 드롭 등은 씬별로 우회.</summary>
        protected virtual bool ShouldUseSceneInteractionGate(string interactionId, string blockName)
        {
            return true;
        }

        /// <summary>씬별 퍼즐 상태에 따라 Fungus 블록 실행 여부를 결정합니다.</summary>
        protected virtual bool ShouldExecuteInteraction(string interactionId, string blockName) => true;

        /// <summary>ExecuteBlock 직전에 Fungus 변수 미러 등 씬별 준비를 수행합니다.</summary>
        protected virtual void PrepareInteractionExecution(string interactionId, string blockName)
        {
        }

        /// <summary>
        /// <see cref="ShouldExecuteInteraction"/>이 클릭을 막았을 때 호출되는 폴백 훅.
        /// Fungus 블록은 재실행하지 않지만, 씬별로 대체 동작(예: 일반 채팅 패널 열기)을 붙일 수 있습니다.
        /// </summary>
        protected virtual void OnInteractionGateBlocked(string interactionId, string blockName)
        {
        }

        /// <summary>패널 백스페이스 등에서 호출. 패널을 닫고 isClicked를 리셋합니다.</summary>
        public void OnClosePanel(string panelCloseId, GameObject panelOverride = null)
        {
            if (string.IsNullOrWhiteSpace(panelCloseId))
                return;

            GameObject panel = panelOverride;
            if (panel == null && !panelByCloseId.TryGetValue(panelCloseId, out panel))
            {
                LogIgnored($"Unknown panel close id '{panelCloseId}'.");
                return;
            }

            if (panel != null)
                panel.SetActive(false);

            HandlePanelClosed(panelCloseId, panel);
            ResetIsClicked();
            DeferredClickCleanup.Run(flowchart, resetWindowClicked: false);
        }

        protected virtual void HandlePanelClosed(string panelCloseId, GameObject closedPanel)
        {
        }

        void OnBlockEnd(Block block)
        {
            if (block == null || flowchart == null || block.GetFlowchart() != flowchart)
                return;

            if (outcomeByBlockName.TryGetValue(block.BlockName, out BlockOutcome outcome))
                ApplyBlockOutcome(block, outcome);

            OnInteractionBlockCompleted(block.BlockName);
        }

        /// <summary>블록 종료 후 씬별 퍼즐 상태를 갱신합니다.</summary>
        protected virtual void OnInteractionBlockCompleted(string blockName)
        {
        }

        protected virtual void ApplyBlockOutcome(Block block, BlockOutcome outcome)
        {
            if (outcome.openPanel != null)
            {
                EnsureModalInputScope(outcome.openPanel);
                outcome.openPanel.SetActive(true);
            }

            if (outcome.resetIsClicked)
                ResetIsClicked();

            if (outcome.goBack)
            {
                ResetIsClicked();
                DeferredClickCleanup.Run(flowchart, resetWindowClicked: false);
                RequestGoBack();
                return;
            }

            if (outcome.loadScene && !string.IsNullOrWhiteSpace(outcome.sceneName))
            {
                ResetIsClicked();
                if (!RequestSceneTransition(outcome.sceneName))
                    DeferredClickCleanup.Run(flowchart, resetWindowClicked: false);
            }
        }

        static void EnsureModalInputScope(GameObject panel)
        {
            if (panel == null || panel.GetComponent<ModalInputScope>() != null)
                return;

            panel.AddComponent<ModalInputScope>();
        }

        void ResetIsClicked()
        {
            ClickInteractionCleanup.ResetAfterUiBoundary(flowchart, resetWindowClicked: false);
        }

        void RequestGoBack()
        {
            if (GoBackHandlerForTests != null)
            {
                GoBackHandlerForTests();
                return;
            }

            if (backNavigator != null)
            {
                backNavigator.GoBack();
                return;
            }

            string sceneName = SceneManagerHelper.GetActiveSceneName();
            if (BackNavigator.TryResolveFixedReturnScene(sceneName, out string fixedReturn)
                && !string.IsNullOrWhiteSpace(fixedReturn))
            {
                SceneTransitionService.LoadSceneSafely(fixedReturn);
                return;
            }

            GameLog.LogWarning($"{LogPrefix} GoBack requested but no BackNavigator or fixed route was found.");
        }

        bool RequestSceneTransition(string sceneName)
        {
            if (SceneLoadHandlerForTests != null)
            {
                SceneLoadHandlerForTests(sceneName);
                return true;
            }

            return SceneTransitionService.LoadSceneSafely(sceneName);
        }

        void BuildLookupCaches()
        {
            blockNameByInteractionId.Clear();
            outcomeByBlockName.Clear();
            panelByCloseId.Clear();

            foreach (InteractionRoute route in routes)
            {
                if (route == null || string.IsNullOrWhiteSpace(route.interactionId)
                    || string.IsNullOrWhiteSpace(route.fungusBlockName))
                    continue;

                blockNameByInteractionId[route.interactionId] = route.fungusBlockName;
            }

            foreach (BlockOutcome outcome in blockOutcomes)
            {
                if (outcome == null || string.IsNullOrWhiteSpace(outcome.blockName))
                    continue;

                outcomeByBlockName[outcome.blockName] = outcome;
            }

            foreach (PanelCloseBinding binding in panelCloses)
            {
                if (binding == null || string.IsNullOrWhiteSpace(binding.panelCloseId) || binding.panel == null)
                    continue;

                panelByCloseId[binding.panelCloseId] = binding.panel;
            }
        }

        void LogIgnored(string message)
        {
            if (enableDebugLogging)
                GameLog.Log($"{LogPrefix} {message}");
        }

        internal static Func<string, bool> SceneLoadHandlerForTests;
        internal static Action GoBackHandlerForTests;

        internal static void ResetStateForTests()
        {
            SceneLoadHandlerForTests = null;
            GoBackHandlerForTests = null;
            InteractionInputGate.ResetForTests();
            SceneInteractionController.ResetForTests();
            FungusDialogueBridge.ResetForTests();
            SceneTransitionService.ResetForTests();
        }

        internal void InvokeBlockEndForTests(Block block) => OnBlockEnd(block);

        static bool TryGetPrimaryPressAndScreenPoint(out Vector2 screenPosition)
        {
            screenPosition = default;

#if ENABLE_INPUT_SYSTEM
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                screenPosition = mouse.position.ReadValue();
                return true;
            }

            var touch = UnityEngine.InputSystem.Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.wasPressedThisFrame)
            {
                screenPosition = touch.primaryTouch.position.ReadValue();
                return true;
            }
#endif
            if (Input.GetMouseButtonDown(0))
            {
                screenPosition = Input.mousePosition;
                return true;
            }

            if (Input.touchCount > 0)
            {
                Touch t = Input.GetTouch(0);
                if (t.phase == TouchPhase.Began)
                {
                    screenPosition = t.position;
                    return true;
                }
            }

            return false;
        }

        static Vector2 ScreenToWorldOnGameplayPlane(Vector2 screenPosition)
        {
            Camera cam = Camera.main;
            if (cam == null)
                return Vector2.zero;

            Ray ray = cam.ScreenPointToRay(screenPosition);
            if (Mathf.Abs(ray.direction.z) > 1e-5f)
            {
                float t = (GameplayPlaneZ - ray.origin.z) / ray.direction.z;
                Vector3 p = ray.GetPoint(t);
                return new Vector2(p.x, p.y);
            }

            Vector3 fallback = cam.ScreenToWorldPoint(new Vector3(
                screenPosition.x,
                screenPosition.y,
                Mathf.Abs(cam.transform.position.z)));
            return fallback;
        }
    }

    [Serializable]
    public class WorldClickBinding
    {
        public string interactionId;
        public Collider2D collider;
        public Clickable2D clickable;
    }

    [Serializable]
    public class InteractionRoute
    {
        public string interactionId;
        public string fungusBlockName;
    }

    [Serializable]
    public class BlockOutcome
    {
        public string blockName;
        public GameObject openPanel;
        public bool resetIsClicked;
        public bool loadScene;
        public string sceneName;
        public bool goBack;
    }

    [Serializable]
    public class PanelCloseBinding
    {
        public string panelCloseId;
        public GameObject panel;
    }

    static class SceneManagerHelper
    {
        public static string GetActiveSceneName() => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
    }
}
