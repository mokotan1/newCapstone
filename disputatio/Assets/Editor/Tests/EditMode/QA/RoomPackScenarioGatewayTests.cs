#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Godlotto.QA.Core;
using Godlotto.QA.Developer;
using Godlotto.QA.Evidence;
using Godlotto.QA.Gateway;
using Godlotto.QA.Profile;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// QA_INFRA_DEFECT: room-pack DeveloperQa JSON must list/run by JSON <c>id</c>
/// (e.g. <c>room.kitchen.smoke</c>), not TextAsset filename (<c>smoke</c>).
/// </summary>
[TestFixture]
public sealed class RoomPackScenarioGatewayTests
{
    private const string KitchenSmokeId = "room.kitchen.smoke";

    private const string KitchenSmokeRelative =
        "Assets/Resources/QA/Scenarios/Rooms/first-floor/kitchen/smoke.json";

    [Test]
    public void DeveloperQaValidator_KitchenSmoke_WithRoomId_IsValid()
    {
        string path = LocateRelative(KitchenSmokeRelative);
        Assert.IsTrue(File.Exists(path), "Expected kitchen smoke at " + path);

        string json = File.ReadAllText(path);
        var validator = new DeveloperQaScenarioValidator();
        DeveloperQaScenarioValidationResult result = validator.Validate(json);

        Assert.IsTrue(result.IsValid, string.Join("; ", result.Errors));
        Assert.AreEqual(KitchenSmokeId, result.Scenario.Id);
        Assert.AreEqual("kitchen", result.Scenario.RoomId);
    }

    [Test]
    public void ResolveScenarioPath_RoomKitchenSmoke_FindsNestedRoomsPack()
    {
        string resolved = DeveloperQaScenarioRunner.ResolveScenarioPath(KitchenSmokeId);
        Assert.IsFalse(string.IsNullOrEmpty(resolved), "ResolveScenarioPath returned empty.");
        Assert.IsTrue(File.Exists(resolved), "Resolved path must exist: " + resolved);

        string json = File.ReadAllText(resolved);
        Assert.IsTrue(
            json.Contains("\"id\": \"" + KitchenSmokeId + "\"")
            || json.Contains("\"id\":\"" + KitchenSmokeId + "\""),
            "Resolved file must be the kitchen smoke pack.");
    }

    [Test]
    public void ResolveDeveloperQaSceneName_KitchenSmoke_UsesSiblingManifestUnityScene()
    {
        string path = LocateRelative(KitchenSmokeRelative);
        string json = File.ReadAllText(path);

        string sceneName = QaCommandGateway.ResolveDeveloperQaSceneName(
            KitchenSmokeId,
            json);

        Assert.AreEqual("Kitchen", sceneName);
    }

    [Test]
    public void ListScenarios_InjectedKitchenSmoke_ReportsJsonIdAsValid()
    {
        string path = LocateRelative(KitchenSmokeRelative);
        string json = File.ReadAllText(path);

        using (var gateway = new QaCommandGateway(
            new FakeEvidenceRecorder(),
            scenarioSourceProvider: () => new List<(string Name, string Json)>
            {
                ("smoke", json)
            }))
        {
            IReadOnlyList<QaGatewayScenarioSummary> listed = gateway.ListScenarios();
            QaGatewayScenarioSummary match = listed.FirstOrDefault(
                s => string.Equals(s.ScenarioId, KitchenSmokeId, StringComparison.Ordinal));

            Assert.IsNotNull(match, "Expected scenario id '" + KitchenSmokeId + "' in qa_list.");
            Assert.IsTrue(match.IsValid, string.Join("; ", match.ValidationErrors));
            Assert.AreEqual(KitchenSmokeId, match.ScenarioId);
            Assert.AreNotEqual("smoke", match.ScenarioId);
        }
    }

