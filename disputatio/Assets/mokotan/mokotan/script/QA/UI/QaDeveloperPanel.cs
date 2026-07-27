#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Godlotto.QA.Core;
using UnityEngine;

namespace Godlotto.QA.Gateway
{
    /// <summary>
    /// 사람이 조작하는 QA 개발자 패널(Task 10 §Step 3). Unity CLI 도구
    /// (<c>Assets/Editor/QA/QaUnityCliTools.cs</c>)와 정확히 같은 <see cref="QaCommandGateway"/>
    /// (<see cref="QaCommandGatewayHost.GetOrCreate"/>)를 호출하고, 정확히 같은
    /// <c>QaCommandGateway.Build*Command</c> 정적 메서드로 <see cref="QaCommand"/> DTO를
    /// 구성합니다(<see cref="BuildStatusCommandForPanel"/> 등 — 계약 테스트가 CLI 쪽 빌더와
    /// 나란히 검증합니다).
    ///
    /// 이 컴포넌트는 QA 코어를 소유하거나 <c>Dispose</c>하지 않습니다 — 게이트웨이는 프로세스
    /// 전역 <see cref="QaCommandGatewayHost"/>가 소유하므로, 패널이 파괴되거나 <see cref="OnGUI"/>
    /// 렌더링이 예외를 던져도 코어 실행 상태에는 아무 영향이 없습니다(Task 10 제약: "패널
    /// 렌더링 예외가 코어를 소유하거나 dispose해서는 안 된다"). Headless(배치/서버) 실행에서는
    /// <see cref="visible"/>이 항상 false이므로 <see cref="OnGUI"/>가 아무 것도 하지 않고
    /// 즉시 반환합니다 — 이 컴포넌트가 존재하는 것만으로 headless QA 구동을 막지 않습니다.
    /// </summary>
    public sealed class QaDeveloperPanel : MonoBehaviour
    {
        private const int DefaultTimeoutMs = 120000;
        private const string DefaultOwnerId = "qa-developer-panel";
        private static readonly Rect DefaultWindowRect = new Rect(560f, 24f, 480f, 560f);

        [SerializeField] private bool visible;
        [SerializeField] private Rect windowRect = DefaultWindowRect;

        private Vector2 scrollPosition;
        private string scenarioIdInput = string.Empty;
        private string timeoutMsInput = DefaultTimeoutMs.ToString(CultureInfo.InvariantCulture);
        private string ownerIdInput = DefaultOwnerId;
        private string lastOperationMessage = string.Empty;
        private bool isRunRequestInFlight;

        private QaGatewayStatusSnapshot latestStatus;
        private IReadOnlyList<QaGatewayScenarioSummary> latestScenarios = Array.Empty<QaGatewayScenarioSummary>();

        // Godlotto.QA.UI must not reference the default Assembly-CSharp (that would create a
        // circular assembly reference, since Assembly-CSharp already references this custom
        // asmdef). DeveloperModeController therefore pushes its own readiness flags in via these
        // provider delegates (DIP) instead of this panel reaching out to that concrete type.
        private Func<bool> canUseDeveloperModeRuntimeProvider;
        private Func<bool> isDeveloperModeEnabledProvider;

#if UNITY_INCLUDE_TESTS
        /// <summary>
        /// 테스트 전용 결함 주입 훅. 설정되면 <see cref="DrawWindow"/> 시작 시 호출되어 렌더링
        /// 예외 경로("패널 렌더링 예외가 코어를 소유/dispose하지 않는다")를 검증할 수 있습니다.
        /// </summary>
        internal Action DrawWindowFaultInjectorForTests { get; set; }
#endif

        public bool IsVisible
        {
            get { return visible; }
        }

        public void SetVisible(bool value)
        {
            visible = value;
            if (value)
            {
                RefreshStatusOnly();
                RefreshScenarioList();
            }
        }

        public void ToggleVisible()
        {
            SetVisible(!visible);
        }

        /// <summary>
        /// <c>DeveloperModeController</c>(default Assembly-CSharp)가 자신의 정적 readiness 상태를
        /// 이 패널에 주입합니다. 호출하지 않으면 readiness 섹션은 "설정되지 않음"으로 표시됩니다.
        /// </summary>
        public void ConfigureReadinessProviders(
            Func<bool> canUseDeveloperModeRuntimeProvider, Func<bool> isDeveloperModeEnabledProvider)
        {
            this.canUseDeveloperModeRuntimeProvider = canUseDeveloperModeRuntimeProvider;
            this.isDeveloperModeEnabledProvider = isDeveloperModeEnabledProvider;
        }

