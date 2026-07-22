using System;
using System.Collections;
using System.IO;
using Godlotto.QA.Core;
using Godlotto.QA.Evidence;
using Godlotto.QA.Gateway;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Task 10 §Step 3/4: <see cref="QaDeveloperPanel"/>이 headless 구동을 막지 않고(비활성 상태에서
/// GUIException 없이 존재할 수 있어야 함), 패널 렌더링 예외가 <see cref="QaCommandGatewayHost"/>가
/// 소유한 공유 QA 코어를 소유하거나 dispose하지 않는지(Task 10 제약) 검증합니다.
/// </summary>
public sealed class QaDeveloperPanelTests
{
    [TearDown]
    public void TearDown()
    {
        QaCommandGatewayHost.ResetForTests();
    }

    [UnityTest]
    public IEnumerator AddingPanel_WhileHidden_DoesNotLogGuiExceptionOrTouchGateway()
    {
        LogAssert.NoUnexpectedReceived();

        var host = new GameObject("QaDeveloperPanelHiddenHost");
        try
        {
            var panel = host.AddComponent<QaDeveloperPanel>();
            Assert.IsFalse(panel.IsVisible, "Panel must default to hidden so headless runs never render it.");

            yield return null;
        }
        finally
        {
            if (host != null)
            {
                UnityEngine.Object.Destroy(host);
            }
        }
    }

    [UnityTest]
    public IEnumerator VisiblePanel_RendersAllSectionsWithoutLoggingUnexpectedErrors()
    {
        LogAssert.NoUnexpectedReceived();

        QaCommandGatewayHost.ResetForTests();
        QaCommandGatewayHost.InstallInstanceForTests(BuildFakeGateway());

        var host = new GameObject("QaDeveloperPanelVisibleHost");
        try
        {
            var panel = host.AddComponent<QaDeveloperPanel>();
            panel.ConfigureReadinessProviders(() => true, () => true);
            panel.SetVisible(true);

            Assert.IsTrue(panel.IsVisible);

            // One frame is enough for Unity to dispatch the IMGUI Layout + Repaint events that
            // drive OnGUI -> GUILayout.Window -> DrawWindow (all 8 sections).
            yield return null;
        }
        finally
        {
            if (host != null)
            {
                UnityEngine.Object.Destroy(host);
            }
        }
    }

    [UnityTest]
    public IEnumerator PanelRenderingException_DoesNotOwnOrDisposeSharedGateway()
    {
        QaCommandGatewayHost.ResetForTests();
        QaCommandGateway fakeGateway = BuildFakeGateway();
        QaCommandGatewayHost.InstallInstanceForTests(fakeGateway);

        var host = new GameObject("QaDeveloperPanelFaultHost");

        // Unity's own GUILayout.Window plumbing catches exceptions thrown by the window callback
        // before they ever reach QaDeveloperPanel.OnGUI's own try/catch, and logs them as an
        // unhandled LogType.Exception entry — possibly more than once, and not necessarily on the
        // exact frame we yield for. That is exactly the "never crashes" behavior Task 10 requires;
        // we only need to stop the test framework from treating that expected log noise as a
        // failure for the whole span where it could occur (including panel/host teardown below),
        // then assert the real invariant afterwards (the shared gateway is untouched).
        LogAssert.ignoreFailingMessages = true;
        try
        {
            var panel = host.AddComponent<QaDeveloperPanel>();
            panel.SetVisible(true);
            panel.DrawWindowFaultInjectorForTests =
                () => throw new InvalidOperationException("Task 10 forced QA panel rendering fault.");

            yield return null;
            yield return null;
            yield return null;
        }
        finally
        {
            if (host != null)
            {
                UnityEngine.Object.Destroy(host);
            }

            LogAssert.ignoreFailingMessages = false;
        }

        Assert.AreSame(
            fakeGateway,
            QaCommandGatewayHost.GetOrCreate(),
            "A panel rendering exception must never own, replace, or dispose the shared QA command gateway.");
    }

    [Test]
    public void BuildRunCommandForPanel_IsDeterministic_ForSameInputs()
    {
        QaCommand first = QaDeveloperPanel.BuildRunCommandForPanel("cmd-1", "kitchen.faucet-key", 45000);
        QaCommand second = QaDeveloperPanel.BuildRunCommandForPanel("cmd-1", "kitchen.faucet-key", 45000);

        Assert.AreEqual(first.Id, second.Id);
        Assert.AreEqual(first.Type, second.Type);
        Assert.AreEqual(first.TargetId, second.TargetId);
        CollectionAssert.AreEquivalent(first.Parameters, second.Parameters);
    }

    private static QaCommandGateway BuildFakeGateway()
    {
        string root = Path.Combine(
            Application.temporaryCachePath, "QaDeveloperPanelTests", Guid.NewGuid().ToString("N"));
        var recorder = new DevelopmentQaEvidenceRecorder(root);
        return new QaCommandGateway(
            recorder,
            evidenceRunDirectoryProvider: () => recorder.RunDirectoryPath,
            scenarioSourceProvider: () => Array.Empty<(string Name, string Json)>());
    }
}
