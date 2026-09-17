# R1 — Fungus 삭제 실행 (씬 이전)

## 4단계 (반복)

1. **Inventory** — `python3 -m fungus_deletion.cli inventory --repo-root .` 로 씬·블록·ExecuteBlock 밀도 확인
2. **Sequence 대체** — `InteractionRoute` + JSON + `RoomInteractionSequenceHost` (Fungus 블록 호출 중단)
3. **Fungus 블록 은퇴** — 해당 `ObjectClicked` / UI `ExecuteBlock` 비활성화
4. **Flowchart 축소** — 사용 블록 0이면 GameObject 제거 (Start·페이드 등 잔존 시 유지)

## 파일럿: BasementHallway

| 항목 | 내용 |
|------|------|
| 씬 | `Assets/Scenes/Mokotan/Basement/BasementHallway.unity` |
| 컨트롤러 | `BasementHallwayInteractionController` |
| Sequence JSON | `Assets/godlotto/SequenceExamples/BasementHallway/*.json` |
| 적용 (Editor) | **Tools → Godlotto → Fungus Deletion → Basement Hallway Sequence Pilot** |
| Fungus 잔류 | `Start` 블록(입장 페이드 인)만 유지 |
| 페이드 | R1.5 `GameplayScreenFade` — Fungus 문 블록과 동일 targetAlpha/duration |

## 다음 후보 (Inventory risk 낮은 순)

- `Basement*Room` (Start+Fade only) — **Tools → Basement Simple Rooms Remove Flowchart** (R1.5)
- `*Entrance` — Menu/Say는 Glass Menu 이후; `CorridorEntranceController` 패턴 유지
- Kitchen / Hall — 고위험, 마지막

## 금지 (유지)

- `Assets/Fungus/` 패치
- `*SceneMigrator` **확장** (기존 migrator 수정 금지; **FungusDeletion/** 단일 파일럿 메뉴는 허용)
