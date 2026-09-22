# Fungus 삭제 — C# 시퀀스 런타임 설계

> **2026-09-17 실행 기준 갱신:** 사용자 목표는 Kitchen 단독 이전이 아니라 **전체 씬과 공용 자산의 Fungus 완전 제거 및 프레임워크 재정립**이다. [전체 실행 계획](../plans/2026-09-17-fungus-deletion-framework-master-plan.md)의 범위·책임 분리·게이트·완료조건을 우선 적용한다. 본 문서의 단계 1은 선행 실험 S1 기록으로 보존하며, 다음 실행은 전체 계획 P0 inventory부터 시작한다. 게임 규칙의 소유자는 C# 도메인 로직이고 Sequence는 제한된 연출 실행을 맡는다.


작성일: 2026-09-17
상태: 채택 (접근 A). 구현은 단계별로 별도 계획.
공유: Astra·다음 세션은 `docs/development/tasks/fungus-deletion/index.md`를 먼저 읽는다.

## 1. 목적

Fungus Flowchart를 게임 규칙의 소스로 쓰지 않는다. 클릭·플래그·대사·전환을 C# 시퀀스 런타임이 소유하고, 마지막에 `Assets/Fungus/`와 `Assets/FungusExamples/`를 삭제한다.

성공은 마이그레이터 메뉴가 늘어나는 것이 아니다. 성공은 **패키지를 지워도 게임이 컴파일되고 플레이되는 것**이다.

## 2. 왜 지금 하이브리드를 고치지 않는가

- 플레이 씬 Flowchart 약 56개, 2026-06 감사 기준 블록 약 399개.
- 게임 코드 `using Fungus`가 godlotto·mokotan에 약 100파일.
- 기존 `*SceneMigrator` / `GlassMenuMigrator`는 클릭을 C#으로 옮기면서 **그래프를 씬에 남겼다.** 이중 주인(C# + Flowchart)이 QA·버그픽스의 회귀 원인이다.
- Fungus QA 어댑터는 그래프·`Continue()`·SayDialog 해킹을 관측한다. 통과해도 게임이 안정하다는 뜻이 아니다.

## 3. 채택 접근 (A)

기능·Fungus 벤더 패치·Fungus QA 확장을 동결한다. `ScenarioScript` JSON/CSV 패턴을 **Fungus `Command` 껍질 없이** 실행하는 `SequencePlayer`를 만든다. Kitchen으로 삭제가 가능한지 증명한 뒤 나머지 방을 옮기고 패키지를 지운다.

Yarn/Ink를 들이지 않는다. 상주 `*SceneMigrator`를 만들지 않는다. 대량 텍스트는 일회용 덤프 스크립트만 허용하며, 그 스크립트는 런타임이 Fungus를 호출하지 않게 된 뒤 제거한다.

### 하지 않는 접근

- B: YAML Flowchart를 JSON으로 자동 변환한 뒤 스위치 — 의미 손실이 컴파일에 안 보인다.
- C: 현재 하이브리드에 QA만 강화 — 회귀만 늘어난다.

## 4. 런타임 경계

```
월드/UI 클릭
  → SceneInteractionController.TryInteract
  → SequencePlayer.Play(document, blockId)
       set_bool / set_int / set_string / if_bool
       talk / menu / wait / fade / load_scene / open_panel / sfx / add_item / quest
  → FlagStore
  → DialogueView (Standing / GlassMenu UI. Fungus.Command 아님)
  → SceneTransitionService
```

규칙:

1. 진입은 C#만. 버튼·콜라이더·드롭존이 `ExecuteBlock`을 부르지 않는다.
2. 플래그는 `FlagStore`만. 증명 방부터 `Variablemanager` 이중 기록을 끊는다.
3. locale 권한은 Fungus `SetLanguage`가 아니다. `CheshireLocaleResolver`가 읽는 저장소를 PlayerPrefs/설정으로 옮긴다 (단계 2 이후).
4. 시퀀스는 데이터(JSON). 알 수 없는 `command`는 건너뛰지 않고 실패한다.
5. QA는 `SequencePlayer` 공개 API를 친다. `FungusDialogueQaAdapter`를 확장하지 않는다.

`Godlotto.Sequence`는 `using Fungus`를 하지 않는다.

## 5. 포트 / 폐기

| 가져온다 | 버린다 |
|----------|--------|
| Say, GlassMenu, Wait, Fade | Flowchart 에디터, Lua |
| If / SetVariable → FlagStore | Fungus Save Point (Checkpoint가 대체) |
| LoadScene / 패널 SetActive | Clickable2D 이벤트 그래프를 게임 규칙으로 쓰는 것 |
| 커스텀: 인벤토리, 등록 SFX, 블룸, 퀘스트 완료 | `FungusExamples` |
| Cheshire가 쓰는 대화창 비주얼 | 벤더 `Continue()` 패치 |

