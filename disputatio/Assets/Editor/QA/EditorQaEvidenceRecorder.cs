#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Godlotto.QA.Evidence;
using UnityEngine;

namespace Godlotto.QA.Evidence
{
    /// <summary>
    /// Unity 에디터(Unity CLI QA 게이트웨이, 개발자 패널)에서 실행되는 QA run이 사용하는
    /// evidence recorder. 실제 append/redaction/verdict 로직은 <see cref="DevelopmentQaEvidenceRecorder"/>를
    /// 그대로 재사용하고(SRP: 이 타입은 "저장소 루트를 어떻게 찾는가"만 책임), 저장 루트를
    /// 저장소(repo)의 <c>docs/qa/runs</c>로 해석하는 역할만 추가합니다.
    /// </summary>
    public sealed class EditorQaEvidenceRecorder : IQaEvidenceRecorder
    {
        private const string RunsRelativeDirectory1 = "docs";
        private const string RunsRelativeDirectory2 = "qa";
        private const string RunsRelativeDirectory3 = "runs";

        private readonly DevelopmentQaEvidenceRecorder inner;

        /// <param name="utcNowProvider">테스트용 시각 주입 훅. 생략하면 <see cref="DateTime.UtcNow"/> 사용.</param>
        /// <param name="redactedFieldNames">생략하면 <see cref="QaEvidenceRedactor.DefaultSensitiveFieldNames"/> 사용.</param>
        /// <param name="runsRootDirectoryOverride">
        /// 테스트에서 실제 저장소를 건드리지 않도록 임시 디렉터리를 주입할 수 있는 훅. 생략하면
        /// <see cref="ResolveRepoRunsRootDirectory"/>가 계산한 저장소 <c>docs/qa/runs</c>를 사용합니다.
        /// </param>
        public EditorQaEvidenceRecorder(
            Func<DateTime> utcNowProvider = null,
            IReadOnlyCollection<string> redactedFieldNames = null,
            string runsRootDirectoryOverride = null)
        {
            string root = runsRootDirectoryOverride ?? ResolveRepoRunsRootDirectory();
            Directory.CreateDirectory(root);
            inner = new DevelopmentQaEvidenceRecorder(root, utcNowProvider, redactedFieldNames);
        }

        /// <summary>
        /// 저장소 루트의 <c>docs/qa/runs</c> 절대 경로를 계산합니다.
        /// <c>Application.dataPath</c>는 <c>&lt;repo&gt;/disputatio/Assets</c>이므로, 두 단계
        /// 위로 올라가면 저장소(또는 이 worktree) 루트에 도달합니다.
        /// </summary>
        public static string ResolveRepoRunsRootDirectory()
        {
            string assetsPath = Application.dataPath;
            string projectRoot = Directory.GetParent(assetsPath)?.FullName
                ?? throw new InvalidOperationException("Unable to resolve the Unity project root from Application.dataPath.");
            string repoRoot = Directory.GetParent(projectRoot)?.FullName
                ?? throw new InvalidOperationException("Unable to resolve the repository root from the Unity project root.");

            return Path.Combine(repoRoot, RunsRelativeDirectory1, RunsRelativeDirectory2, RunsRelativeDirectory3);
        }

        /// <summary>현재 활성 run의 절대 디렉터리 경로. 활성 run이 없으면 <c>null</c>.</summary>
        public string RunDirectoryPath
        {
            get { return inner.RunDirectoryPath; }
        }

        public QaEvidenceOperationResult BeginRun(string runId, QaDriverSnapshot startSnapshot = null)
        {
            return inner.BeginRun(runId, startSnapshot);
        }

        public QaEvidenceOperationResult AppendEvent(QaEvidenceEvent evidenceEvent)
        {
            return inner.AppendEvent(evidenceEvent);
        }

        public QaEvidenceOperationResult AttachScreenshot(string commandId, byte[] pngBytes, string fileNameHint = null)
        {
            return inner.AttachScreenshot(commandId, pngBytes, fileNameHint);
        }

        public QaEvidenceOperationResult RecordConsole(string logText)
        {
            return inner.RecordConsole(logText);
        }

        public QaEvidenceFinalizeResult Finalize(QaDriverSnapshot endSnapshot = null)
        {
            return inner.Finalize(endSnapshot);
        }
    }
}
#endif
