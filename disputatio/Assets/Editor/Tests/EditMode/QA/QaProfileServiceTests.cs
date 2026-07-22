using System;
using System.Collections.Generic;
using System.IO;
using Godlotto.QA.Core;
using Godlotto.QA.Profile;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// <see cref="QaProfileService"/>가 강제하는 격리 보장(일반 진행 PlayerPrefs 키는 QA 실행
/// 전후로 byte-for-byte 동일해야 하고, 오디오/비디오 설정은 QA가 절대 건드리지 않아야 함)과
/// 크래시 이후 중단된 세션 복구를 검증합니다.
/// </summary>
[TestFixture]
public class QaProfileServiceTests
{
    // ---------------------------------------------------------------------------------------
    // Step 1: 이 스위트가 지키는 세이브 경계 인벤토리.
    //
    // 1) PlayerPrefs 설정 키 (QA 중 절대 변형 금지, SettingPlayerPrefsKeys):
    //    BGMVolume, SFXVolume, Fullscreen, ResolutionIndex.
    // 2) PlayerPrefs 고정 이름 진행 키 (QaProfileService.KnownGameplayKeys — QA 종료/복구 시
    //    byte-for-byte 복원 대상):
    //    - Checkpoint.Latest.v1 / Checkpoint.LatestId.v1 (CheckpointRepository)
    //    - SafeLock_Unlocked (UISafeLockController)
    //    - InventoryAccess.UnlockedAfterHallPlayableRetry (InventoryAccessState)
    //    - InventoryGuide.InventoryOpened (InventoryAccessState / InventoryGuideController)
    //    - LastCalendarMonth (CalendarController)
    //    - PlayLogRecorder.SessionId (PlayLogRecorder)
    // 3) PlayerPrefs 동적 접두 진행 키 (씬 인스턴스 이름에 의존 — PlayerPrefs에 열거 API가
    //    없어 EditMode에서 다룰 수 없는 알려진 한계. DONE_WITH_CONCERNS 참고):
    //    SnapState_<scene>_<object> (DraggableSnap2D), Dial_<object>_Value (UIDialRotator),
    //    LastBookPage_<object> (BookPanelController).
    // 4) Fungus Variablemanager 전역 변수 / InventoryManager(DontDestroyOnLoad) 런타임 상태 —
    //    씬 로드가 필요해 EditMode로 검증 불가(PlayMode 필요, DONE_WITH_CONCERNS).
    // 5) Fungus SaveManager 파일(persistentDataPath/FungusSaves) — QaProfileService는
    //    건드리지 않음(ResetGameplay가 PlayDataPrefsCleaner를 deleteEditorFungusSaveFiles:false로 재사용).
    // ---------------------------------------------------------------------------------------

    private static readonly string[] ExpectedKnownGameplayKeys =
    {
        "Checkpoint.Latest.v1",
        "Checkpoint.LatestId.v1",
        "SafeLock_Unlocked",
        "InventoryAccess.UnlockedAfterHallPlayableRetry",
        "InventoryGuide.InventoryOpened",
        "LastCalendarMonth",
        "PlayLogRecorder.SessionId"
    };

    private const string CheckpointLatestKey = "Checkpoint.Latest.v1";
    private const string CheckpointLatestIdKey = "Checkpoint.LatestId.v1";
    private const string SafeLockUnlockedKey = "SafeLock_Unlocked";
    private const string InventoryAccessUnlockedKey = "InventoryAccess.UnlockedAfterHallPlayableRetry";
    private const string InventoryGuideOpenedKey = "InventoryGuide.InventoryOpened";
    private const string LastCalendarMonthKey = "LastCalendarMonth";
    private const string PlayLogSessionIdKey = "PlayLogRecorder.SessionId";

    private static readonly string[] AllTouchedKeysForCleanup =
    {
        CheckpointLatestKey,
        CheckpointLatestIdKey,
        SafeLockUnlockedKey,
        InventoryAccessUnlockedKey,
        InventoryGuideOpenedKey,
        LastCalendarMonthKey,
        PlayLogSessionIdKey,
        SettingPlayerPrefsKeys.BgmVolume,
        SettingPlayerPrefsKeys.SfxVolume,
        SettingPlayerPrefsKeys.Fullscreen,
        SettingPlayerPrefsKeys.ResolutionIndex,
        "__QaProfileServiceTests_NeverExistedKey__"
    };

