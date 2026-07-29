#if UNITY_EDITOR
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Godlotto.QA.Core;
using Godlotto.QA.Evidence;
using Godlotto.QA.Gateway;
using Godlotto.QA.Profile;
using Godlotto.QA.Scenes;
using Newtonsoft.Json.Linq;
using UnityCliConnector;
using UnityEditor;
using UnityEngine;

namespace Godlotto.QA.EditorCli
{
    /// <summary>
    /// Editor 로드 시점에 <see cref="QaCommandGatewayHost"/>에 Editor 전용 evidence 저장소
    /// (<see cref="EditorQaEvidenceRecorder"/>, <c>docs/qa/runs</c>)를 사용하는 팩토리를 설치합니다
    /// (Task 10). 이렇게 하면 아래의 <c>qa_*</c> CLI 도구와 런타임 <c>QaDeveloperPanel</c>이
    /// 같은 Editor 프로세스(및 Editor Play Mode) 안에서 정확히 같은 <see cref="QaCommandGateway"/>
    /// 인스턴스를 공유합니다 — 순수 development player 빌드에서는 이 설치가 일어나지 않으므로
    /// <see cref="QaCommandGatewayHost"/>가 자체 기본값으로 대체합니다.
    /// </summary>
    [InitializeOnLoad]
    internal static class QaEditorCommandGatewayInstaller
    {
        // Deliberately smaller than QaCapture's qa_capture default (1920x1080): this provider can
        // fire once per evidence.capture step plus once more in QaScenarioRunner's pre-Finalize
        // safety net, so a smaller render target keeps mid-run captures cheap while still being a
        // legible evidence screenshot.
        private const int MidRunCaptureWidth = 1280;
        private const int MidRunCaptureHeight = 720;
        private const string MidRunCaptureView = "game";

        static QaEditorCommandGatewayInstaller()
        {
            QaCommandGatewayHost.InstallFactory(CreateEditorGateway);
        }

        private static QaCommandGateway CreateEditorGateway()
        {
            var recorder = new EditorQaEvidenceRecorder();

            // QaProfileService (real PlayerPrefs isolation) lives in Assembly-CSharp (it depends
            // on PlayDataPrefsCleaner there), so only a predefined assembly like this one
            // (Assembly-CSharp-Editor) can construct it — Godlotto.QA.UI cannot, without a
            // circular assembly reference. See QaCommandGateway.CreateFallbackProfileService for
            // what happens if this is ever omitted.
            var profileService = new QaProfileService(QaFileProfileMarkerStore.CreateDefault());

            // Task 12: the initial scene adapters (MainMenu/Kitchen/Hall/MaidRoom/TutorRoom) live
            // in Assembly-CSharp for the same circular-reference reason as QaProfileService above
            // (see QaSceneAdapterRegistration's remarks), so only an assembly that can see both
            // Assembly-CSharp and Godlotto.QA.Scenes -- like this Editor one -- can wire them in.
            QaSceneRegistry sceneRegistry = Godlotto.QA.SceneAdapters.QaSceneAdapterRegistration.BuildRegistry();

            return new QaCommandGateway(
                recorder,
                () => recorder.RunDirectoryPath,
                profileService: profileService,
                sceneRegistry: sceneRegistry,
                captureScreenshotPng: CaptureMidRunScreenshotPng);
        }

        /// <summary>
        /// Root-cause fix (manifest PASS): <c>qa_run</c> awaits <see cref="QaCommandGateway.RunScenarioAsync"/>
        /// to completion before the Unity CLI tool returns, so a separate follow-up <c>qa_capture</c>
        /// call always arrives after the run (and its evidence recorder) has already closed. Wiring
        /// this provider into <see cref="QaScenarioRunner"/> lets <c>evidence.capture</c> steps (and
        /// the runner's pre-Finalize safety net) attach a real screenshot <em>during</em> the run.
        ///
        /// This reuses <see cref="QaCapture"/>'s render-to-texture procedure (see that type's
        /// remarks) so there is exactly one PNG-capture implementation to keep correct. Every
        /// caller in this codebase -- CLI dispatch for qa_run/qa_capture, and this installer -- runs
        /// on the Editor main thread without ever truly parking on a background thread (see
        /// QaScenarioRunner/QaDriverCore: every awaited operation on this path completes
        /// synchronously), so no extra thread marshaling is required for Camera.Render/ReadPixels.
        /// </summary>
        private static byte[] CaptureMidRunScreenshotPng()
        {
            return QaCapture.CapturePngBytes(MidRunCaptureView, MidRunCaptureWidth, MidRunCaptureHeight);
        }
    }

