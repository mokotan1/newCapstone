# fungus-deletion

## 현재 실행 기준

- 최종 범위: 전체 씬·공용 자산의 Fungus 제거. Kitchen은 후보일 뿐.
- 브랜치: `feature/fungus-deletion-framework` (base `develop` / `e1de9505`)
- 현재 단계: **P2 세이브 계약**. Unity EditMode는 이 Linux checkout에서 실행 불가 (Editor 없음).
- 다음: Windows에서 `unity-cli status` ready 후 `--filter FlagStoreSnapshotTests` / `FlagStoreCheckpointMapperTests` / `CheckpointRepositoryTests`. 씬 일괄 변경 금지. `RoomUnlockCheckpointService`에 FlagStore 싱글톤을 넣지 말 것.

## 동결

새 Fungus 블록, `Assets/Fungus/` 패치, `*SceneMigrator`, `FungusDialogueQaAdapter` 확장 금지. FlagStore와 Variablemanager 이중 기록 금지.

## 인계

| 필드 | 내용 |
|------|------|
| runner | Cursor 부모 (cloud) |
| modelId | Cursor Grok 4.6 |
| checkedAt | 2026-09-17 |
| Editor | 없음. unity-cli blocked |
| independentReview | false |
| 다음 명령 | `.\scripts\unity-cli.cmd --project disputatio test --mode EditMode --filter FlagStoreSnapshotTests` |

## P2 AC

- FlagStore `Export`/`Import` 라운드트립. Import는 검증 후 통째 교체
- 빈 키·중복·타입 충돌은 `SequencePlayException`, FlagStore 불변
- `FlagStoreCheckpointMapper`는 `sequence*`만 기록. `fungus*`·Variablemanager 미사용
- 정책 키(`isClicked`, 설정 등)는 캡처·복원에서 제외

## 검증 (이 checkout)

- `/tmp/p2-save-contract-tests` NUnit: 구현 전 11 fail / 20 pass, 구현 후 31 pass / 0 fail.
- `dotnet run --project scripts/CSharpSyntaxChecker -- disputatio/Assets`: exit 0
- Unity EditMode: 미실행 (tool-missing)
