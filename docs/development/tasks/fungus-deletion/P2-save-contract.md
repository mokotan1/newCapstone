# P2-save-contract / FlagStore 스냅샷과 Checkpoint 분리 기록

- state: done (Unity EditMode 남음)
- phase: P2 (R1)
- allowedFiles: `disputatio/Assets/godlotto/Script/Sequence/**`, `disputatio/Assets/godlotto/Script/Checkpoint/CheckpointSaveData.cs`, `CheckpointRepository.cs`, `FlagStoreCheckpointMapper.cs`, `disputatio/Assets/Editor/Tests/EditMode/Sequence/**`, `disputatio/Assets/Editor/Tests/EditMode/Checkpoint/FlagStoreCheckpointMapperTests.cs`, `CheckpointRepositoryTests.cs`, `docs/architecture.md`, 본 task 폴더
- excluded: 씬/프리팹, Variablemanager 이중 기록, RoomUnlockCheckpointService에 FlagStore 싱글톤, Kitchen Flowchart 제거, 벤더 패치

## 실행

- [x] AC 확정
- [x] 실패 테스트 (빈 Export, no-op Import, fungus 배열 미기록)
- [x] Export/Import 검증 후 교체 + Mapper
- [ ] Unity EditMode `--filter FlagStoreSnapshotTests` / `FlagStoreCheckpointMapperTests` / `CheckpointRepositoryTests` (Editor 없음)
- [x] independentReview: false

## 증거

- RED: standalone NUnit 11 failed / 20 passed (empty Export, no-op Import, no exception)
- GREEN: standalone NUnit 31 passed / 0 failed. Unity 미실행 (Editor 없음)
- CSharpSyntaxChecker: exit 0. CSharpSyntaxChecker.Tests: 3 passed
