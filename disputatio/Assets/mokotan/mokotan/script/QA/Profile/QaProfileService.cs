#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Godlotto.QA.Core;
using UnityEngine;

namespace Godlotto.QA.Profile
{
    /// <summary>PlayerPrefs에 저장되는 값의 종류. 스냅샷 직렬화·복원에 사용됩니다.</summary>
    public enum QaPlayerPrefsValueKind
    {
        Int,
        String
    }

    /// <summary>
    /// "무엇이 일반 진행(gameplay) PlayerPrefs 키인가"에 대한 명시적 분류 항목. 문자열을
    /// 직접 흩어 쓰지 않고 이 카탈로그(<see cref="QaProfileService.KnownGameplayKeys"/>)만
    /// 참조하도록 하여, 격리 대상 키 목록이 한 곳에서만 정의되게 합니다.
    /// </summary>
    public readonly struct QaGameplayKeyDefinition
    {
        public string Key { get; }

        public QaPlayerPrefsValueKind Kind { get; }

        public QaGameplayKeyDefinition(string key, QaPlayerPrefsValueKind kind)
        {
            Key = key;
            Kind = kind;
        }
    }

    /// <summary>
    /// 한 진행 키에 대한 QA 시작 시점 스냅샷. <see cref="Existed"/>가 false면 해당 키가
    /// 원래 존재하지 않았다는 뜻이며, 복원 시에는 값을 쓰는 대신 키를 삭제해야 합니다
    /// (그래야 "복원 후 상태 == QA 시작 전 상태"가 byte-for-byte 성립합니다).
    /// </summary>
    public sealed class QaGameplaySnapshotEntry
    {
        public string Key { get; }

        public QaPlayerPrefsValueKind Kind { get; }

        public bool Existed { get; }

        /// <summary>불변 문자열 표현(Int는 InvariantCulture 십진 표현). <see cref="Existed"/>가 false면 null.</summary>
        public string RawValue { get; }

        public QaGameplaySnapshotEntry(string key, QaPlayerPrefsValueKind kind, bool existed, string rawValue)
        {
            Key = key;
            Kind = kind;
            Existed = existed;
            RawValue = rawValue;
        }
    }

    /// <summary>
    /// 디스크에 영속화되는 QA 프로필 복구 마커의 불변 스냅샷. <see cref="QaLeaseRecoveryMarker"/>와
    /// 동일한 철학: 비밀값을 담지 않고, 프로세스가 비정상 종료되어도 다음 프로세스가 일반
    /// 진행 데이터를 정확히 복원할 수 있을 만큼의 정보만 보관합니다.
    /// </summary>
    public sealed class QaProfileMarker
    {
        public string RunId { get; }

        public IReadOnlyList<QaGameplaySnapshotEntry> Entries { get; }

        private QaProfileMarker(string runId, IReadOnlyList<QaGameplaySnapshotEntry> entries)
        {
            RunId = runId;
            Entries = entries ?? Array.Empty<QaGameplaySnapshotEntry>();
        }

        public static QaProfileMarker Create(string runId, IReadOnlyList<QaGameplaySnapshotEntry> entries)
        {
            return new QaProfileMarker(runId, entries);
        }
    }

    /// <summary>
    /// <see cref="QaProfileMarker"/>를 영속화하는 추상 저장소. 테스트에서는 실제 파일 시스템을
    /// 건드리지 않도록 인메모리 구현을 주입할 수 있습니다(의존성 역전).
    /// </summary>
    public interface IQaProfileMarkerStore
    {
        /// <summary>이전에 저장된 마커를 불러옵니다. 없거나 읽기에 실패하면 <c>null</c>.</summary>
        QaProfileMarker Load();

        /// <summary>마커를 저장(갱신)합니다.</summary>
        void Save(QaProfileMarker marker);

        /// <summary>저장된 마커를 제거합니다(정상 종료 또는 복구 완료 시).</summary>
        void Clear();
    }

    /// <summary>영속화를 하지 않는 널 오브젝트. 복구 기능이 필요 없는 호출자를 위한 기본값.</summary>
    public sealed class QaNullProfileMarkerStore : IQaProfileMarkerStore
    {
        public static readonly QaNullProfileMarkerStore Instance = new QaNullProfileMarkerStore();

        private QaNullProfileMarkerStore()
        {
        }

        public QaProfileMarker Load()
        {
            return null;
        }

        public void Save(QaProfileMarker marker)
        {
        }

        public void Clear()
        {
        }
    }

