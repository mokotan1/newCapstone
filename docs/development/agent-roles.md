# 역할 (Cursor Task 타입에 맞춤)

하위 역할은 임의로 에이전트를 더 만들지 않는다. 범위 변경은 부모에게 반환한다.
`Task` 타입이 아닌 이름은 프롬프트 문장일 뿐이며 도구가 아니다.

## 부모 — 현재 채팅 (총괄 + 기본 구현자)

AC와 제외 범위를 적는다. 분할 조건을 검사한다.
미달이면 직접 구현하고 검증 명령을 돌린다.
위임했다면 보고를 그대로 완료로 인정하지 말고 diff와 테스트 출력을 확인한다.
QA에게 게임 코드 수정을 맡기지 않는다.

## 조사 — `Task` `explore`

관련 코드·씬·테스트 경로와 기존 패턴만 반환한다.
제품 파일을 수정하지 않는다. 코드와 문서가 다르면 양쪽 근거를 적는다.

## 구현 — 기본은 부모. 위임 시 `Task` `generalPurpose`

할당된 파일과 테스트만 고친다. 허용 목록 밖은 부모에게 반환한다.
백엔드면 해당 pytest, Unity C#이면 해당 EditMode `--filter`.
씬·프리팹·ProjectSettings는 이 역할이 아니라 Editor 단독 세션(부모 또는 명시된 한 명)만.
키를 출력하거나 유료 API를 무단 호출하지 않는다.

프롬프트에 “백엔드만” / “Unity C#만”을 적어 범위를 한정할 수 있다.
그 문장이 `subagent_type`을 바꾸지는 않는다.

## 리뷰 — `Task` `code-reviewer` 또는 `generalPurpose`, 구현과 별도 세션

패킷 AC와 diff·테스트 증거를 대조한다. 구현자 요약만으로 통과시키지 않는다.
판정: `pass` / `changes-requested` / `blocked`.
회귀·복구·테스트 실효성은 같은 리뷰 세션에서 이어서 봐도 된다.
취향 리팩터를 완료 조건으로 넣지 않는다.

## 플레이 QA — 기존 `.cursor/agents/qa-*.md`

`qa-coordinator` → inventory / scenario-author → **단일** `qa-playtester` → evidence-reviewer.
런타임 프로파일과 Editor lease를 지킨다. 실행 중 제품 코드를 고치지 않는다.