    [TearDown]
    public void TearDown()
    {
        foreach (string key in AllTouchedKeysForCleanup)
        {
            PlayerPrefs.DeleteKey(key);
        }

        PlayerPrefs.Save();
    }

    // -----------------------------------------------------------------------
    //  Step 1 (continued): inventory assertion
    // -----------------------------------------------------------------------

    [Test]
    public void KnownGameplayKeys_MatchesDocumentedInventory()
    {
        var actual = new List<string>();
        foreach (QaGameplayKeyDefinition definition in QaProfileService.KnownGameplayKeys)
        {
            actual.Add(definition.Key);
        }

        CollectionAssert.AreEquivalent(ExpectedKnownGameplayKeys, actual,
            "KnownGameplayKeys catalog drifted from the documented save-boundary inventory.");
    }

    // -----------------------------------------------------------------------
    //  Step 2: isolation - normal progress & settings survive a full QA cycle
    // -----------------------------------------------------------------------

    [Test]
    public void FullQaProfileCycle_LeavesNormalGameplayKeysByteForByteUnchanged()
    {
        const string originalCheckpointJson = "{\"resumeSceneName\":\"Kitchen\",\"checkpointId\":\"chk-original\"}";
        SeedNormalProgress(originalCheckpointJson, "chk-original", unlocked: 1, guideOpened: 1, month: 5, sessionId: "session-original");
        SeedSettings(bgm: 0.33f, sfx: 0.61f, fullscreen: 1, resolutionIndex: 5);

        var store = new FakeProfileMarkerStore();
        var service = new QaProfileService(store);

        QaProfileOperationResult begin = service.BeginQaProfile(QaRunId.NewId());
        Assert.IsTrue(begin.IsSuccess, begin.Message);

        // Simulate QA mutating gameplay progress while the profile is active.
        PlayerPrefs.SetString(CheckpointLatestKey, "{\"resumeSceneName\":\"QA_Mutated\"}");
        PlayerPrefs.SetString(CheckpointLatestIdKey, "chk-mutated-by-qa");
        PlayerPrefs.SetInt(SafeLockUnlockedKey, 0);
        PlayerPrefs.SetInt(LastCalendarMonthKey, 1);
        PlayerPrefs.SetString(PlayLogSessionIdKey, "session-during-qa");
        PlayerPrefs.Save();

        QaProfileOperationResult resetResult = service.ResetGameplay();
        Assert.IsTrue(resetResult.IsSuccess, resetResult.Message);

        QaProfileOperationResult end = service.RestorePreviousProfile();
        Assert.IsTrue(end.IsSuccess, end.Message);

        Assert.AreEqual(originalCheckpointJson, PlayerPrefs.GetString(CheckpointLatestKey));
        Assert.AreEqual("chk-original", PlayerPrefs.GetString(CheckpointLatestIdKey));
        Assert.AreEqual(1, PlayerPrefs.GetInt(SafeLockUnlockedKey));
        Assert.AreEqual(1, PlayerPrefs.GetInt(InventoryAccessUnlockedKey));
        Assert.AreEqual(1, PlayerPrefs.GetInt(InventoryGuideOpenedKey));
        Assert.AreEqual(5, PlayerPrefs.GetInt(LastCalendarMonthKey));
        Assert.AreEqual("session-original", PlayerPrefs.GetString(PlayLogSessionIdKey));

        AssertSettingsUnchanged(bgm: 0.33f, sfx: 0.61f, fullscreen: 1, resolutionIndex: 5);
        Assert.IsFalse(service.IsQaProfileActive);
    }

    [Test]
    public void RestorePreviousProfile_KeyThatDidNotExistBeforeQa_IsDeletedNotZeroed()
    {
        // No SafeLock_Unlocked seeded: it never existed before QA began.
        SeedSettings(bgm: 0.5f, sfx: 0.5f, fullscreen: 0, resolutionIndex: 2);

        var service = new QaProfileService(new FakeProfileMarkerStore());
        service.BeginQaProfile(QaRunId.NewId());

        // QA creates the key mid-run.
        PlayerPrefs.SetInt(SafeLockUnlockedKey, 1);
        PlayerPrefs.Save();
        Assert.IsTrue(PlayerPrefs.HasKey(SafeLockUnlockedKey));

        service.RestorePreviousProfile();

        Assert.IsFalse(PlayerPrefs.HasKey(SafeLockUnlockedKey),
            "A gameplay key that did not exist before QA must be removed on restore, not left at a default value.");
    }

