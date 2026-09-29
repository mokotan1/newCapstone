# TODO — Fungus 삭제 (전체 씬 프레임워크)

## 목표
Flowchart 제거율 A ≥ 30% (17/55). Auto는 Codex NEXT_WORK만 실행.

## 진행 중
- Codex PASS: Opening_Mention 배선 수리 + Opening_Mention _open → **A 15/55 = 27.3%**
- Codex NEXT_WORK: `Hall_playerble` — `HallPlayableController` + `HallGlobalStateHost`(Variablemanager 대체, FlagStore 싱글톤 금지)로 **양쪽** Flowchart 제거; Opening_Office Variablemanager도 동일 패킷

## 남음
- Hall_playerble (+ Opening_Office Variablemanager) 완료 → A 16/55
- Hall_Right → A 17/55 (30.9%)
- HallGlobalStateHost 도입됨; 소비자 Find()+SetBooleanVariable → FlowchartLocator.Get/SetBoolean 이전 및 씬 Flowchart 제거 파일럿 미완

## 검증
- OpeningMentionControllerTests 5/5; OpeningMentionOpenControllerTests 3/3 (OnDoorClicked 경로)
- Opening_Mention _open: static OK host=OpeningMentionOpenController; flowchart GUID 없음
- Play Mode: SayDialog waitForInput로 unity-cli hang — Codex가 EditMode OnDoorClicked로 PASS 인정
- HallGlobalStateHostTests: 미실행(작성만)

## 막힘 / 재개
- 재개: Codex Turn 17–18 AC대로 HallPlayableController 라우트 이전 + Variablemanager 제거 파일럿 + 소비자 Get/SetBoolean 이전
- `TODO(handoff)` Hall_playerble packet incomplete
