#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Godlotto.Interaction;
using Godlotto.QA.Developer;
using Godlotto.QA.Input;
using Godlotto.QA.Scenes;
using UnityEngine;
using UnityEngine.SceneManagement;
using Task = System.Threading.Tasks.Task;

namespace Godlotto.QA.SceneAdapters
{
    /// <summary>
    /// BasementHallway Sequence door QA adapter. Drives the five real
    /// <see cref="BasementHallwayInteractionController.OnInteraction"/> ids
    /// (<c>door_brick</c> … <c>entry_upper</c>). Never executes Fungus blocks.
    /// assert-route requires the destination from the last successful click,
    /// a finished transition, and an open input gate.
    /// </summary>
    public sealed class BasementHallwayQaAdapter : IQaSceneAdapter, IQaApiInteractable
    {
        public const string DoorBrickTargetIdValue = "basement-hallway.door.brick";
        public const string DoorExtractionTargetIdValue = "basement-hallway.door.extraction";
        public const string DoorObservationTargetIdValue = "basement-hallway.door.observation";
        public const string DoorResearchTargetIdValue = "basement-hallway.door.research";
        public const string EntryUpperTargetIdValue = "basement-hallway.entry.upper";

        public const string DoorBrickInteractionId = "door_brick";
        public const string DoorExtractionInteractionId = "door_extraction";
        public const string DoorObservationInteractionId = "door_observation";
        public const string DoorResearchInteractionId = "door_research";
        public const string EntryUpperInteractionId = "entry_upper";

        public const string ClickDoorBrickCapabilityId = "basement-hallway.nav.click-door-brick";
        public const string ClickDoorExtractionCapabilityId = "basement-hallway.nav.click-door-extraction";
        public const string ClickDoorObservationCapabilityId = "basement-hallway.nav.click-door-observation";
        public const string ClickDoorResearchCapabilityId = "basement-hallway.nav.click-door-research";
        public const string ClickEntryUpperCapabilityId = "basement-hallway.nav.click-entry-upper";
        public const string NavProbeCapabilityId = "basement-hallway.nav.probe";
        public const string NavAssertRouteCapabilityId = "basement-hallway.nav.assert-route";
        public const string NavCaptureCapabilityId = "basement-hallway.nav.capture";
        public const string ResetToHallwayCapabilityId = "basement-hallway.nav.reset-to-hallway";

        private static readonly QaTargetId DoorBrickTargetId = QaTargetId.Create(DoorBrickTargetIdValue);
        private static readonly QaTargetId DoorExtractionTargetId = QaTargetId.Create(DoorExtractionTargetIdValue);
        private static readonly QaTargetId DoorObservationTargetId = QaTargetId.Create(DoorObservationTargetIdValue);
        private static readonly QaTargetId DoorResearchTargetId = QaTargetId.Create(DoorResearchTargetIdValue);
        private static readonly QaTargetId EntryUpperTargetId = QaTargetId.Create(EntryUpperTargetIdValue);

        private static readonly IReadOnlyDictionary<QaTargetId, string> InteractionIdByTarget =
            new Dictionary<QaTargetId, string>
            {
                [DoorBrickTargetId] = DoorBrickInteractionId,
                [DoorExtractionTargetId] = DoorExtractionInteractionId,
                [DoorObservationTargetId] = DoorObservationInteractionId,
                [DoorResearchTargetId] = DoorResearchInteractionId,
                [EntryUpperTargetId] = EntryUpperInteractionId
            };

        private static readonly IReadOnlyDictionary<string, string> DestinationByInteractionId =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [DoorBrickInteractionId] = SceneNames.BasementBrickRoom,
                [DoorExtractionInteractionId] = SceneNames.BasementExtractionRoom,
                [DoorObservationInteractionId] = SceneNames.BasementObservationRoom,
                [DoorResearchInteractionId] = SceneNames.BasementResearchRoom,
                [EntryUpperInteractionId] = SceneNames.Basement
            };

        private static readonly IReadOnlyCollection<QaTargetId> DeclaredTargetIds =
            new List<QaTargetId>
            {
                DoorBrickTargetId,
                DoorExtractionTargetId,
                DoorObservationTargetId,
                DoorResearchTargetId,
                EntryUpperTargetId
            };

        private static readonly IReadOnlyCollection<string> DeclaredPresetIds = Array.Empty<string>();

        public const int RouteReadyTimeoutMs = 15000;
        public const int RouteReadyPollMs = 100;

        private static string lastExpectedDestination;
        private static int consoleErrorCount;
        private static bool consoleHookInstalled;

        public string SceneName
        {
            get { return SceneNames.BasementHallway; }
        }