        // -----------------------------------------------------------------------------------
        //  Shared command builders — must delegate to the exact same QaCommandGateway static
        //  methods that Assets/Editor/QA/QaUnityCliTools.cs delegates to (Task 10 §Step 1).
        // -----------------------------------------------------------------------------------

        public static QaCommand BuildStatusCommandForPanel(string commandId)
        {
            return QaCommandGateway.BuildStatusCommand(commandId);
        }

        public static QaCommand BuildRunCommandForPanel(string commandId, string scenarioId, int timeoutMs)
        {
            TimeSpan timeout = TimeSpan.FromMilliseconds(Math.Max(0, timeoutMs));
            return QaCommandGateway.BuildRunCommand(commandId, scenarioId, timeout);
        }

        public static QaCommand BuildCancelCommandForPanel(string commandId, string scenarioId)
        {
            return QaCommandGateway.BuildCancelCommand(commandId, scenarioId);
        }

        public static QaCommand BuildCaptureCommandForPanel(string commandId, string scenarioId)
        {
            return QaCommandGateway.BuildCaptureCommand(commandId, scenarioId);
        }

        private void OnEnable()
        {
            if (visible)
            {
                RefreshStatusOnly();
                RefreshScenarioList();
            }
        }

        private void OnGUI()
        {
            if (!visible)
            {
                return;
            }

            if (Event.current == null || GUI.skin == null)
            {
                return;
            }

            try
            {
                windowRect = GUILayout.Window(GetInstanceID(), windowRect, DrawWindow, "QA Developer Panel");
            }
            catch (Exception ex)
            {
                // A panel rendering exception must never own or dispose the QA core (Task 10
                // constraint): the shared gateway lives in QaCommandGatewayHost, untouched by
                // this catch. Log once and simply skip rendering for this frame.
                Debug.LogWarning("[QaDeveloperPanel] OnGUI threw " + ex.GetType().Name + "; skipping this frame.");
            }
        }

