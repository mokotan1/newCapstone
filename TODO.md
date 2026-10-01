# TODO — Fungus 삭제

## 목표
40% 패킷(오른쪽 복도 5씬)을 검증하고, Codex 다음 패킷을 받아 보고한다. 다음 씬은 시작하지 않는다.

## 달성
오른쪽 복도 5씬 Flowchart 제거. Unity 검증 PASS. Codex `gpt-6-sol` 검증 PASS. `PROGRESS.md` A = 22/55. 다음 패킷은 2층 오른쪽 4씬(26/55에서 정지). 미착수.

## 진행 중
없음.

## 남음
다음 패킷 구현. 커밋·push 없음. 폰트 SDF 변경은 이번 작업이 아님.

## 검증
- EditMode `ElectricLightControllerTests` 2/2, `RightHallCorridorControllerTests` 21/21, `HallPlayableControllerTests` 8/8
- PlayMode `RightHallRoutePlayModeTests` 1/1
- 5씬 reserialize 후 console error 없음
- Build Settings, BetaEnd 제외: Flowchart 없는 씬 22/55
- `qa_run` 게이트웨이 없음. 라이브 스크린샷 플레이는 blocked. 경로 QA는 PlayMode
- Codex 검증: PASS. Unity는 Codex가 재실행하지 않음
- `FungusDeletionSequenceTests` csproj 경로 오류는 기존 문제. 이번 패킷 밖
