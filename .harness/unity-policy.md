# Unity 공통 정책

에이전트는 대상 Unity 프로젝트와 Editor 상태를 확인하고, 기존 에셋·참조·사용자 변경을 보존하며 요청된 작업을 수행한다. Editor 조작은 단일 담당자가 수행한다. 변경 종류와 위험도에 맞는 컴파일·직렬화·테스트·플레이 증거로 결과를 검증한다. 실행 도구가 달라도 완료 기준을 유지한다.

이 파일은 대전제·범위·위험도·소유권이다. 명령어와 완료 판정은 `.harness/unity-verification.md`와 `.harness/unity-toolchain.json`을 본다.

## 범위

적용: `disputatio/` C#·씬·프리팹·패키지, Unity 검증 규칙, 로컬 CLI 하네스, QA gateway 연동.

제외: 게임 기능 자체 변경을 이 문서로 승인하지 않는다. Unity 버전 업그레이드, 백엔드 재설계, 배포 정책, 공식 CLI 기본 경로 전환은 별도 단계와 증거가 필요하다.

## 문서 우선순위

1. 사용자 요청과 플랫폼 상위 지침
2. 이 정책과 `.harness/unity-verification.md`
3. 영역별 규칙(Cheshire localization, QA lease, architecture §6)
4. backend별 사용법(`.cursor/skills/newcapstone-unity-automation/SKILL.md`, `.harness/unity-cli-postflight.md`)

영역별 추가 요구는 공통 완료 기준을 약화하지 않는다. `AGENTS.md`는 진입점만 두고 상세 명령을 복제하지 않는다.

## 위험도

파일 확장자만으로 판단하지 않는다. 공유 상태 영향이 있으면 상향한다.

| 등급 | 예 | 검증 및 리뷰 |
|---|---|---|
| R0 | 설명 문서, 동작에 영향 없는 주석 | diff·경로·규칙 정합. Unity 실행 불필요 |
| R1 | 독립 계산, 좁은 로직 버그, 단일 컴포넌트 | 재현/실패 조건, 컴파일, 관련 테스트. 명세·품질 체크를 한 명이 순차 수행 가능 |
| R2 | 씬·프리팹·UI 입력, 컴포넌트 수명 주기 | 컴파일(해당 시), 참조 확인, PlayMode/플레이 검증, 별도 세션의 명세·품질 검토 |
| R3 | 저장·씬 전환·공유 상태·패키지·하네스 변경 | R2 + 경계 회귀·복구. 명세 통과 후 별도 품질 리뷰 |

R0/R1의 자체 검토는 `independentReview: false`로 기록하되, 승인된 경량 정책을 충족하면 완료 가능하다. R2/R3에서 필수 독립 리뷰가 없으면 리뷰는 blocked이며 전체 verified가 아니다.

## Editor 소유권

1. 변경 전 projectPath, Editor PID, compilation/play 상태, dirty scene, QA run/lease를 확인한다.
2. 통합 담당자가 일반 Editor 조작을 소유한다. QA 시작 시 새 Editor 변경 명령을 멈추고 playtester에게 소유권을 인계한다.
3. playtester는 기존 QA gateway로 lease를 획득하고 heartbeat를 유지한다.
4. 성공·실패·취소 시 증거를 저장하고 프로파일 복원·lease 해제를 확인한다.
5. 통합 담당자는 복원 증거를 확인한 뒤 소유권을 돌려받는다.

문서상의 소유권 기록은 실제 잠금이 아니다. 연결 timeout은 재시작 허가가 아니다. mutation 결과가 불명확하면 같은 명령을 재전송하지 않는다.

같은 checkout의 구현은 순차다. 별도 checkout에서도 Editor 조작자는 한 명이다.

## 예외

- 사용자가 검증 생략을 명시한 검사는 waived다. 전체 verified로 표시하지 않고 '구현 반영, 검증 일부 생략'으로 보고한다.
- 도구 불가·기존 실패는 통과로 바꾸지 않는다. 필수 대상이 기존부터 실패면 fail/blocked로 남긴다.
- 공식 CLI는 `.harness/unity-toolchain.json`의 `official-unity-cli.status`가 호환성 증거를 기록하기 전에는 활성화하지 않는다.

## 스펙·에셋 작업 사례 (AC10–AC14)

정책 통합 단계의 정적 검토다. 실제 에이전트 준수나 Unity 동작 성공의 증거가 아니다.

| 사례 | 필수 기록 | verified가 되려면 |
|---|---|---|
| 스펙이 있는 작업 | 기준 문서 식별값과 **요구사항 대응표** | **누락된 요구**가 있으면 verified 금지 |
| 명시된 **구조 변경** | 차이와 결정 근거 | **사용자 결정** 전 의존 변경 실행 금지. 명세를 유지하는 내부 구현은 자율 |
| 버튼 참조·고정 UI | **기존 에셋**을 수정 | 불필요한 **런타임 보정** 스크립트 추가 금지. 새 스크립트에는 요구와 필요성 근거 |
| **일회성** Editor 자동화 | 대상만 저장 | **재로드** 후 값·참조 유지, 자동화 재실행 없이 동작 확인 |
| 리뷰 | **명세 리뷰**는 요구와 결과 대조, **품질 리뷰**는 불필요 Manager·상태 중복·임시 코드 잔존 | R2/R3는 둘 다 필요 |