    /// <summary>
    /// <c>Application.persistentDataPath</c> 아래 QA 영역에 마커를 저장하는 기본 구현.
    /// <see cref="QaLeaseService"/>의 <c>QaFileLeaseRecoveryStore</c>와 동일한 실패-안전
    /// 원칙을 따릅니다: 읽기/쓰기 실패는 절대 밖으로 던지지 않고 "마커 없음"으로 취급합니다.
    /// 값은 구분자 충돌을 피하기 위해 Base64로 인코딩해 한 줄에 하나씩 기록합니다.
    /// </summary>
    public sealed class QaFileProfileMarkerStore : IQaProfileMarkerStore
    {
        private const string RunIdPrefix = "runId=";
        private const string EntryPrefix = "entry=";
        private const char FieldSeparator = '|';

        private readonly string filePath;

        public QaFileProfileMarkerStore(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path must not be blank.", nameof(filePath));
            }

            this.filePath = filePath;
        }

        /// <summary>기본 경로(<c>QA/profile-recovery.marker</c>)를 사용하는 인스턴스를 생성합니다.</summary>
        public static QaFileProfileMarkerStore CreateDefault()
        {
            string basePath = Application.persistentDataPath;
            string path = Path.Combine(basePath, "QA", "profile-recovery.marker");
            return new QaFileProfileMarkerStore(path);
        }

