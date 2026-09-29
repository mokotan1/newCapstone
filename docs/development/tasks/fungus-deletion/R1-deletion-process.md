# R1 — Fungus 삭제 실행 (씬 이전)

## 4단계 (반복)

1. **Inventory** — `python3 -m fungus_deletion.cli --repo-root . inventory` 로 씬·블록·ExecuteBlock 밀도 확인
2. **Start+Fade 자동 제거** — `python3 -m fungus_deletion.cli --repo-root . strip-start-fade --apply` (8씬, `PROGRESS.md` 참고)
3. **Sequence 대체** — `InteractionRoute` + JSON + `RoomInteractionSequenceHost` (Fungus 블록 호출 중단)
4. **Fungus 블록 은퇴** — 해당 `ObjectClicked` / UI `ExecuteBlock` 비활성화
5. **Flowchart 축소** — 사용 블록 0이면 GameObject 제거 (Start·페이드 등 잔존 시 유지)

## 파일럿: BasementHallway

| 항목 | 내용 |
|------|------|
| 씬 | `Assets/Scenes/Mokotan/Basement/BasementHallway.unity` |
| 컨트롤러 | `BasementHallwayInteractionController` |
| Sequence JSON | `Assets/godlotto/SequenceExamples/BasementHallway/*.json` |
| 적용 (Editor) | **Tools → Godlotto → Fungus Deletion → Basement Hallway Sequence Pilot** |
| Fungus 잔류 | `Start` 블록(입장 페이드 인)만 유지 |
| 페이드 | R1.5 `GameplayScreenFade` — Fungus 문 블록과 동일 targetAlpha/duration |

## 다음 작업 선정

- `BetaEnd`는 사용자 요청으로 제거 대상에서 제외한다.
- 블록·명령 수는 작업량 추정에만 쓴다. 이전 순서는 실제 플레이 경로의 중요도, 공용 Fungus 의존성 해소, 검증 가능성으로 정한다.
- 다음은 새 게임 진입 경로(`MainMenuScene` → `IntroScene` → 오프닝 → `Hall_playerble`)와 공용 Say/Menu 연결을 확인한다. 씬 이전에 필요한 공용 기반이 있으면 그것을 먼저 처리한다.
- `Basement*Room`의 단순 Start+Fade 제거는 기존 작업에 반영했다. `BookCase2Back`은 현재 확정된 다음 대상이 아니다.

## 금지 (유지)

- `Assets/Fungus/` 패치
- `*SceneMigrator` **확장** (기존 migrator 수정 금지; **FungusDeletion/** 단일 파일럿 메뉴는 허용)
