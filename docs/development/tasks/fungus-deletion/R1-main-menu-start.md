# R1 MainMenu 새 게임 전환

상태: implementation-applied, verification-pending (2026-09-28). 사용자 요청으로 리뷰 에이전트는 사용하지 않고, QA 도구 문제로 실제 플레이 검증을 뒤로 미뤘다. R3 저장·씬 전환 변경이므로 `verified`가 아니다.

## 동작과 범위

`MainMenuScene` Start 버튼은 `MainMenu.OnStartButton`만 호출한다. 이 메서드는 진행 데이터와 인벤토리·드래그를 초기화하고 오디오·화면 설정은 보존한 뒤 `SceneNames.IntroScene`으로 전환한다. 이미 다른 씬 전환이 진행 중이면 저장 데이터를 지우지 않는다. 기존 Fungus `StartButton` 블록과 Flowchart는 제거했다. 이어하기와 설정 UI는 이번 작업 범위가 아니다.

하위 모델은 `MainMenu.cs`와 `MainMenuNewGameResetTests.cs`를 구현했다. 부모가 `MainMenuScene.unity`를 Editor에서 수정했고, 씬 전환 진행 중 초기화 방지 조건과 회귀 테스트를 추가했다. `Assets/Fungus/`와 기존 사용자 변경은 수정하지 않았다.

## 확인한 증거

- Unity 6000.0.36f1 컴파일 완료.
- `unity-cli test --mode EditMode --filter MainMenuNewGameResetTests`: 6/6 통과. 진행 초기화·설정 보존·Intro 전환 요청 순서·중복 클릭·전환 중 저장 보존을 포함한다.
- `MainMenuScene` 재로드: Flowchart 0, StartButton persistent call 1개(`MainMenu.OnStartButton`), `IntroScene` 빌드 포함, 씬 dirty=false.
- `MainMenuManager`의 누락 스크립트 1개는 HEAD 씬에도 있는 기존 상태다.
- QA 게이트웨이의 `mainmenu.new-game-reset` 보고서는 PASS지만 실제 씬 전환을 assert하지 않고 종료 시 SceneName도 비어 있어 시작→Intro 검증 근거로 계산하지 않는다.

## 남은 확인

격리 프로필에서 Start 클릭→Intro 진입, 설정 보존, 반복 클릭, 새 Console 오류를 확인해야 한다. 사용자 요청으로 독립 리뷰는 수행하지 않는다.
