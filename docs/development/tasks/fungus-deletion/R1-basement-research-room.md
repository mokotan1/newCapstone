# R1 — BasementResearchRoom Flowchart 제거

- 상태: 씬 적용·EditMode·재로드 확인, 플레이 QA·독립 리뷰 대기 (2026-09-28)
- 위험도: R2 (씬·UI 입력)

## 행동 AC와 범위

1. 입장 시 기존 `Start`의 검은색 1초 페이드(`targetAlpha=0`)를 유지한다.
2. `Desk` 클릭은 `Panel`을 열고, 패널의 기존 Backspace 버튼은 패널을 닫는다. 열린 동안 뒤쪽 월드·HUD 입력을 막는다.
3. 이 씬의 `Flowchart`와 세 블록을 제거하고 씬 재로드 후 누락 참조가 없다.

허용: `BasementResearchRoom.unity`, 연구실 전용 `BasementResearchDeskOpener.cs`와 EditMode 테스트, 일회성 `BasementResearchRoomFlowchartRemovalPilot.cs`, 이 문서와 `index.md`. 제외: 다른 씬, Fungus 코어, 기존 체크포인트 변경, 커밋·push.

## 적용·검증

기존 세 블록은 `Start`→`FadeScreen(1초, targetAlpha=0)`, `Desk_Clicked`→`Panel.SetActive(true)`, `BackSpace_Panel`→`PanelBackspaceCloser.ClosePanel` 호출이다. Backspace 블록의 버튼 참조는 비어 있었고, 실제 버튼에는 이미 `PanelBackspaceCloser`가 연결돼 있었다.

Editor PID 41792, Unity 6000.0.36f1, connector 0.3.21. 변경 전 활성 `BasementHallway`는 미저장 변경과 PlayMode가 없었고 QA lease도 없었다. `editor refresh --compile` 성공. `BasementResearchDeskOpenerTests` 2/2 통과. 일회성 메뉴 적용 후 씬을 다시 열어 `Flowchart=0`, `Missing Script=0`, Desk opener/Panel 참조/모달 범위/닫기 버튼/입장 페이드가 모두 존재하고 `Clickable2D`는 비활성, 씬은 dirty=false임을 확인했다.

Unity Console의 오류 목록은 `[]`였다. 실제 클릭·화면 페이드 PlayMode QA는 아직 수행하지 않았다. 기존 QA gateway의 등록 씬 목록에 `BasementResearchRoom`이 없다. Cursor wrapper를 통한 별도 리뷰는 CLI 인증 요구(`agent login` 또는 `CURSOR_API_KEY`)로 시작되지 못했다. `independentReview: false`; 별도 세션 리뷰 전에는 verified가 아니다.