    /// <summary>모든 <c>qa_*</c> CLI 도구가 공유하는 아주 작은 파싱 헬퍼(중복 제거용).</summary>
    internal static class QaCliToolSupport
    {
        internal static string ResolveCommandId(ToolParams p)
        {
            string commandId = p.Get("command_id");
            return string.IsNullOrWhiteSpace(commandId) ? Guid.NewGuid().ToString("N") : commandId;
        }
    }

    /// <summary>
    /// <c>qa_status</c>: 공유 QA 명령 게이트웨이의 현재 상태를 읽기 전용으로 보고합니다.
    /// 리스/세션 없이도 항상 허용됩니다(mutation이 아님).
    /// </summary>
    [UnityCliTool(Name = "qa_status", Group = "qa",
        Description = "Report the shared QA command gateway's status: QA profile active, scenario running, active scenario/run id, evidence path, registered scenes.")]
    public static class QaStatus
    {
        public class Parameters
        {
            [ToolParameter("Correlation id to stamp on the underlying QaCommand DTO. Generated if omitted.")]
            public string CommandId { get; set; }
        }

        public static object HandleCommand(JObject @params)
        {
            if (@params == null)
            {
                @params = new JObject();
            }

            QaCommand command = BuildStatusCommandForCli(@params);
            QaGatewayStatusSnapshot status = QaCommandGatewayHost.GetOrCreate().GetStatus();

            return new SuccessResponse("QA gateway status.", new
            {
                commandId = command.Id,
                isQaProfileActive = status.IsQaProfileActive,
                isScenarioRunning = status.IsScenarioRunning,
                activeScenarioId = status.ActiveScenarioId,
                activeRunId = status.ActiveRunId,
                evidenceRunDirectoryPath = status.EvidenceRunDirectoryPath,
                registeredSceneNames = status.RegisteredSceneNames
            });
        }

        /// <summary>
        /// CLI JSON 인자 파싱만 담당하는 순수 함수(부수효과 없음). <c>QaDeveloperPanel</c>의
        /// <c>BuildStatusCommandForPanel</c>과 동일한 <see cref="QaCommandGateway.BuildStatusCommand"/>로
        /// 위임하므로, 같은 <c>command_id</c>가 주어지면 두 어댑터는 항상 같은 <see cref="QaCommand"/>를
        /// 만듭니다(Task 10 §Step 1 계약 테스트 대상).
        /// </summary>
        public static QaCommand BuildStatusCommandForCli(JObject @params)
        {
            if (@params == null)
            {
                @params = new JObject();
            }

            var p = new ToolParams(@params);
            string commandId = QaCliToolSupport.ResolveCommandId(p);
            return QaCommandGateway.BuildStatusCommand(commandId);
        }
    }

    /// <summary>
    /// <c>qa_list</c>: <c>Resources/QA/Scenarios</c> 아래에서 발견된 시나리오와 각각의 검증
    /// 상태를 나열합니다. 순수 카탈로그 조회이므로 <see cref="QaCommand"/> DTO로 감싸지
    /// 않습니다(디자인 문서상 "scenario.list"에 대응하는 <see cref="QaCommandType"/> 값이 없음).
    /// </summary>
    [UnityCliTool(Name = "qa_list", Group = "qa",
        Description = "List QA scenarios discovered under Resources/QA/Scenarios, including validation status for each.")]
    public static class QaList
    {
        public class Parameters
        {
        }

        public static object HandleCommand(JObject @params)
        {
            var scenarios = QaCommandGatewayHost.GetOrCreate().ListScenarios();
            var payload = scenarios.Select(scenario => new
            {
                scenarioId = scenario.ScenarioId,
                scene = scenario.Scene,
                preset = scenario.Preset,
                stepCount = scenario.StepCount,
                isValid = scenario.IsValid,
                validationErrors = scenario.ValidationErrors
            }).ToList();

            return new SuccessResponse(payload.Count + " scenario(s) found.", payload);
        }
    }

