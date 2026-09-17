# fungus-deletion

## 현재 실행 기준

- 최종 범위: 전체 씬·공용 자산의 Fungus 제거. Kitchen은 후보일 뿐.
- 브랜치: `feature/fungus-deletion-framework` (base `develop`)
- **마스터 계획**: [MASTER-PLAN.md](./MASTER-PLAN.md)
- cloud 완료: **P0–P7**. **R1** BasementHallway 파일럿(코드·JSON·Editor 메뉴)까지 push.
- 다음: Unity에서 **Basement Hallway Sequence Pilot** 실행 → PlayMode로 문 5개 씬 전환 확인. `unity-cli EditMode --filter Sequence`

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
| 다음 명령 | `.\scripts\unity-cli.cmd --project disputatio test --mode EditMode --filter RoomInteractionSequenceControllerTests` |

## P4 AC

- `SequenceCatalog.Register`는 문서를 검사한 뒤 interactionId를 저장. 잘못된 문서·중복 id는 등록하지 않음
- `SequenceRouter.Play`는 등록된 문서만 `SequenceSession`으로 실행. 없는 id는 `unknown_route`, 입력 미잠금
- Fungus 블록을 호출하지 않음

## 검증 (이 checkout)

- `/tmp/p4-routing-tests` NUnit: 구현 전 6 fail / 38 pass, 구현 후 44 pass / 0 fail.
- `dotnet run --project scripts/CSharpSyntaxChecker -- disputatio/Assets`: exit 0
- Unity EditMode: 미실행 (tool-missing)
