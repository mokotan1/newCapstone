# TODO — Fungus 삭제 (전체 씬 프레임워크)

## 목표
전체 씬·공용 자산에서 Fungus를 제거하고 C# 프레임워크로 통일한다.

## 진행 중인 항목
- 브랜치 `feature/fungus-deletion-framework`
- BasementHallway QA 슬라이스 커밋 직후 → Codex NEXT: Hallway `Start` fade → C#

## 남은 항목
- BasementHallway Start 페이드 C# 이전 + Flowchart 제거
- 독립 리뷰 (Cursor auth) — verified 전제
- 대사 이전
- 커밋 후 다음 슬라이스

## 검증 결과
- EditMode: RouteAssertion 10/10, Capability 5/5, Serialization 9/9
- Codex verify: PASS
- live qa_run basement-hallway.sequence-doors: Passed

## 목표 달성 여부
Hallway 문 Sequence + 격리 QA는 코드·라이브 Passed. Start 페이드·독립리뷰·verified는 남음.