    /// <summary>
    /// <c>qa_run</c>: 시나리오를 검증·실행합니다. 실제 mutation은 전적으로
    /// <see cref="QaCommandGateway.RunScenarioAsync"/> → <c>QaScenarioRunner</c>가 소유하는 리스
    /// 획득을 통과해야만 일어나므로, 유효한 리스 없이는 이 도구도 아무 것도 바꾸지 못합니다.
    /// </summary>
    [UnityCliTool(Name = "qa_run", Group = "qa",
        Description = "Run a validated QA scenario by id through the shared QA command gateway. Requires scenario_id; refuses to start a second run while one is already active.")]
    public static class QaRun
    {
        private const int DefaultTimeoutMs = 120000;

        public class Parameters
        {
            [ToolParameter("Correlation id to stamp on the underlying QaCommand DTO. Generated if omitted.")]
            public string CommandId { get; set; }

            [ToolParameter("Scenario id to run (must match a validated scenario under Resources/QA/Scenarios).", Required = true)]
            public string ScenarioId { get; set; }

            [ToolParameter("Overall run timeout in milliseconds. 0 disables the overall timeout (only per-step timeouts apply). Default 120000.")]
            public int TimeoutMs { get; set; }
        }

        public static async Task<object> HandleCommand(JObject @params)
        {
            if (@params == null)
            {
                return new ErrorResponse("Parameters cannot be null.");
            }

            var p = new ToolParams(@params);
            Result<string> scenarioIdResult = p.GetRequired("scenario_id");
            if (!scenarioIdResult.IsSuccess)
            {
                return new ErrorResponse(scenarioIdResult.ErrorMessage);
            }

            string scenarioId = scenarioIdResult.Value;
            int timeoutMs = p.GetInt("timeout_ms", DefaultTimeoutMs) ?? DefaultTimeoutMs;
            QaCommand command = BuildRunCommandForCli(@params);
            TimeSpan overallTimeout = TimeSpan.FromMilliseconds(Math.Max(0, timeoutMs));

            QaGatewayRunResult result = await QaCommandGatewayHost.GetOrCreate()
                .RunScenarioAsync(scenarioId, overallTimeout, CancellationToken.None)
                .ConfigureAwait(false);

            object payload = new
            {
                commandId = command.Id,
                scenarioId,
                operationCode = result.Code.ToString(),
                outcomeCode = result.Outcome != null ? result.Outcome.Code.ToString() : null,
                outcomeMessage = result.Outcome != null ? result.Outcome.Message : null
            };

            return result.IsSuccess
                ? new SuccessResponse(result.Message, payload)
                : new ErrorResponse(result.Message, payload);
        }

        /// <summary>
        /// CLI JSON 인자 파싱만 담당하는 순수 함수(부수효과 없음). <c>QaDeveloperPanel</c>의
        /// <c>BuildRunCommandForPanel</c>과 동일한 <see cref="QaCommandGateway.BuildRunCommand"/>로
        /// 위임하므로, 같은 scenario_id/timeout_ms/command_id가 주어지면 두 어댑터는 항상 같은
        /// <see cref="QaCommand"/>를 만듭니다(Task 10 §Step 1 계약 테스트 대상).
        /// </summary>
        public static QaCommand BuildRunCommandForCli(JObject @params)
        {
            if (@params == null)
            {
                @params = new JObject();
            }

            var p = new ToolParams(@params);
            string commandId = QaCliToolSupport.ResolveCommandId(p);
            string scenarioId = p.Get("scenario_id") ?? string.Empty;
            int timeoutMs = p.GetInt("timeout_ms", DefaultTimeoutMs) ?? DefaultTimeoutMs;
            TimeSpan timeout = TimeSpan.FromMilliseconds(Math.Max(0, timeoutMs));
            return QaCommandGateway.BuildRunCommand(commandId, scenarioId, timeout);
        }
    }

    /// <summary><c>qa_cancel</c>: 현재 활성 QA 시나리오 run(있다면)에 취소를 요청합니다.</summary>
    [UnityCliTool(Name = "qa_cancel", Group = "qa",
        Description = "Cancel the QA scenario run currently active in the shared QA command gateway, if any.")]
    public static class QaCancel
    {
        public class Parameters
        {
            [ToolParameter("Correlation id to stamp on the underlying QaCommand DTO. Generated if omitted.")]
            public string CommandId { get; set; }