    [Test]
    public void ResetGameplay_NeverMutatesAudioVideoSettings()
    {
        SeedNormalProgress("{\"resumeSceneName\":\"Study\"}", "chk-a", 1, 1, 3, "session-a");
        SeedSettings(bgm: 0.2f, sfx: 0.8f, fullscreen: 1, resolutionIndex: 1);

        var service = new QaProfileService(new FakeProfileMarkerStore());
        service.BeginQaProfile(QaRunId.NewId());

        QaProfileOperationResult result = service.ResetGameplay();

        Assert.IsTrue(result.IsSuccess);
        AssertSettingsUnchanged(bgm: 0.2f, sfx: 0.8f, fullscreen: 1, resolutionIndex: 1);
        Assert.IsFalse(PlayerPrefs.HasKey(CheckpointLatestKey), "ResetGameplay must clear progress keys.");
    }

    // -----------------------------------------------------------------------
    //  Guard rails: invalid requests, double-begin, operations without an active profile
    // -----------------------------------------------------------------------

    [Test]
    public void BeginQaProfile_WithNoneRunId_ReturnsInvalidRequest()
    {
        var service = new QaProfileService(new FakeProfileMarkerStore());

        QaProfileOperationResult result = service.BeginQaProfile(QaRunId.None);

        Assert.AreEqual(QaProfileOperationCode.InvalidRequest, result.Code);
        Assert.IsFalse(service.IsQaProfileActive);
    }

    [Test]
    public void BeginQaProfile_CalledTwice_ReturnsAlreadyActive_AndPreservesOriginalSnapshot()
    {
        SeedNormalProgress("{\"resumeSceneName\":\"Kitchen\"}", "chk-1", 1, 1, 4, "session-1");

        var service = new QaProfileService(new FakeProfileMarkerStore());
        QaProfileOperationResult first = service.BeginQaProfile(QaRunId.NewId());
        Assert.IsTrue(first.IsSuccess);

        // Mutate before the (rejected) second Begin attempt.
        PlayerPrefs.SetInt(LastCalendarMonthKey, 11);
        PlayerPrefs.Save();

        QaProfileOperationResult second = service.BeginQaProfile(QaRunId.NewId());
        Assert.AreEqual(QaProfileOperationCode.AlreadyActive, second.Code);

        service.RestorePreviousProfile();

        Assert.AreEqual(4, PlayerPrefs.GetInt(LastCalendarMonthKey),
            "A denied second BeginQaProfile call must not overwrite the original snapshot.");
    }

    [Test]
    public void ResetGameplay_WithNoActiveProfile_ReturnsNotActiveWithoutThrowing()
    {
        var service = new QaProfileService(new FakeProfileMarkerStore());

        QaProfileOperationResult result = null;
        Assert.DoesNotThrow(() => result = service.ResetGameplay());

        Assert.AreEqual(QaProfileOperationCode.NotActive, result.Code);
    }

    [Test]
    public void RestorePreviousProfile_WithNoActiveProfile_ReturnsNotActiveWithoutThrowing()
    {
        var service = new QaProfileService(new FakeProfileMarkerStore());

        QaProfileOperationResult result = null;
        Assert.DoesNotThrow(() => result = service.RestorePreviousProfile());

        Assert.AreEqual(QaProfileOperationCode.NotActive, result.Code);
    }

    // -----------------------------------------------------------------------
    //  Step 4: interrupted-session (crash) recovery
    // -----------------------------------------------------------------------