        public QaProfileMarker Load()
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return null;
                }

                string runId = null;
                var entries = new List<QaGameplaySnapshotEntry>();

                foreach (string line in File.ReadAllLines(filePath))
                {
                    if (line.StartsWith(RunIdPrefix, StringComparison.Ordinal))
                    {
                        runId = line.Substring(RunIdPrefix.Length);
                    }
                    else if (line.StartsWith(EntryPrefix, StringComparison.Ordinal))
                    {
                        QaGameplaySnapshotEntry entry = ParseEntryLine(line.Substring(EntryPrefix.Length));
                        if (entry != null)
                        {
                            entries.Add(entry);
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(runId))
                {
                    return null;
                }

                return QaProfileMarker.Create(runId, entries);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[QaProfileService] Failed to read profile recovery marker: " + ex.GetType().Name);
                return null;
            }
        }

        public void Save(QaProfileMarker marker)
        {
            if (marker == null)
            {
                return;
            }

            try
            {
                string directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var content = new StringBuilder();
                content.Append(RunIdPrefix).Append(marker.RunId).Append(Environment.NewLine);

                foreach (QaGameplaySnapshotEntry entry in marker.Entries)
                {
                    content.Append(EntryPrefix).Append(FormatEntryLine(entry)).Append(Environment.NewLine);
                }

                File.WriteAllText(filePath, content.ToString());
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[QaProfileService] Failed to persist profile recovery marker: " + ex.GetType().Name);
            }
        }

        public void Clear()
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[QaProfileService] Failed to clear profile recovery marker: " + ex.GetType().Name);
            }
        }

        private static string FormatEntryLine(QaGameplaySnapshotEntry entry)
        {
            string base64Value = entry.Existed
                ? Convert.ToBase64String(Encoding.UTF8.GetBytes(entry.RawValue ?? string.Empty))
                : string.Empty;

            return entry.Key + FieldSeparator
                + entry.Kind + FieldSeparator
                + (entry.Existed ? "1" : "0") + FieldSeparator
                + base64Value;
        }

        private static QaGameplaySnapshotEntry ParseEntryLine(string payload)
        {
            string[] parts = payload.Split(FieldSeparator);
            if (parts.Length != 4)
            {
                return null;
            }

            string key = parts[0];
            if (string.IsNullOrEmpty(key) || !Enum.TryParse(parts[1], out QaPlayerPrefsValueKind kind))
            {
                return null;
            }

            bool existed = parts[2] == "1";
            string rawValue = null;

            if (existed)
            {
                try
                {
                    rawValue = Encoding.UTF8.GetString(Convert.FromBase64String(parts[3]));
                }
                catch (FormatException)
                {
                    return null;
                }
            }

            return new QaGameplaySnapshotEntry(key, kind, existed, rawValue);
        }
    }

    /// <summary>
    /// 단일 EditMode/DEVELOPMENT_BUILD 프로세스 안에서 QA 프로필을 관리하는 기본 구현.
    /// 일반 진행 PlayerPrefs 키를 명시적 카탈로그(<see cref="KnownGameplayKeys"/>)로만
    /// 다루고, 오디오/비디오 설정은 <see cref="PlayDataPrefsCleaner"/>가 정의한 동일한
    /// 분류(<see cref="AudioVideoSettingsSnapshot"/>)를 재사용하여 절대 건드리지 않습니다
    /// (단일 정의 원칙: 설정 키가 무엇인지는 오직 <see cref="PlayDataPrefsCleaner"/>만 정의).
    /// </summary>
    public sealed class QaProfileService : IQaProfileService
    {
        /// <summary>
        /// 일반 진행 PlayerPrefs 키의 명시적 카탈로그. PlayerPrefs는 플랫폼 공통 열거(enumeration)
        /// API가 없으므로, 격리·복원 대상 키는 반드시 이 목록으로만 정의됩니다. 코드베이스에
        /// 새 고정 이름 진행 키를 추가하면 이 목록도 함께 갱신해야 합니다(드리프트 방지는
        /// <c>QaProfileServiceTests</c>의 인벤토리 테스트가 문서화합니다).
        /// 알려진 한계: 씬 인스턴스 이름에 의존하는 동적 접두 키(<c>SnapState_*</c>,
        /// <c>Dial_*_Value</c>, <c>LastBookPage_*</c>)는 EditMode에서 열거할 수 없어
        /// 포함하지 않습니다(DONE_WITH_CONCERNS 참고).
        /// </summary>
        public static readonly IReadOnlyList<QaGameplayKeyDefinition> KnownGameplayKeys = new[]
        {
            new QaGameplayKeyDefinition("Checkpoint.Latest.v1", QaPlayerPrefsValueKind.String),
            new QaGameplayKeyDefinition("Checkpoint.LatestId.v1", QaPlayerPrefsValueKind.String),
            new QaGameplayKeyDefinition("SafeLock_Unlocked", QaPlayerPrefsValueKind.Int),
            new QaGameplayKeyDefinition("InventoryAccess.UnlockedAfterHallPlayableRetry", QaPlayerPrefsValueKind.Int),
            new QaGameplayKeyDefinition("InventoryGuide.InventoryOpened", QaPlayerPrefsValueKind.Int),
            new QaGameplayKeyDefinition("LastCalendarMonth", QaPlayerPrefsValueKind.Int),
            new QaGameplayKeyDefinition("PlayLogRecorder.SessionId", QaPlayerPrefsValueKind.String)
        };

        private readonly object sync = new object();
        private readonly IQaProfileMarkerStore markerStore;

        private bool isActive;
        private QaRunId activeRunId = QaRunId.None;
        private List<QaGameplaySnapshotEntry> activeSnapshot;
        private AudioVideoSettingsSnapshot settingsSnapshotAtBegin;
        private QaProfileMarker pendingRecoveryMarker;

        public QaProfileService(IQaProfileMarkerStore markerStore = null)
        {
            this.markerStore = markerStore ?? QaNullProfileMarkerStore.Instance;

            // 생성 시점에 미해소 마커가 있으면(이전 프로세스가 RestorePreviousProfile을 호출하지
            // 못하고 종료된 경우), 이 인스턴스는 절대 조용히 새 프로필을 시작하지 않습니다.
            pendingRecoveryMarker = SafeLoadMarker();
        }

        public bool IsQaProfileActive
        {
            get
            {
                lock (sync)
                {
                    return isActive;
                }
            }
        }

        public QaProfileOperationResult BeginQaProfile(QaRunId runId)
        {
            lock (sync)
            {
                if (runId.IsNone)
                {
                    return QaProfileOperationResult.Invalid("runId must not be QaRunId.None.");
                }

                if (pendingRecoveryMarker != null)
                {
                    return QaProfileOperationResult.RecoveryRequired(
                        "An interrupted QA session must be recovered via RecoverInterruptedSession before a new QA profile can begin.");
                }

                if (isActive)
                {
                    return QaProfileOperationResult.AlreadyActive(
                        "A QA profile is already active. Call RestorePreviousProfile before beginning a new one.");
                }

                List<QaGameplaySnapshotEntry> snapshot = CaptureGameplaySnapshot();
                QaProfileMarker marker = QaProfileMarker.Create(runId.ToString(), snapshot);

                markerStore.Save(marker);

                activeSnapshot = snapshot;
                activeRunId = runId;
                settingsSnapshotAtBegin = PlayDataPrefsCleaner.CaptureAudioVideoSettings();
                isActive = true;

                return QaProfileOperationResult.Success(
                    "QA profile started; normal gameplay progress snapshotted for restoration.");
            }
        }

        public QaProfileOperationResult ResetGameplay()
        {
            lock (sync)
            {
                if (!isActive)
                {
                    return QaProfileOperationResult.NotActive("No active QA profile; call BeginQaProfile first.");
                }

                // 동일한 명시적 분류(오디오/비디오 설정 보존)를 사용하는 기존 클리너를 재사용합니다.
                // 에디터 Fungus 세이브 파일은 QA 프로필 격리 범위 밖이라 삭제하지 않습니다.
                PlayDataPrefsCleaner.ClearProgressPreserveAudioVideoSettings(deleteEditorFungusSaveFiles: false);

                VerifySettingsUntouched("ResetGameplay");

                return QaProfileOperationResult.Success("QA gameplay progress reset; settings preserved.");
            }
        }

        public QaProfileOperationResult RestorePreviousProfile()
        {
            lock (sync)
            {
                if (!isActive)
                {
                    return QaProfileOperationResult.NotActive("No active QA profile to restore from.");
                }

                ApplySnapshot(activeSnapshot);
                VerifySettingsUntouched("RestorePreviousProfile");

                markerStore.Clear();
                activeSnapshot = null;
                activeRunId = QaRunId.None;
                isActive = false;

                return QaProfileOperationResult.Success(
                    "Normal gameplay progress restored; QA profile ended.");
            }
        }

        public QaProfileOperationResult RecoverInterruptedSession()
        {
            lock (sync)
            {
                if (pendingRecoveryMarker == null)
                {
                    return QaProfileOperationResult.NothingToRecover("No interrupted QA session found.");
                }

                ApplySnapshot(pendingRecoveryMarker.Entries);

                markerStore.Clear();
                pendingRecoveryMarker = null;
                activeSnapshot = null;
                activeRunId = QaRunId.None;
                isActive = false;

                return QaProfileOperationResult.Recovered(
                    "Interrupted QA session recovered; normal gameplay progress restored and normal profile selected.");
            }
        }

        private static List<QaGameplaySnapshotEntry> CaptureGameplaySnapshot()
        {
            var entries = new List<QaGameplaySnapshotEntry>(KnownGameplayKeys.Count);

            foreach (QaGameplayKeyDefinition definition in KnownGameplayKeys)
            {
                bool existed = PlayerPrefs.HasKey(definition.Key);
                string rawValue = null;

                if (existed)
                {
                    rawValue = definition.Kind == QaPlayerPrefsValueKind.Int
                        ? PlayerPrefs.GetInt(definition.Key).ToString(CultureInfo.InvariantCulture)
                        : PlayerPrefs.GetString(definition.Key);
                }

                entries.Add(new QaGameplaySnapshotEntry(definition.Key, definition.Kind, existed, rawValue));
            }

            return entries;
        }

        private static void ApplySnapshot(IReadOnlyList<QaGameplaySnapshotEntry> entries)
        {
            if (entries == null)
            {
                return;
            }

            foreach (QaGameplaySnapshotEntry entry in entries)
            {
                if (!entry.Existed)
                {
                    PlayerPrefs.DeleteKey(entry.Key);
                    continue;
                }

                if (entry.Kind == QaPlayerPrefsValueKind.Int)
                {
                    int parsed = int.TryParse(entry.RawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                        ? value
                        : 0;
                    PlayerPrefs.SetInt(entry.Key, parsed);
                }
                else
                {
                    PlayerPrefs.SetString(entry.Key, entry.RawValue ?? string.Empty);
                }
            }

            PlayerPrefs.Save();
        }

        /// <summary>
        /// Fail-Safe 방어 점검: 복원 연산 전후로 오디오/비디오 설정이 우연히도 바뀌지 않았는지
        /// 확인합니다. 값이 달라도 예외를 던지지 않고 경고만 남깁니다 — QA 인프라의 방어적
        /// 점검 실패가 실제 플레이어 세션을 절대 무너뜨리면 안 되기 때문입니다.
        /// </summary>
        private void VerifySettingsUntouched(string context)
        {
            AudioVideoSettingsSnapshot current = PlayDataPrefsCleaner.CaptureAudioVideoSettings();
            if (!AudioVideoSettingsSnapshot.AreEqual(settingsSnapshotAtBegin, current))
            {
                Debug.LogWarning(
                    "[QaProfileService] Audio/video settings changed during " + context +
                    "; QA profiles must never mutate settings keys.");
            }
        }

        private QaProfileMarker SafeLoadMarker()
        {
            try
            {
                return markerStore.Load();
            }
            catch (Exception)
            {
                // Fail-safe: a broken store must never crash service construction.
                return null;
            }
        }
    }
}
#endif