            [ToolParameter("Scenario id to echo on the QaCommand DTO (informational only; the gateway always cancels whichever run is active).")]
            public string ScenarioId { get; set; }
        }

        public static object HandleCommand(JObject @params)
        {
            if (@params == null)
            {
                @params = new JObject();
            }

            QaCommand command = BuildCancelCommandForCli(@params);
            QaGatewayOperationResult result = QaCommandGatewayHost.GetOrCreate().CancelActiveRun();
            object payload = new { commandId = command.Id, operationCode = result.Code.ToString() };

            return result.IsSuccess
                ? new SuccessResponse(result.Message, payload)
                : new ErrorResponse(result.Message, payload);
        }

        /// <summary>
        /// CLI JSON 인자 파싱만 담당하는 순수 함수(부수효과 없음). <c>QaDeveloperPanel</c>의
        /// <c>BuildCancelCommandForPanel</c>과 동일한 <see cref="QaCommandGateway.BuildCancelCommand"/>로
        /// 위임합니다(Task 10 §Step 1 계약 테스트 대상).
        /// </summary>
        public static QaCommand BuildCancelCommandForCli(JObject @params)
        {
            if (@params == null)
            {
                @params = new JObject();
            }

            var p = new ToolParams(@params);
            string commandId = QaCliToolSupport.ResolveCommandId(p);
            string scenarioId = p.Get("scenario_id") ?? string.Empty;
            return QaCommandGateway.BuildCancelCommand(commandId, scenarioId);
        }
    }

    /// <summary>
    /// <c>qa_capture</c>: Scene/Game 뷰를 캡처하여 현재 활성 run의 evidence에 첨부합니다.
    /// 활성 run이 없으면(<c>qa_run</c>이 먼저 필요) 거부됩니다 — evidence는 항상 특정 run에
    /// 귀속되어야 하기 때문입니다.
    /// </summary>
    [UnityCliTool(Name = "qa_capture", Group = "qa",
        Description = "Capture a screenshot (scene or game view) and attach it as evidence to the QA command gateway's active scenario run. Requires an active run (started via qa_run).")]
    public static class QaCapture
    {
        private const int DefaultWidth = 1920;
        private const int DefaultHeight = 1080;

        public class Parameters
        {
            [ToolParameter("Correlation id to stamp on the underlying QaCommand DTO. Generated if omitted.")]
            public string CommandId { get; set; }

            [ToolParameter("Scenario id to echo on the QaCommand DTO (informational only).")]
            public string ScenarioId { get; set; }

            [ToolParameter("View to capture: scene (default) or game.")]
            public string View { get; set; }

            [ToolParameter("Override width (default 1920).")]
            public int Width { get; set; }

            [ToolParameter("Override height (default 1080).")]
            public int Height { get; set; }
        }

        public static object HandleCommand(JObject @params)
        {
            if (@params == null)
            {
                @params = new JObject();
            }

            var p = new ToolParams(@params);
            QaCommand command = BuildCaptureCommandForCli(@params);

            string view = (p.Get("view", "scene") ?? "scene").ToLowerInvariant();
            int width = p.GetInt("width", DefaultWidth) ?? DefaultWidth;
            int height = p.GetInt("height", DefaultHeight) ?? DefaultHeight;

            byte[] pngBytes;
            try
            {
                pngBytes = CapturePngBytes(view, width, height);
            }
            catch (Exception ex)
            {
                return new ErrorResponse("Screenshot capture failed: " + ex.Message);
            }

            if (pngBytes == null)
            {
                return new ErrorResponse("Unknown view '" + view + "'. Valid values: scene, game.");
            }

            QaGatewayOperationResult result =
                QaCommandGatewayHost.GetOrCreate().CaptureEvidence(command.Id, pngBytes);
            object payload = new
            {
                commandId = command.Id,
                operationCode = result.Code.ToString(),
                byteCount = pngBytes.Length
            };

            return result.IsSuccess
                ? new SuccessResponse(result.Message, payload)
                : new ErrorResponse(result.Message, payload);
        }