    [Test]
    public void RecoverInterruptedSession_WithPersistedMarkerFromCrash_RestoresNormalProgressAndSelectsNormalProfile()
    {
        const string originalCheckpointJson = "{\"resumeSceneName\":\"Kitchen\",\"checkpointId\":\"chk-before-crash\"}";
        SeedNormalProgress(originalCheckpointJson, "chk-before-crash", 1, 0, 6, "session-before-crash");
        SeedSettings(bgm: 0.44f, sfx: 0.77f, fullscreen: 0, resolutionIndex: 4);

        var store = new FakeProfileMarkerStore();
        var crashedProcess = new QaProfileService(store);
        QaProfileOperationResult begin = crashedProcess.BeginQaProfile(QaRunId.NewId());
        Assert.IsTrue(begin.IsSuccess);

        // Simulate QA mutating progress, then the process crashing before RestorePreviousProfile.
        PlayerPrefs.SetString(CheckpointLatestKey, "{\"resumeSceneName\":\"QA_Crashed_Mid_Mutation\"}");
        PlayerPrefs.SetInt(LastCalendarMonthKey, 12);
        PlayerPrefs.Save();
        // crashedProcess is abandoned here without calling RestorePreviousProfile (simulated crash).

        // Simulate an app restart: a brand-new QaProfileService backed by the same persisted store.
        var recovered = new QaProfileService(store);

        QaProfileOperationResult result = recovered.RecoverInterruptedSession();

        Assert.AreEqual(QaProfileOperationCode.Recovered, result.Code);
        Assert.IsFalse(recovered.IsQaProfileActive, "The normal profile must be selected after recovery.");

        Assert.AreEqual(originalCheckpointJson, PlayerPrefs.GetString(CheckpointLatestKey));
        Assert.AreEqual("chk-before-crash", PlayerPrefs.GetString(CheckpointLatestIdKey));
        Assert.AreEqual(6, PlayerPrefs.GetInt(LastCalendarMonthKey));
        Assert.AreEqual(0, PlayerPrefs.GetInt(InventoryGuideOpenedKey));
        Assert.AreEqual("session-before-crash", PlayerPrefs.GetString(PlayLogSessionIdKey));

        AssertSettingsUnchanged(bgm: 0.44f, sfx: 0.77f, fullscreen: 0, resolutionIndex: 4);
        Assert.IsNull(store.SavedMarker, "The recovery marker must be cleared once recovered.");
    }

    [Test]
    public void RecoverInterruptedSession_WithNoMarker_ReturnsNothingToRecover_AndIsIdempotent()
    {
        var service = new QaProfileService(new FakeProfileMarkerStore());

        QaProfileOperationResult first = service.RecoverInterruptedSession();
        QaProfileOperationResult second = service.RecoverInterruptedSession();

        Assert.AreEqual(QaProfileOperationCode.NothingToRecover, first.Code);
        Assert.AreEqual(QaProfileOperationCode.NothingToRecover, second.Code);
        Assert.IsFalse(service.IsQaProfileActive);
    }

    [Test]
    public void BeginQaProfile_WithUnresolvedMarkerFromPriorProcess_ReturnsRecoveryRequired()
    {
        var store = new FakeProfileMarkerStore
        {
            SavedMarker = QaProfileMarker.Create("stale-run", new List<QaGameplaySnapshotEntry>())
        };

        var service = new QaProfileService(store);

        QaProfileOperationResult result = service.BeginQaProfile(QaRunId.NewId());

        Assert.AreEqual(QaProfileOperationCode.RecoveryRequired, result.Code);
        Assert.IsFalse(service.IsQaProfileActive);
    }

    // -----------------------------------------------------------------------
    //  QaFileProfileMarkerStore - round trip through the real file system
    // -----------------------------------------------------------------------

    [Test]
    public void QaFileProfileMarkerStore_SaveThenLoad_RoundTripsSpecialCharactersExactly()
    {
        string tempPath = Path.Combine(Path.GetTempPath(), "qa-profile-marker-test-" + Guid.NewGuid().ToString("N") + ".marker");
        var store = new QaFileProfileMarkerStore(tempPath);

        try
        {
            const string trickyJson = "{\"a\":\"b|c\",\"unicode\":\"한글\\n줄바꿈\"}";
            var entries = new List<QaGameplaySnapshotEntry>
            {
                new QaGameplaySnapshotEntry("Checkpoint.Latest.v1", QaPlayerPrefsValueKind.String, true, trickyJson),
                new QaGameplaySnapshotEntry("SafeLock_Unlocked", QaPlayerPrefsValueKind.Int, true, "1"),
                new QaGameplaySnapshotEntry("LastCalendarMonth", QaPlayerPrefsValueKind.Int, false, null)
            };
            QaProfileMarker marker = QaProfileMarker.Create("run-xyz", entries);

            store.Save(marker);
            QaProfileMarker loaded = store.Load();

            Assert.IsNotNull(loaded);
            Assert.AreEqual("run-xyz", loaded.RunId);
            Assert.AreEqual(3, loaded.Entries.Count);

            QaGameplaySnapshotEntry checkpointEntry = FindEntry(loaded.Entries, "Checkpoint.Latest.v1");
            Assert.IsTrue(checkpointEntry.Existed);
            Assert.AreEqual(trickyJson, checkpointEntry.RawValue);

            QaGameplaySnapshotEntry missingEntry = FindEntry(loaded.Entries, "LastCalendarMonth");
            Assert.IsFalse(missingEntry.Existed);
            Assert.IsNull(missingEntry.RawValue);
        }
        finally
        {
            store.Clear();
            Assert.IsFalse(File.Exists(tempPath));
        }
    }