    [Test]
    public async Task RunScenarioAsync_DeveloperQaRoomPack_RoutesById()
    {
        string path = LocateRelative(KitchenSmokeRelative);
        string json = File.ReadAllText(path);
        var recording = new RecordingDeveloperQaService();

        using (var gateway = new QaCommandGateway(
            new FakeEvidenceRecorder(),
            scenarioSourceProvider: () => new List<(string Name, string Json)>
            {
                ("smoke", json)
            },
            developerQaServiceFactory: () => recording))
        {
            QaGatewayRunResult result = await gateway.RunScenarioAsync(
                KitchenSmokeId,
                TimeSpan.FromSeconds(5),
                CancellationToken.None);

            Assert.IsTrue(result.IsSuccess, result.Message);
            Assert.IsNotNull(result.Outcome);
            Assert.AreEqual(1, recording.RunCount);
            Assert.AreEqual(KitchenSmokeId, recording.LastScenarioId);
        }
    }

    private static string LocateRelative(string relativeUnderDisputatio)
    {
        string dataPath = Application.dataPath;
        if (!string.IsNullOrEmpty(dataPath))
        {
            string underAssets = Path.GetFullPath(Path.Combine(dataPath, "..", relativeUnderDisputatio));
            if (File.Exists(underAssets))
            {
                return underAssets;
            }
        }

        string cwd = Directory.GetCurrentDirectory();
        string[] candidates =
        {
            Path.Combine(cwd, "disputatio", relativeUnderDisputatio),
            Path.Combine(cwd, relativeUnderDisputatio)
        };
        for (int i = 0; i < candidates.Length; i++)
        {
            string full = Path.GetFullPath(candidates[i]);
            if (File.Exists(full))
            {
                return full;
            }
        }

        return Path.GetFullPath(Path.Combine(cwd, "disputatio", relativeUnderDisputatio));
    }

    private sealed class FakeEvidenceRecorder : IQaEvidenceRecorder
    {
        public QaEvidenceOperationResult BeginRun(string runId, QaDriverSnapshot startSnapshot = null)
        {
            return QaEvidenceOperationResult.Success("ok");
        }

        public QaEvidenceOperationResult AppendEvent(QaEvidenceEvent evidenceEvent)
        {
            return QaEvidenceOperationResult.Success();
        }

        public QaEvidenceOperationResult AttachScreenshot(string commandId, byte[] pngBytes, string fileNameHint = null)
        {
            return QaEvidenceOperationResult.Success();
        }

        public QaEvidenceOperationResult RecordConsole(string logText)
        {
            return QaEvidenceOperationResult.Success();
        }

        public QaEvidenceFinalizeResult Finalize(QaDriverSnapshot endSnapshot = null)
        {
            return QaEvidenceFinalizeResult.Success(
                QaRunManifest.Create(
                    "fake",
                    "fake-dir",
                    DateTime.UtcNow,
                    DateTime.UtcNow,
                    Array.Empty<QaEvidenceEvent>(),
                    "events.jsonl",
                    "console.log",
                    "screenshots",
                    "report.md"),
                "fake-dir");
        }
    }

    private sealed class RecordingDeveloperQaService : IDeveloperQaService
    {
        public int RunCount { get; private set; }

        public string LastScenarioId { get; private set; }

        public Task<DeveloperQaResult> ExecuteAsync(DeveloperQaCommand command, CancellationToken cancellationToken)
        {
            if (command != null
                && string.Equals(command.Family, "scenario", StringComparison.Ordinal)
                && string.Equals(command.Name, "run", StringComparison.Ordinal))
            {
                RunCount++;
                if (command.Parameters != null
                    && command.Parameters.TryGetValue("scenario_id", out string id))
                {
                    LastScenarioId = id;
                }
                else
                {
                    LastScenarioId = command.TargetId;
                }

                return Task.FromResult(new DeveloperQaResult(
                    DeveloperQaResultCode.Ok,
                    "Scenario completed.",
                    data: new Dictionary<string, string>
                    {
                        ["state"] = DeveloperQaScenarioStates.Completed,
                        ["scenario_id"] = LastScenarioId ?? string.Empty
                    }));
            }

            return Task.FromResult(new DeveloperQaResult(
                DeveloperQaResultCode.UnsupportedCommand,
                "unexpected"));
        }

        public DeveloperQaSnapshot CaptureSnapshot()
        {
            return new DeveloperQaSnapshot(
                System.DateTime.UtcNow.ToString("o"),
                string.Empty,
                string.Empty,
                "0",
                null);
        }

        public IReadOnlyCollection<DeveloperQaCapability> ListCapabilities()
        {
            return Array.Empty<DeveloperQaCapability>();
        }
    }
}
#endif
