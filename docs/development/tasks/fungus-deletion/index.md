# fungus-deletion

## 현재 실행 기준

- 최종 범위: 전체 씬·공용 자산의 Fungus 제거. Kitchen은 후보일 뿐.
- 브랜치: `feature/fungus-deletion-framework` (base `develop`)
- 현재 단계: **P3 비동기·UI 계약**. Unity EditMode는 이 Linux checkout에서 실행 불가.
- 다음: Windows에서 `--filter SequenceSessionTests`. 씬 일괄 변경 금지. FlagStore 싱글톤 금지.

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
| 다음 명령 | `.\scripts\unity-cli.cmd --project disputatio test --mode EditMode --filter SequenceSessionTests` |

## P3 AC

- `wait`(ms) / `say`는 `ISequenceHost`만 호출. Thread.Sleep 없음. FlagStore에 쓰지 않음
- `SequenceSession`은 문서를 먼저 검증한 뒤 입력을 잠그고, 예외가 나도 해제
- 호스트 없는 `wait`/`say`는 `async_required`, 플래그 불변
- Unity 입력은 `SequenceInputGateLock` → `InteractionInputGate`. Sequence 폴더는 Interaction을 참조하지 않음

## 검증 (이 checkout)

- `/tmp/p3-async-ui-tests` NUnit: 구현 전 7 fail / 31 pass, 구현 후 38 pass / 0 fail.
- `dotnet run --project scripts/CSharpSyntaxChecker -- disputatio/Assets`: exit 0
- Unity EditMode: 미실행 (tool-missing)
