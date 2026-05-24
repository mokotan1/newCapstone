# StudyRoom FilterCard 책 패널 배치/이동 — 설계

작성일: 2026-05-22 (최종 갱신: 2026-05-22, 씬 직접 배치 방식으로 변경)
대상 씬: `disputatio/Assets/Scenes/Mokotan/First Floor/1floorRight/StudyRoom.unity`

## 목적

서재(StudyRoom) 씬의 `CardStackPanel` 아래에 BookOverlayPanelA 책 패널을 만들고,
인벤토리의 `FilterCard` 아이템을 드롭하면 기존 `FilterCard` UI 카드가 활성화되어
책 영역 안에서 자유롭게 드래그되도록 한다. 기존 회전 버튼/`FilterCardRotator`
흐름은 건드리지 않는다.

## 결정 사항 (사용자 확인)

1. 구현 방식: StudyRoom.unity 씬 파일 직접 편집.
2. `CardStackPanel` 아래에 **새 패널**(`FilterCardBookPanel`)을 직접 생성.
   기존 `WordCard` 패널은 원래 `DropZone` 상태로 복구.
3. 책 비주얼: `BookOverlayPanelA` 프리팹 인스턴스를 씬에 상시 배치.
4. 프리팹 인스턴스의 충돌 2가지를 인스턴스 한정 수정으로 우회.

## 발견한 충돌과 우회

`BookOverlayPanelA`는 런타임 전체화면 오버레이로 설계돼 있어 씬에 상시 배치하면:

1. **렌더링 순서** — 프리팹 루트의 `Canvas`(`overrideSorting`, `sortingOrder=50`)가
   다른 UI 위로 그려져 FilterCard·회전 버튼이 가려진다.
   → 프리팹 인스턴스 수정으로 `Canvas`(컴포넌트 `510002`)를 비활성화. 책이 부모
   캔버스에 인라인으로 그려지고, 계층 순서대로 카드/버튼이 위에 온다.
2. **일기 시스템 충돌** — 프리팹의 `BookOverlayPagedReader`를 `SceneBookOverlayRuntime`이
   `FindObjectOfType<BookOverlayPagedReader>`로 찾아 일기 오버레이로 오인한다.
   → 프리팹 인스턴스에서 `BookOverlayPagedReader`(컴포넌트 `510006`)를 제거
   (`m_RemovedComponents`). 프리팹 원본은 그대로.

## 씬 파일 변경 (`StudyRoom.unity`)

1. `WordCard` 패널(`&1755439169`): 작업 중 잠시 교체했던 스크립트를 원래 `DropZone`
   guid(`4f55531d…`)로 **복구** — `WordCard`는 원상태.
2. `FilterCard`(`&1493005125`): `Draggable` → `FilterCardBoundedDrag`
   (guid `3d7b869c47764fc592cbd6a3fd8e7687`).
3. `CardStackPanel` 자식 목록에 새 패널 RectTransform(`7700000002`) 추가
   (`WordCard` 다음, `FilterCard` 앞 — 책은 카드 뒤, 회전 버튼은 카드 앞).
4. 새 GameObject `FilterCardBookPanel`(`7700000001`) 추가 — `CardStackPanel`을
   가득 채우는 RectTransform + 투명 Image(드롭 타깃) + `FilterCardBookDropZone`.
5. `BookOverlayPanelA` 프리팹 인스턴스(`7700000010`) 추가 — `FilterCardBookPanel`의
   자식. 수정: `Canvas` 비활성, `BookOverlayPagedReader` 제거.

## 신규 스크립트 ① `FilterCardBookDropZone.cs` (IDropHandler)

위치: `disputatio/Assets/godlotto/Script/DropZone/`. `FilterCardBookPanel`에 부착.

- `Start()`: 회전 버튼 2개를 숨긴다(기존 `DropZone`과 동일 — 회전 흐름 보존).
- `OnDrop()`: 드롭 아이템이 `requiredItem`(FilterCard.asset)이면
  1. 드래그 경계로 쓸 책 패널 확보 — 씬에 배치된 `bookOverlayInstance`를 우선 사용
     (없으면 `Resources`에서 런타임 생성하는 폴백 경로).
  2. 기존 `FilterCard` UI 카드 활성화 + `anchoredPosition` 0으로 중앙 배치.
  3. `FilterCard`의 `FilterCardBoundedDrag`에 책 RectTransform을 경계로 주입.
  4. 회전 버튼 2개 표시.
  5. `InventoryManager.RemoveItem`으로 아이템 소비, `InventorySlot.ClearDragState()`.
  - `maxUses`(기본 1)만큼만 동작.

## 신규 스크립트 ② `FilterCardBoundedDrag.cs` (드래그 핸들러)

위치: `disputatio/Assets/godlotto/Script/`. `FilterCard`에 부착(기존 `Draggable` 대체).

- 포인터를 따라 `anchoredPosition` 이동, 드롭 실패 시에도 위치를 되돌리지 않음.
- 매 드래그마다 카드의 월드 4꼭짓점 AABB를 경계 AABB 안으로 클램프 →
  회전/스케일 상태에서도 패널 밖으로 안 나감.
- 경계는 드롭 시 `FilterCardBookDropZone`이 주입.
- 회전은 `localRotation`, 드래그는 위치 — `FilterCardRotator`와 충돌 없음.

## 건드리지 않는 것

- `FilterCardRotator.cs`, `RotateLeft`/`RotateRightButton`과 OnClick 배선.
- DiaryPanel 쪽 `WordCard2`의 `DropZone`, `FilterCard2`의 `Draggable`.
- 인벤토리 시스템, `SceneBookOverlayRuntime` 등 일기 흐름 코드, 기존
  `DropZone.cs`/`Draggable.cs` 파일.

## 알려진 리스크

- 프리팹에 원래 존재하던 미싱 스크립트 컴포넌트(`510005`)가 인스턴스에도
  딸려 와 콘솔에 "missing script" 경고가 뜰 수 있다(프리팹 원본 문제).
- `BookOverlayPagedReader` 제거로 책의 닫기/페이지 버튼 OnClick은 무동작이 된다.
- 프리팹 인스턴스 YAML을 직접 작성했으므로, 유니티에서 씬을 열어 정상
  로드·렌더링되는지 반드시 확인 필요.
