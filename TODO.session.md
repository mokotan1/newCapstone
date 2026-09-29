# TODO — Fungus 삭제 (전체 씬 프레임워크)

## 목표
Flowchart 제거율 A ≥ 30% (17/55). Auto는 Codex NEXT_WORK만 실행.

## 진행 중
- Hall_playerble **커밋·push 완료** (A 17/55). Codex NEXT: Hall_Right
- QA autorun: hall-nav click→HallPlayableController OK; assert Kitchen은 scene.waitReady 미구현·멀티홉 대기로 BLOCKED

## 남음
- Hall_Right 패킷
- hall-nav 풀 Kitchen 라우트: waitReady 구현 또는 동기 hop/대기 스텝

## 검증
- Push: 13fad545, 76610828, 88c8ab0e
- Autorun: `docs/qa/runs/20260929T-hall-playerble-30pct-autorun` BLOCKED; retry2 Failed(destination-mismatch) after adapter fix

## 막힘 / 재개
- DeveloperQa `scene.waitReady` not implemented — Kitchen assert 불가
- Play Mode `editor play` 시 unity-cli hang 잔존
- 재개: Hall_Right 또는 waitReady/hop QA
