# 다크 글래스 선택지 메뉴 — 사용 가이드 (기획자용)

Fungus 기본 선택지 UI(`Menu` + `MenuDialog`)를 대체하는 다크 글래스 톤 선택지 메뉴.
하나의 커맨드에서 **선택지 개수·문구·분기·위치**를 모두 지정한다.

## 1. 최초 1회: 프리팹 생성

Unity 메뉴 `Tools ▸ Godlotto ▸ Build Glass Menu Prefabs` 실행.
→ `Assets/godlotto/Resources/Prefabs/` 에 `GlassMenuDialog.prefab`,
`GlassMenuOptionButton.prefab` 생성. (이미 있으면 룩 변경 시에만 재실행)

런타임에 씬에 `GlassMenuDialog`가 없으면 이 Resources 프리팹에서 **자동 스폰**된다.
원하면 씬에 직접 배치해 두어도 된다(씬 인스턴스가 우선).

## 2. 플로우차트에서 선택지 띄우기

블록의 **마지막 커맨드**로 `Narrative ▸ Glass Menu` 를 추가한다.

| 필드 | 의미 |
|---|---|
| `Options` | 선택지 목록. **리스트 길이 = 선택지 개수.** 항목마다: |
| ─ `Text` | 버튼 문구 (Fungus 변수 치환 `{$var}` 지원) |
| ─ `Target Block` | 이 선택지를 고르면 실행할 블록 |
| ─ `Interactable` | 끄면 표시되지만 선택 불가(회색) |
| `Anchor` | 패널 정렬 위치 9분할 프리셋(기본 `BottomCenter`) |
| `Menu Offset` | 앵커 기준 픽셀 오프셋 (예: `(0, 120)` = 하단에서 120px 위) |
| `Set Menu Dialog` | (선택) 특정 `GlassMenuDialog`로 지정. 비우면 자동 사용 |

**규약**: `Glass Menu`는 블록의 마지막 커맨드여야 한다. 이후 커맨드는 즉시 실행되므로
선택 대기 흐름이 깨진다(Fungus 원본 `Menu`와 동일 규칙).

분기 화살표는 플로우차트 그래프에 자동으로 그려진다(각 `Target Block`으로).

## 3. 위치 조정 예시

- 하단 중앙, 살짝 띄움: `Anchor = BottomCenter`, `Menu Offset = (0, 120)`
- 화면 정중앙 팝업: `Anchor = Center`, `Menu Offset = (0, 0)`
- 우측 하단: `Anchor = BottomRight`, `Menu Offset = (-40, 60)`

## 4. 룩 변경

팔레트는 `GlassMenuPrefabBuilder.cs` 상단 상수:

- 패널 채움: 검정 35% 알파
- 보더: 골드 `#D4AF6E (212,175,110)`
- 텍스트: `#EEF2F8 (238,242,248)`

색/여백/폰트 크기를 바꾸려면 프리팹을 직접 편집하거나, 빌더 상수를 고치고
`Build Glass Menu Prefabs` 를 재실행한다.

## 5. 제약 / 알아둘 점

- **타이머 없음** (의도된 사양).
- **실제 backdrop-blur 미지원**: uGUI 한계로 반투명+보더+그라데이션으로 글래스를
  근사한다. 진짜 블러가 필요하면 별도 UI 블러 셰이더/RenderTexture 작업이 필요.
- 원본 Fungus `Menu`/`MenuDialog`는 그대로 두었다. 기존 콘텐츠와 병행 가능.

## 관련 문서

- 설계: `docs/superpowers/specs/2026-06-06-glass-choice-menu-design.md`
- 구현 계획: `docs/superpowers/plans/2026-06-06-glass-choice-menu.md`