        private void DrawWindow(int id)
        {
#if UNITY_INCLUDE_TESTS
            DrawWindowFaultInjectorForTests?.Invoke();
#endif

            scrollPosition = GUILayout.BeginScrollView(scrollPosition);

            DrawReadinessSection();
            DrawProfileSection();
            DrawScenarioListSection();
            DrawScenePresetSection();
            DrawStepControlsSection();
            DrawCurrentStateSection();
            DrawEvidenceSection();
            DrawCancelRecoverSection();

            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0f, 0f, windowRect.width, 20f));
        }

        // -----------------------------------------------------------------------------------
        //  Sections
        // -----------------------------------------------------------------------------------

        private void DrawReadinessSection()
        {
            GUILayout.Label("Readiness", GUI.skin.box);
            GUILayout.Label("CanUseDeveloperModeRuntime: " + MarkNullable(SafeInvoke(canUseDeveloperModeRuntimeProvider)));
            GUILayout.Label("DeveloperModeEnabled: " + MarkNullable(SafeInvoke(isDeveloperModeEnabledProvider)));

            if (GUILayout.Button("Refresh Status"))
            {
                RefreshStatusOnly();
            }

            GUILayout.Space(4f);
        }

        private void DrawProfileSection()
        {
            GUILayout.Label("Profile", GUI.skin.box);
            GUILayout.Label("QA profile active: " + Mark(latestStatus != null && latestStatus.IsQaProfileActive));
            GUILayout.Space(4f);
        }

        private void DrawScenarioListSection()
        {
            GUILayout.Label("Scenarios", GUI.skin.box);

            if (GUILayout.Button("Refresh Scenario List"))
            {
                RefreshScenarioList();
            }

            if (latestScenarios.Count == 0)
            {
                GUILayout.Label("(no scenarios found under Resources/QA/Scenarios)");
            }

            foreach (QaGatewayScenarioSummary scenario in latestScenarios)
            {
                GUILayout.BeginHorizontal();

                bool isSelected = string.Equals(scenario.ScenarioId, scenarioIdInput, StringComparison.Ordinal);
                bool toggled = GUILayout.Toggle(isSelected, string.Empty, GUILayout.Width(18f));
                if (toggled && !isSelected && scenario.IsValid)
                {
                    scenarioIdInput = scenario.ScenarioId;
                }

                string label = scenario.IsValid
                    ? scenario.ScenarioId + "  (" + scenario.StepCount + " step(s))"
                    : scenario.ScenarioId + "  [INVALID]";
                GUILayout.Label(label);

                GUILayout.EndHorizontal();

                if (!scenario.IsValid)
                {
                    foreach (string error in scenario.ValidationErrors)
                    {
                        GUILayout.Label("    - " + error);
                    }
                }
            }

            GUILayout.Space(4f);
        }

        private void DrawScenePresetSection()
        {
            GUILayout.Label("Scene / Preset (selected scenario)", GUI.skin.box);

            QaGatewayScenarioSummary selected = FindSelectedScenario();
            if (selected == null)
            {
                GUILayout.Label("(select a scenario above)");
            }
            else
            {
                GUILayout.Label("Scene: " + (string.IsNullOrEmpty(selected.Scene) ? "(none)" : selected.Scene));
                GUILayout.Label("Preset: " + (string.IsNullOrEmpty(selected.Preset) ? "(none)" : selected.Preset));
            }

            GUILayout.Space(4f);
        }

        private void DrawStepControlsSection()
        {
            GUILayout.Label("Step Controls", GUI.skin.box);
            GUILayout.Label(
                "Scenarios run to completion as a single atomic step sequence in this build; " +
                "there is no manual per-step pause/resume yet.");

            GUILayout.BeginHorizontal();
            GUILayout.Label("Scenario id", GUILayout.Width(90f));
            scenarioIdInput = GUILayout.TextField(scenarioIdInput);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Timeout (ms)", GUILayout.Width(90f));
            timeoutMsInput = GUILayout.TextField(timeoutMsInput);
            GUILayout.EndHorizontal();

            GUI.enabled = !isRunRequestInFlight && !string.IsNullOrWhiteSpace(scenarioIdInput);
            if (GUILayout.Button(isRunRequestInFlight ? "Running..." : "Run Scenario"))
            {
                OnRunButtonClicked();
            }

            GUI.enabled = true;

            if (!string.IsNullOrEmpty(lastOperationMessage))
            {
                GUILayout.Label(lastOperationMessage);
            }

            GUILayout.Space(4f);
        }

        private void DrawCurrentStateSection()
        {
            GUILayout.Label("Current State", GUI.skin.box);

            if (latestStatus == null)
            {
                GUILayout.Label("(status not yet loaded)");
            }
            else
            {
                GUILayout.Label("Scenario running: " + Mark(latestStatus.IsScenarioRunning));
                GUILayout.Label("Active scenario id: " +
                    (string.IsNullOrEmpty(latestStatus.ActiveScenarioId) ? "(none)" : latestStatus.ActiveScenarioId));
                GUILayout.Label("Active run id: " +
                    (string.IsNullOrEmpty(latestStatus.ActiveRunId) ? "(none)" : latestStatus.ActiveRunId));
            }

            GUILayout.Space(4f);
        }

        private void DrawEvidenceSection()
        {
            GUILayout.Label("Evidence Path", GUI.skin.box);
            string path = latestStatus != null ? latestStatus.EvidenceRunDirectoryPath : string.Empty;
            GUILayout.Label(string.IsNullOrEmpty(path) ? "(no active run)" : path);
            GUILayout.Space(4f);
        }

        private void DrawCancelRecoverSection()
        {
            GUILayout.Label("Cancel / Recover", GUI.skin.box);

            if (GUILayout.Button("Cancel Active Run"))
            {
                OnCancelButtonClicked();
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Owner id", GUILayout.Width(90f));
            ownerIdInput = GUILayout.TextField(ownerIdInput);
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Recover Interrupted Session"))
            {
                OnRecoverButtonClicked();
            }
        }

        // -----------------------------------------------------------------------------------
        //  Button handlers — kept thin: build the shared QaCommand for audit/logging parity
        //  with the CLI, then delegate the actual operation to the shared gateway.
        // -----------------------------------------------------------------------------------

        private void OnRunButtonClicked()
        {
            if (isRunRequestInFlight || string.IsNullOrWhiteSpace(scenarioIdInput))
            {
                return;
            }

            string commandId = Guid.NewGuid().ToString("N");
            string scenarioId = scenarioIdInput;
            int timeoutMs = ParseTimeoutMs(timeoutMsInput);

            // Built purely for CLI/panel command-parity auditing (Task 10 §Step 1); the actual
            // execution below goes through the gateway's typed RunScenarioAsync overload.
            QaCommand command = BuildRunCommandForPanel(commandId, scenarioId, timeoutMs);

            isRunRequestInFlight = true;
            lastOperationMessage = "Running '" + scenarioId + "' (command " + command.Id + ")...";

            RunScenarioFireAndForget(scenarioId, TimeSpan.FromMilliseconds(timeoutMs));
        }

        private async void RunScenarioFireAndForget(string scenarioId, TimeSpan timeout)
        {
            try
            {
                QaCommandGateway gateway = QaCommandGatewayHost.GetOrCreate();
                QaGatewayRunResult result = await gateway.RunScenarioAsync(scenarioId, timeout, CancellationToken.None)
                    .ConfigureAwait(false);
                lastOperationMessage = DescribeRunResult(result);
            }
            catch (Exception ex)
            {
                // Fail-Safe: a panel-initiated run must never crash the panel or the QA core.
                lastOperationMessage = "Run request threw " + ex.GetType().Name + ".";
            }
            finally
            {
                isRunRequestInFlight = false;

                // Resources.LoadAll (invoked transitively by RunScenarioAsync's scenario lookup)
                // and any other Unity API calls have already happened by this point; only pure
                // C# field writes remain, so this is safe even if the continuation above resumed
                // on a non-main thread (QaScenarioRunner awaits with ConfigureAwait(false)).
                pendingStatusRefresh = true;
            }
        }

        private void OnCancelButtonClicked()
        {
            string commandId = Guid.NewGuid().ToString("N");
            string scenarioId = latestStatus != null ? latestStatus.ActiveScenarioId : string.Empty;
            QaCommand command = BuildCancelCommandForPanel(commandId, scenarioId);

            QaGatewayOperationResult result = QaCommandGatewayHost.GetOrCreate().CancelActiveRun();
            lastOperationMessage = "[" + command.Id + "] " + result.Message;
            RefreshStatusOnly();
        }

        private void OnRecoverButtonClicked()
        {
            string ownerId = string.IsNullOrWhiteSpace(ownerIdInput) ? DefaultOwnerId : ownerIdInput;
            QaGatewayOperationResult result = QaCommandGatewayHost.GetOrCreate().Recover(ownerId);
            lastOperationMessage = result.Message;
            RefreshStatusOnly();
        }

        // -----------------------------------------------------------------------------------
        //  Refresh helpers. RefreshScenarioList transitively touches Resources.LoadAll (a Unity
        //  API), so it must only ever be invoked from a main-thread call site (OnEnable/OnGUI
        //  button handlers) — never from an awaited continuation that might resume off-thread.
        //  RefreshStatusOnly touches only plain C# state and is safe from any thread.
        // -----------------------------------------------------------------------------------

        private bool pendingStatusRefresh;

        private void Update()
        {
            if (!pendingStatusRefresh)
            {
                return;
            }

            pendingStatusRefresh = false;
            RefreshStatusOnly();
        }

        private void RefreshStatusOnly()
        {
            try
            {
                latestStatus = QaCommandGatewayHost.GetOrCreate().GetStatus();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[QaDeveloperPanel] GetStatus threw: " + ex.GetType().Name);
            }
        }

        private void RefreshScenarioList()
        {
            try
            {
                latestScenarios = QaCommandGatewayHost.GetOrCreate().ListScenarios();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[QaDeveloperPanel] ListScenarios threw: " + ex.GetType().Name);
                latestScenarios = Array.Empty<QaGatewayScenarioSummary>();
            }
        }

        private QaGatewayScenarioSummary FindSelectedScenario()
        {
            foreach (QaGatewayScenarioSummary scenario in latestScenarios)
            {
                if (string.Equals(scenario.ScenarioId, scenarioIdInput, StringComparison.Ordinal))
                {
                    return scenario;
                }
            }

            return null;
        }

        private static int ParseTimeoutMs(string rawValue)
        {
            return int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) && parsed > 0
                ? parsed
                : DefaultTimeoutMs;
        }

        private static string DescribeRunResult(QaGatewayRunResult result)
        {
            if (!result.IsSuccess)
            {
                return "Run request failed (" + result.Code + "): " + result.Message;
            }

            return "Run finished: " + result.Outcome.Code + " - " + result.Outcome.Message;
        }

        private static string Mark(bool value)
        {
            return value ? "yes" : "no";
        }

        private static string MarkNullable(bool? value)
        {
            return value.HasValue ? Mark(value.Value) : "(not configured)";
        }

        private static bool? SafeInvoke(Func<bool> provider)
        {
            if (provider == null)
            {
                return null;
            }

            try
            {
                return provider();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[QaDeveloperPanel] Readiness provider threw: " + ex.GetType().Name);
                return null;
            }
        }
    }
}
#endif
