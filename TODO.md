# TODO — Fungus 삭제

## 목표
2층 오른쪽 4씬 Flowchart 제거를 Codex `gpt-6-sol`이 PASS할 때까지 검증한다. 26/55에서 정지.

## 달성
`SecondFloorRightController`로 4씬 제거. 1차 FAIL(사진 콜라이더, 모달/UI 가드) 수정 후 재검증 PASS. `PROGRESS.md` A = 26/55.

## 진행 중
없음.

## 남음
push 없음. 폰트 SDF는 이번 작업이 아님. 제거율 A 50%는 다음 작업.

## 검증
- EditMode `SecondFloorRightControllerTests` 20/20
- PlayMode `SecondFloorRightPlayModeTests` 1/1 (사진 콜라이더 enabled 포함)
- EditMode `ElectricLightControllerTests` 2/2, `HallPlayableControllerTests` 8/8
- 4씬 reserialize 후 console error 없음
- Build Settings, BetaEnd 제외: Flowchart 없는 씬 26/55
- Codex 1차 FAIL, 2차 PASS. Unity는 Codex가 재실행하지 않음
