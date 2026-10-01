# TODO — Fungus 삭제

## 목표
제거율 A를 50%까지. 28/55에서 멈춤.

## 달성
`WifeEntrance`, `BedEntrance` Flowchart 제거. A 28/55 (50.91%). Codex `gpt-6-sol` 재검증 PASS.

## 진행 중
없음.

## 남음
29번째 씬은 시작하지 않음. 폰트 SDF는 커밋에서 제외.

## 검증
EditMode `SecondFloorRoomEntranceControllerTests` 12/12, `SecondFloorRoomEntranceSceneTests` 2/2, `WorldItemDropZoneHostTests` 3/3. PlayMode `SecondFloorRoomEntrancePlayModeTests` 1/1. 콘솔 에러 없음. Build Settings `BetaEnd` 제외 28/55. `git diff --check` 통과.

## 변경 파일
`SecondFloorRoomEntranceController.cs`, `WorldItemDropZone.cs`, `WifeEntrance.unity`, `BedEntrance.unity`, 테스트 4개, `docs/architecture.md`, `docs/development/tasks/fungus-deletion/PROGRESS.md`.