    [Test]
    public void QaFileProfileMarkerStore_Load_WithNoFile_ReturnsNullWithoutThrowing()
    {
        string tempPath = Path.Combine(Path.GetTempPath(), "qa-profile-marker-missing-" + Guid.NewGuid().ToString("N") + ".marker");
        var store = new QaFileProfileMarkerStore(tempPath);

        QaProfileMarker result = null;
        Assert.DoesNotThrow(() => result = store.Load());
        Assert.IsNull(result);
    }

    // -----------------------------------------------------------------------
    //  Test helpers
    // -----------------------------------------------------------------------

    private static void SeedNormalProgress(
        string checkpointJson,
        string checkpointId,
        int unlocked,
        int guideOpened,
        int month,
        string sessionId)
    {
        PlayerPrefs.SetString(CheckpointLatestKey, checkpointJson);
        PlayerPrefs.SetString(CheckpointLatestIdKey, checkpointId);
        PlayerPrefs.SetInt(SafeLockUnlockedKey, unlocked);
        PlayerPrefs.SetInt(InventoryAccessUnlockedKey, unlocked);
        PlayerPrefs.SetInt(InventoryGuideOpenedKey, guideOpened);
        PlayerPrefs.SetInt(LastCalendarMonthKey, month);
        PlayerPrefs.SetString(PlayLogSessionIdKey, sessionId);
        PlayerPrefs.Save();
    }

    private static void SeedSettings(float bgm, float sfx, int fullscreen, int resolutionIndex)
    {
        PlayerPrefs.SetFloat(SettingPlayerPrefsKeys.BgmVolume, bgm);
        PlayerPrefs.SetFloat(SettingPlayerPrefsKeys.SfxVolume, sfx);
        PlayerPrefs.SetInt(SettingPlayerPrefsKeys.Fullscreen, fullscreen);
        PlayerPrefs.SetInt(SettingPlayerPrefsKeys.ResolutionIndex, resolutionIndex);
        PlayerPrefs.Save();
    }

    private static void AssertSettingsUnchanged(float bgm, float sfx, int fullscreen, int resolutionIndex)
    {
        Assert.That(PlayerPrefs.GetFloat(SettingPlayerPrefsKeys.BgmVolume), Is.EqualTo(bgm).Within(0.0001f));
        Assert.That(PlayerPrefs.GetFloat(SettingPlayerPrefsKeys.SfxVolume), Is.EqualTo(sfx).Within(0.0001f));
        Assert.AreEqual(fullscreen, PlayerPrefs.GetInt(SettingPlayerPrefsKeys.Fullscreen));
        Assert.AreEqual(resolutionIndex, PlayerPrefs.GetInt(SettingPlayerPrefsKeys.ResolutionIndex));
    }

    private static QaGameplaySnapshotEntry FindEntry(IReadOnlyList<QaGameplaySnapshotEntry> entries, string key)
    {
        foreach (QaGameplaySnapshotEntry entry in entries)
        {
            if (entry.Key == key)
            {
                return entry;
            }
        }

        return null;
    }

    /// <summary>
    /// 인메모리 페이크 저장소. 실제 파일 시스템을 건드리지 않고 <see cref="QaProfileService"/>의
    /// 영속화 계약(크래시 복구용 마커 저장/로드/삭제)을 검증할 수 있게 합니다.
    /// </summary>
    private sealed class FakeProfileMarkerStore : IQaProfileMarkerStore
    {
        public QaProfileMarker SavedMarker { get; set; }

        public QaProfileMarker Load()
        {
            return SavedMarker;
        }

        public void Save(QaProfileMarker marker)
        {
            SavedMarker = marker;
        }

        public void Clear()
        {
            SavedMarker = null;
        }
    }
}