## 6. 단계와 일정 (1인 집중, 기능 동결 전제)

제출 하드 데드라인은 이 문서 작성 시점에 없다. 삭제가 목표면 **8~14주**.

| 단계 | 산출 | 기간 | 계획 파일 |
|------|------|------|-----------|
| 0 | 동결 (아래 §7) | 즉시 | 본 스펙 |
| 1 | FlagStore + SequencePlayer (동기 플래그/분기) | 1.5~3주 중 첫 슬라이스 | `docs/superpowers/plans/2026-09-17-sequence-runtime-phase1.md` |
| 2 | Kitchen을 Fungus 없이 클리어 | 1~2주 | 단계 2 계획 (단계 1 완료 후) |
| 3 | 나머지 방 JSON/C# | 4~8주 | 단계 3 계획 |
| 4 | `Assets/Fungus` 삭제, 참조 0, QA 재부착 | 1~2주 | 단계 4 계획 |

단계 2가 끝나기 전에 56개 씬을 한꺼번에 옮기지 않는다.

## 7. 동결 (단계 0, 지금 유효)

하지 않는다:

- 새 방·새 퍼즐 콘텐츠
- `Assets/Fungus/` 벤더 수정 (Fade/Wait/`Continue()` 포함)
- 새 `*SceneMigrator` / Flowchart를 남기는 에디터 툴
- Flowchart에 블록 추가
- `FungusDialogueQaAdapter` · `HallQaFungusHop` · `DeveloperQaFungusSayPump` 확장
- Fungus `Command`를 상속하는 새 게임 커맨드

해도 된다:

- `Godlotto.Sequence` 런타임과 그 EditMode 테스트
- 대화 UI를 Fungus 타입에서 분리 (단계 1 이후)
- Kitchen 증명 (단계 2)
- 이 스펙·계획·task index 갱신
- 동결과 무관한 백엔드/로컬 LLM은 **이 feature의 허용 파일이 아니면** 별도 작업으로만

## 8. 단계 1 범위 (지금 구현)

관찰 가능:

- JSON 시퀀스 문서에서 `set_bool` / `set_int` / `set_string`이 `FlagStore`를 바꾼다.
- `if_bool`이 `then_block` / `else_block`으로 같은 문서 안 다른 블록을 실행한다.
- 알 수 없는 `command`는 `SequencePlayException`을 던진다.
- 빈 키, 없는 `block_id`, `if_bool` 순환은 실패한다.

제외 (단계 1 아님):

- Kitchen 씬 YAML 수정
- `RoomInteractionController`에서 Flowchart 제거
- talk/menu/wait/fade UI
- Checkpoint를 FlagStore에 연결
- Fungus 패키지 삭제
- QA 툴 재작성

## 9. 단계 2 증명 (Kitchen, 나중에)

Kitchen.unity에서 Flowchart 컴포넌트가 0이어야 한다. 싱크/병/수도 경로는 Sequence JSON + 기존 `KitchenPuzzleState` 게이트가 FlagStore를 읽는다. 이 단계가 끝나야 삭제가 가능하다는 증거가 된다.

## 10. 삭제 정의 (단계 4)

- `disputatio/Assets/Fungus/` 없음
- `disputatio/Assets/FungusExamples/` 없음
- 게임 어셈블리에 `using Fungus` 0
- 플레이 씬에 Flowchart 컴포넌트 0
- QA가 Flowchart/`SayDialog` 내부 필드를 긁지 않음

## 11. 테스트

- 단계 1: EditMode `FlagStoreTests`, `SequencePlayerTests`
- 명령: `.\scripts\unity-cli.cmd --project disputatio test --mode EditMode --filter FlagStoreTests` 및 `SequencePlayerTests`
- 단계 2 이후: Kitchen 시퀀스 JSON 픽스처 + 씬에 Flowchart 문자열 없음 검사

## 12. 오류

- 데이터 오류는 조용히 Continue하지 않는다. 예외 메시지에는 `block_id`와 `command`를 넣는다.
- 런타임은 null document / null FlagStore를 받지 않는다.

## 13. 공유 (Astra)

| 문서 | 역할 |
|------|------|
| `docs/development/tasks/fungus-deletion/index.md` | 현재 상태·동결·다음 명령. 채팅 대신 이것을 연다 |
| 본 스펙 | 목적지와 금지 |
| `docs/superpowers/plans/2026-09-17-sequence-runtime-phase1.md` | 단계 1 구현 순서 |
| `docs/architecture.md` | 코드 위치. 동결 배너가 있으면 새 Fungus 작업을 시작하지 않는다 |

커밋·push는 사용자 요청이 있을 때만 한다.