        /// <summary>
        /// CLI JSON 인자 파싱만 담당하는 순수 함수(부수효과 없음). <c>QaDeveloperPanel</c>의
        /// <c>BuildCaptureCommandForPanel</c>과 동일한 <see cref="QaCommandGateway.BuildCaptureCommand"/>로
        /// 위임합니다(Task 10 §Step 1 계약 테스트 대상).
        /// </summary>
        public static QaCommand BuildCaptureCommandForCli(JObject @params)
        {
            if (@params == null)
            {
                @params = new JObject();
            }

            var p = new ToolParams(@params);
            string commandId = QaCliToolSupport.ResolveCommandId(p);
            string scenarioId = p.Get("scenario_id") ?? string.Empty;
            return QaCommandGateway.BuildCaptureCommand(commandId, scenarioId);
        }

        /// <summary>
        /// <c>EditorScreenshot.CaptureCamera</c>(unity-cli-connector 패키지)와 동일한 렌더-투-텍스처
        /// 절차를 재사용하되, 파일로 쓰지 않고 PNG 바이트를 그대로 반환합니다 — 실제 저장은
        /// <see cref="IQaEvidenceRecorder.AttachScreenshot"/>가 evidence run 디렉터리 아래에서
        /// 전담합니다(SRP: 이 메서드는 "어떻게 캡처하는가"만 책임).
        ///
        /// <c>internal</c>인 이유: <see cref="QaEditorCommandGatewayInstaller"/>가 이 동일한 절차를
        /// <see cref="QaCommandGateway"/>의 <c>captureScreenshotPng</c> provider로 재사용하여,
        /// PNG 캡처 구현이 <c>qa_capture</c>와 mid-run <c>evidence.capture</c> 사이에서 하나만
        /// 존재하도록 합니다(중복 구현 금지).
        /// </summary>
        internal static byte[] CapturePngBytes(string view, int width, int height)
        {
            Camera camera = null;

            if (view == "scene")
            {
                SceneView sceneView = SceneView.lastActiveSceneView;
                camera = sceneView != null ? sceneView.camera : null;
            }
            else if (view == "game")
            {
                camera = Camera.main;
                if (camera == null)
                {
                    camera = UnityEngine.Object.FindFirstObjectByType<Camera>();
                }
            }
            else
            {
                return null;
            }

            if (camera == null)
            {
                throw new InvalidOperationException("No camera available to capture the '" + view + "' view.");
            }

            RenderTexture previousTargetTexture = camera.targetTexture;
            RenderTexture renderTexture = null;
            Texture2D texture = null;

            try
            {
                renderTexture = new RenderTexture(width, height, 24);
                camera.targetTexture = renderTexture;
                camera.Render();

                RenderTexture.active = renderTexture;
                texture = new Texture2D(width, height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();

                return texture.EncodeToPNG();
            }
            finally
            {
                camera.targetTexture = previousTargetTexture;
                RenderTexture.active = null;
                if (renderTexture != null)
                {
                    UnityEngine.Object.DestroyImmediate(renderTexture);
                }

                if (texture != null)
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }
        }
    }

    /// <summary>
    /// <c>qa_recover</c>: 이전 프로세스가 남긴 미해소 QA 프로필/리스 마커를 해소합니다. 특정
    /// <see cref="QaCommandType"/>에 대응하지 않는 서비스 레벨 연산이므로 <see cref="QaCommand"/>
    /// DTO로 감싸지 않습니다.
    /// </summary>
    [UnityCliTool(Name = "qa_recover", Group = "qa",
        Description = "Recover an interrupted QA profile/lease left behind by a previous process, so a new qa_run is not blocked.")]
    public static class QaRecover
    {
        private const string DefaultOwnerId = "unity-cli-qa_recover";

        public class Parameters
        {
            [ToolParameter("Owner id to record on the recovery attempt (diagnostics only).")]
            public string OwnerId { get; set; }
        }

        public static object HandleCommand(JObject @params)
        {
            if (@params == null)
            {
                @params = new JObject();
            }

            var p = new ToolParams(@params);
            string ownerId = p.Get("owner_id", DefaultOwnerId);

            QaGatewayOperationResult result = QaCommandGatewayHost.GetOrCreate().Recover(ownerId);
            return new SuccessResponse(result.Message, new { operationCode = result.Code.ToString() });
        }
    }
}
#endif
