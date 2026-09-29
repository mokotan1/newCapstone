# TODO — Fungus 삭제 (전체 씬 프레임워크)

## 목표
Flowchart 제거율 A ≥ 30% (17/55). Auto는 Codex NEXT_WORK만 실행.

## 진행 중
- Hall_playerble **Codex PASS** (슬라이스 + PlayMode). A **17/55 = 30.9%**
- Codex NEXT: `Hall_Right.unity` — Start/selectYes|No/Front_clicked/Showcase_Clicked/Medal_Clicked → C#; ElectricOn 게이트; fade→Hall_Right2; Flowchart 제거

## 남음
- Hall_Right EditMode HallRightControllerTests + PlayMode HallRightPlayModeTests
- Codex verify Hall_Right

## 검증 (Hall_playerble)
- EditMode HallPlayable 8/8, HallGlobalState 3/3
- PlayMode HallPlayablePlayModeTests 1/1
- Flowchart GUID 0; Opening_Office Variablemanager 제거

## 막힘 / 재개
- `HttpListenPortPolicy.DefaultPort` 임시 18290 (8090 대역 좀비). 재개: Hall_Right 패킷
