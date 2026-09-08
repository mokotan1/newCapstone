---
name: newcapstone-feature-workflow
description: >-
  Split or continue a product feature across Cursor Task subagents. Use when
  the user asks to coordinate work, delegate subagents, write task packets,
  or continue a feature from docs/development/tasks/.
---

# newCapstone 얇은 총괄

## When to use

- 기능을 나누거나 서브에이전트에 맡길 때
- `docs/development/tasks/`를 쓰거나 이어서 할 때
- “feature-coordinator를 맡아라”는 요청

구현 폴더 배치는 `newcapstone-architecture`가 먼저다.

## Mandatory

1. `AGENTS.md`와 `docs/development/feature-workflow.md`를 읽는다.
2. 현재 러너의 `Task` 타입과 `model` slug를 **이번 세션에서** 확인한다. 문서 표와 다르면 문서가 오래된 것이다. 없는 도구를 있다고 하지 않는다.
3. `feature-workflow.md` 범위 산정으로 허용 파일과 공유 자원을 고정한다. AC가 천장이다.
4. 분할 조건 4개를 검사한다. 미달이면 패킷을 늘리지 말고 부모가 구현한다.
5. 위임 시 `Task`에 실제 `subagent_type`과 (필요하면) `model`을 넣는다. 역할 이름을 타입인 척하지 않는다.
6. 같은 checkout 쓰기는 순차. Unity Editor는 한 세션. QA playtester lease가 있으면 Editor를 만지지 않는다.
7. 리뷰는 구현과 별도 `Task` 세션. 없으면 `independentReview: false`, verified 금지.
8. 커밋·push는 사용자가 요청할 때만.

## Do not

- `backend-implementer` 등을 Cursor 타입으로 호출
- 목록에 없는 model slug 발명
- 위임하지 않은 작업을 멀티에이전트 완료로 보고
- QA 에이전트에게 버그 수정 맡기기
