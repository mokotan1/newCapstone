# newCapstone repository instructions

기능 개발·버그 수정 전에 `docs/architecture.md`의 관련 절과
`docs/development/feature-workflow.md`를 읽는다.

- 부모(현재 채팅)가 총괄이다. 기본 구현자도 부모다.
- 위임은 Cursor `Task`가 실제로 제공하는 타입만 쓴다.
  문서 역할 이름을 등록된 서브에이전트라고 가정하지 않는다.
- 수정 범위는 `feature-workflow.md`의 범위 산정으로 정한다. AC가 천장이다.
- 쪼개기 전에 분할 조건을 확인한다. 미달이면 패킷을 늘리지 말고 부모가 구현한다.
- 구현이 끝나면 별도 세션으로 리뷰한다. 같은 채팅의 자기검토는
  `independentReview: false`이며 verified가 아니다.
- 같은 checkout의 쓰기 작업은 순차. Unity Editor는 한 세션만.
  `qa-playtester`가 Editor를 쓰는 동안 구현은 멈춘다.
- 커밋·push·배포는 사용자가 요청할 때만.
- 기존 사용자 변경을 보존한다. Unity Editor 조작은 단일 담당자가 수행한다.
- Unity 작업 분류·소유권은 `.harness/unity-policy.md`, 완료 판정은
  `.harness/unity-verification.md`를 따른다. Cursor 진입 규칙은
  `.cursor/rules/unity-verification-postflight.mdc`다. 상세 명령은 복제하지 않는다.
- 플레이 QA는 `.cursor/agents/qa-*.md`와
  `.cursor/rules/qa-subagent-orchestration.mdc`를 따른다. QA는 게임 코드를 고치지 않는다.
- Cheshire localization은 `.cursor/skills/cheshire-localization-sdd/SKILL.md`가 더 엄격하면 그것을 우선한다.

이 파일은 행동 지침이다. 에이전트 프로세스나 모델을 자동 생성하지 않는다.