        public IReadOnlyCollection<QaTargetId> TargetIds
        {
            get { return DeclaredTargetIds; }
        }

        public IReadOnlyCollection<string> PresetIds
        {
            get { return DeclaredPresetIds; }
        }

        public static string LastExpectedDestinationForTests
        {
            get { return lastExpectedDestination; }
        }

        public static void ResetLastExpectedDestinationForTests()
        {
            lastExpectedDestination = null;
            consoleErrorCount = 0;
        }

        public static int ConsoleErrorCountForTests
        {
            get { return consoleErrorCount; }
        }

        public static string DestinationForInteraction(string interactionId)
        {
            string destination;
            if (string.IsNullOrWhiteSpace(interactionId)
                || !DestinationByInteractionId.TryGetValue(interactionId, out destination))
            {
                return null;
            }

            return destination;
        }

        public static void RegisterCapabilities(DeveloperQaCapabilityRegistry registry)
        {
            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry));
            }

            string sceneId = SceneNames.BasementHallway;
            var adapter = new BasementHallwayQaAdapter();

            RegisterClick(registry, ClickDoorBrickCapabilityId, sceneId, adapter, DoorBrickTargetId);
            RegisterClick(registry, ClickDoorExtractionCapabilityId, sceneId, adapter, DoorExtractionTargetId);
            RegisterClick(registry, ClickDoorObservationCapabilityId, sceneId, adapter, DoorObservationTargetId);
            RegisterClick(registry, ClickDoorResearchCapabilityId, sceneId, adapter, DoorResearchTargetId);
            RegisterClick(registry, ClickEntryUpperCapabilityId, sceneId, adapter, EntryUpperTargetId);

            registry.Register(
                new DeveloperQaCapability(
                    NavProbeCapabilityId,
                    sceneId,
                    DeveloperQaCapabilityKind.Probe,
                    "{}",
                    "{controllerFound:bool,activeScene:string,transitionPending:bool,inputGateBlocked:bool,expectedDestination:string,fadeSettled:bool,consoleErrorCount:int}"),
                _ => MapSnapshot(adapter, assertRoute: false));

            registry.Register(
                new DeveloperQaCapability(
                    NavCaptureCapabilityId,
                    sceneId,
                    DeveloperQaCapabilityKind.Probe,
                    "{}",
                    "{controllerFound:bool,activeScene:string,transitionPending:bool,inputGateBlocked:bool,expectedDestination:string,fadeSettled:bool,consoleErrorCount:int}"),
                _ => MapSnapshot(adapter, assertRoute: false));

            registry.RegisterAsync(
                new DeveloperQaCapability(
                    NavAssertRouteCapabilityId,
                    sceneId,
                    DeveloperQaCapabilityKind.Assertion,
                    "{}",
                    "{controllerFound:bool,activeScene:string,transitionPending:bool,inputGateBlocked:bool,expectedDestination:string,fadeSettled:bool,consoleErrorDelta:int,assertPassed:bool}"),
                (_, cancellationToken) => MapAssertRouteAsync(adapter, cancellationToken));

            registry.RegisterAsync(
                new DeveloperQaCapability(
                    ResetToHallwayCapabilityId,
                    sceneId,
                    DeveloperQaCapabilityKind.Recovery,
                    "{}",
                    "{reset:bool,activeScene:string,fadeSettled:bool,consoleErrorDelta:int}"),
                (_, cancellationToken) => MapResetToHallwayAsync(cancellationToken));
        }

        public QaScenePresetResult ApplyPreset(string presetId)
        {
            return QaScenePresetResult.UnknownPreset(presetId);
        }

        public QaSceneSnapshot CaptureSnapshot()
        {
            EnsureConsoleHook();
            BasementHallwayInteractionController controller = ResolveController();
            string activeScene = SceneManager.GetActiveScene().name;
            bool transitionPending = SceneTransitionService.IsTransitionPending;
            bool atExpected = !string.IsNullOrEmpty(lastExpectedDestination)
                && string.Equals(activeScene, lastExpectedDestination, StringComparison.Ordinal);
            bool fadeSettled = atExpected && !transitionPending;
            var values = new Dictionary<string, string>
            {
                ["controllerFound"] = (controller != null).ToString(),
                ["activeScene"] = activeScene,
                ["transitionPending"] = transitionPending.ToString(),
                ["inputGateBlocked"] = InteractionInputGate.IsBlocked.ToString(),
                ["expectedDestination"] = lastExpectedDestination ?? string.Empty,
                ["fadeSettled"] = fadeSettled.ToString(),
                ["consoleErrorCount"] = consoleErrorCount.ToString()
            };

            return QaSceneSnapshot.Create(SceneName, DateTime.UtcNow, values);
        }

        public bool TryClick(QaTargetId targetId, out string error)
        {
            string interactionId;
            if (!InteractionIdByTarget.TryGetValue(targetId, out interactionId))
            {
                error = "BasementHallwayQaAdapter does not own target '" + targetId + "'.";
                return false;
            }

            BasementHallwayInteractionController controller = ResolveController();
            if (controller == null)
            {
                error = "BasementHallwayInteractionController not found in the active scene. " +
                    "This adapter only works while BasementHallway is the active Play Mode scene.";
                return false;
            }

            controller.OnInteraction(interactionId);
            lastExpectedDestination = DestinationForInteraction(interactionId);
            error = null;
            return true;
        }

        public bool TryDrag(QaTargetId sourceTargetId, QaTargetId destinationTargetId, out string error)
        {
            error = "BasementHallwayQaAdapter does not support drag interactions for target '" +
                sourceTargetId + "'.";
            return false;
        }

        public bool TryKey(QaTargetId targetId, string text, out string error)
        {
            error = "BasementHallwayQaAdapter does not support key interactions for target '" +
                targetId + "'.";
            return false;
        }

        private static void RegisterClick(
            DeveloperQaCapabilityRegistry registry,
            string capabilityId,
            string sceneId,
            BasementHallwayQaAdapter adapter,
            QaTargetId targetId)
        {
            registry.Register(
                new DeveloperQaCapability(
                    capabilityId,
                    sceneId,
                    DeveloperQaCapabilityKind.Interaction,
                    "{}",
                    "{clicked:bool,interactionId:string,expectedDestination:string}"),
                _ => MapClick(adapter, targetId));
        }

        private static DeveloperQaResult MapClick(BasementHallwayQaAdapter adapter, QaTargetId targetId)
        {
            if (adapter == null)
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.EnvironmentBlocked,
                    "BasementHallwayQaAdapter instance is required for door click.");
            }

            string error;
            if (adapter.TryClick(targetId, out error))
            {
                string interactionId = InteractionIdByTarget[targetId];
                return new DeveloperQaResult(
                    DeveloperQaResultCode.Ok,
                    "BasementHallway door click dispatched (OnInteraction(\"" + interactionId + "\")).",
                    data: new Dictionary<string, string>
                    {
                        ["clicked"] = "True",
                        ["interactionId"] = interactionId,
                        ["expectedDestination"] = lastExpectedDestination ?? string.Empty
                    });
            }

            return new DeveloperQaResult(
                DeveloperQaResultCode.EnvironmentBlocked,
                string.IsNullOrEmpty(error)
                    ? "BasementHallway door click blocked (BasementHallway scene unavailable)."
                    : error,
                data: new Dictionary<string, string>
                {
                    ["clicked"] = "False"
                });
        }

        private static DeveloperQaResult MapSnapshot(BasementHallwayQaAdapter adapter, bool assertRoute)
        {
            if (adapter == null)
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.EnvironmentBlocked,
                    "BasementHallwayQaAdapter instance is required for nav snapshot.");
            }

            QaSceneSnapshot snapshot = adapter.CaptureSnapshot();
            var data = new Dictionary<string, string>();
            if (snapshot != null && snapshot.Values != null)
            {
                foreach (KeyValuePair<string, string> pair in snapshot.Values)
                {
                    data[pair.Key] = pair.Value;
                }
            }

            if (assertRoute)
            {
                BasementHallwayQaRouteAssertionResult assertion =
                    BasementHallwayQaRouteAssertion.Evaluate(data, lastExpectedDestination);
                data["assertPassed"] = assertion.Passed.ToString();
                data["reasonCodes"] = string.Join(",", assertion.ReasonCodes);
                if (!assertion.Passed)
                {
                    return new DeveloperQaResult(
                        DeveloperQaResultCode.AssertionFailed,
                        "BasementHallway nav assert-route failed: " + data["reasonCodes"] + ".",
                        data: data);
                }
            }

            return new DeveloperQaResult(
                DeveloperQaResultCode.Ok,
                assertRoute
                    ? "BasementHallway nav assert-route passed."
                    : "BasementHallway nav snapshot captured.",
                data: data);
        }

        private static async Task<DeveloperQaResult> MapAssertRouteAsync(
            BasementHallwayQaAdapter adapter,
            CancellationToken cancellationToken)
        {
            EnsureConsoleHook();
            if (string.IsNullOrWhiteSpace(lastExpectedDestination))
            {
                return MapSnapshot(adapter, assertRoute: true);
            }

            int errorsAtStart = consoleErrorCount;
            DeveloperQaResult last = MapSnapshot(adapter, assertRoute: true);
            if (IsRouteReady(last, errorsAtStart))
            {
                return AnnotateConsoleDelta(last, errorsAtStart);
            }

            int elapsed = 0;
            while (elapsed < RouteReadyTimeoutMs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(RouteReadyPollMs, cancellationToken).ConfigureAwait(true);
                elapsed += RouteReadyPollMs;
                last = MapSnapshot(adapter, assertRoute: true);
                if (IsRouteReady(last, errorsAtStart))
                {
                    return AnnotateConsoleDelta(last, errorsAtStart);
                }
            }

            last = AnnotateConsoleDelta(last, errorsAtStart);
            if (last.Code == DeveloperQaResultCode.Ok
                && consoleErrorCount > errorsAtStart)
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.AssertionFailed,
                    "BasementHallway nav assert-route failed: console-delta.",
                    data: last.Data);
            }

            return last;
        }

        private static async Task<DeveloperQaResult> MapResetToHallwayAsync(
            CancellationToken cancellationToken)
        {
            EnsureConsoleHook();
            int errorsAtStart = consoleErrorCount;
            lastExpectedDestination = SceneNames.BasementHallway;
            bool loaded = SceneTransitionService.LoadSceneSafely(SceneNames.BasementHallway);
            if (!loaded)
            {
                lastExpectedDestination = null;
                return new DeveloperQaResult(
                    DeveloperQaResultCode.EnvironmentBlocked,
                    "Failed to load BasementHallway for QA re-entry.",
                    data: new Dictionary<string, string>
                    {
                        ["reset"] = "False",
                        ["activeScene"] = SceneManager.GetActiveScene().name,
                        ["consoleErrorDelta"] = "0"
                    });
            }

            var adapter = new BasementHallwayQaAdapter();
            DeveloperQaResult ready = await MapAssertRouteAsync(adapter, cancellationToken)
                .ConfigureAwait(true);
            lastExpectedDestination = null;
            var data = new Dictionary<string, string>();
            if (ready.Data != null)
            {
                foreach (KeyValuePair<string, string> pair in ready.Data)
                {
                    data[pair.Key] = pair.Value;
                }
            }

            data["reset"] = ready.Code == DeveloperQaResultCode.Ok ? "True" : "False";
            data["consoleErrorDelta"] = (consoleErrorCount - errorsAtStart).ToString();
            if (ready.Code != DeveloperQaResultCode.Ok)
            {
                return new DeveloperQaResult(
                    ready.Code,
                    "BasementHallway re-entry did not settle: " + ready.Message,
                    data: data);
            }

            return new DeveloperQaResult(
                DeveloperQaResultCode.Ok,
                "BasementHallway re-entry settled (fade/transition complete, input unlocked).",
                data: data);
        }

        private static bool IsRouteReady(DeveloperQaResult result, int errorsAtStart)
        {
            if (result == null || result.Code != DeveloperQaResultCode.Ok || result.Data == null)
            {
                return false;
            }

            string fadeSettled;
            if (!result.Data.TryGetValue("fadeSettled", out fadeSettled)
                || !string.Equals(fadeSettled, bool.TrueString, StringComparison.Ordinal))
            {
                return false;
            }

            return consoleErrorCount <= errorsAtStart;
        }

        private static DeveloperQaResult AnnotateConsoleDelta(DeveloperQaResult result, int errorsAtStart)
        {
            if (result == null)
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.EnvironmentBlocked,
                    "BasementHallway assert returned null.");
            }

            var data = new Dictionary<string, string>();
            if (result.Data != null)
            {
                foreach (KeyValuePair<string, string> pair in result.Data)
                {
                    data[pair.Key] = pair.Value;
                }
            }

            int delta = consoleErrorCount - errorsAtStart;
            data["consoleErrorDelta"] = delta.ToString();
            if (result.Code == DeveloperQaResultCode.Ok && delta > 0)
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.AssertionFailed,
                    "BasementHallway nav assert-route failed: console-delta.",
                    data: data);
            }

            return new DeveloperQaResult(result.Code, result.Message, data: data);
        }

        private static void EnsureConsoleHook()
        {
            if (consoleHookInstalled)
            {
                return;
            }

            Application.logMessageReceived += OnConsoleLogMessage;
            consoleHookInstalled = true;
        }

        private static void OnConsoleLogMessage(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception)
            {
                consoleErrorCount++;
            }
        }

        private static BasementHallwayInteractionController ResolveController()
        {
            return UnityEngine.Object.FindFirstObjectByType<BasementHallwayInteractionController>();
        }
    }
}
#endif
