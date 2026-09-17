# fungus-deletion

## 현재 실행 기준

- 최종 범위: 전체 씬·공용 자산의 Fungus 제거. Kitchen은 후보일 뿐.
- 브랜치: `feature/fungus-deletion-framework` (base `develop` / `e1de9505`)
- 현재 단계: **P1 Sequence 계약 구현**. Unity EditMode는 이 Linux checkout에서 실행 불가 (Editor 없음).
- 다음: Windows에서 `unity-cli status` ready 후 `--filter FlagStoreTests` / `SequencePlayerTests`. 씬 일괄 변경 금지.

## 동결

새 Fungus 블록, `Assets/Fungus/` 패치, `*SceneMigrator`, `FungusDialogueQaAdapter` 확장 금지.

## 인계

| 필드 | 내용 |
|------|------|
| runner | Cursor 부모 (cloud) |
| modelId | Cursor Grok 4.6 |
| checkedAt | 2026-09-17 |
| Editor | 없음. unity-cli blocked |
| independentReview | false |
| 다음 명령 | `.\scripts\unity-cli.cmd --project disputatio test --mode EditMode --filter FlagStoreTests` |

## P1 AC

- 동일 키 타입 충돌 / 잘못된 읽기 / 공백 키는 `SequencePlayException`
- Play 전 문서 전체 검증. 실패 시 FlagStore 불변
- 허용 명령: `set_bool`, `set_int`, `set_string`, `if_bool`

## 검증 (이 checkout)

- `/tmp/sequence-contract-tests` xunit: Sequence 소스 직접 컴파일. 구현 전 6 fail / 1 pass, 구현 후 전 pass.
- Unity EditMode: 미실행 (tool-missing)
