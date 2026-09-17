# TODO — Fungus 삭제 P2

## 목표
FlagStore 스냅샷을 Checkpoint `sequence*`에 저장·복원. Fungus 배열과 이중 기록 금지.

## 진행 중인 항목
- P2 세이브 계약. Unity EditMode는 이 환경에서 미실행.

## 남은 항목
- Windows unity-cli EditMode: FlagStoreSnapshotTests, FlagStoreCheckpointMapperTests, CheckpointRepositoryTests
- P3 async/UI
- 씬 이전. FlagStore 싱글톤 금지

## 검증 결과
- standalone NUnit `/tmp/p2-save-contract-tests`: RED 11 fail/20 pass → GREEN 31 pass
- CSharpSyntaxChecker: exit 0. Tests 3 pass
- Unity compile/EditMode: blocked (no Editor)
- independentReview: false
