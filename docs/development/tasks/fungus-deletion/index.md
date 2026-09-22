# fungus-deletion

## 현재 실행 기준 — 전체 씬 프레임워크 재정립 (2026-09-17)

- 최종 범위: **전체 씬·공용 프리팹·전역 상태·저장·대화·QA의 Fungus 제거**. Kitchen은 검증 후보 하나이며 최종 목표/필수 선행 방이 아니다.
- 전체 계획: [Fungus 제거 및 프레임워크 재정립 master plan](../../../superpowers/plans/2026-09-17-fungus-deletion-framework-master-plan.md)
- 실행 환경/모델: Codex 부모. 이전 Cursor Grok 4.6 기록은 과거 실행 정보이며, 이 세션의 도구·모델 배정을 뜻하지 않는다.
- 현재 단계: **P0 리스너·P1 Sequence·P3 route-state verified (EditMode).** P2 저장 거부 슬라이스는 EditMode 통과, independentReview false. 플레이와 라이브 QA는 미검증이다.
- 다음 작업: 제품 씬 42개의 Save Point 명령을 뺐다. 시작 블록은 Game Started가 이어 실행한다. 남은 구현은 대사. 플레이 QA는 아직.
- 원장: [migration-inventory.md](migration-inventory.md) / `migration-inventory.json` / `.csv`
- 구현 범위: P0는 조사·문서. 게임 씬/프리팹/패키지는 변경하지 않음. S1 Sequence 소스와 architecture 동결 배너는 워킹트리에 보존.
- 우선순위: 사용자 지시와 저장소 정책 다음으로 master plan.
- 중단/재개: 아래 인계. TODO 목록만으로 완료를 판정하지 않는다.

## 선행 실험 S1 — 기존 단계 1 기록 (아래 내용 보존)

아래 모델·Editor PID·다음 단계·passed 표기는 당시 실행 기록이며 현재 세션 상태 또는 전체 이전 완료를 뜻하지 않는다.


- Feature: Fungus 삭제 (C# 시퀀스 런타임)
- 한 줄 목표: Flowchart를 게임 규칙 소스에서 제거하고, 마지막에 `Assets/Fungus`를 지운다.
- AC1 (관찰 가능): `FlagStore` + `SequencePlayer`가 JSON `set_*` / `if_bool`을 Fungus 없이 실행한다.
- AC2 (실패/경계): 알 수 없는 command, 빈 키, 없는 블록, `if_bool` 순환은 예외로 실패한다.
- 제외(아직 미달): Kitchen 씬에서 Flowchart 제거, talk/menu UI, 패키지 삭제, Fungus QA 재작성
- 허용 파일 (단계 1): `disputatio/Assets/godlotto/Script/Sequence/**`, `disputatio/Assets/Editor/Tests/EditMode/Sequence/**`, `docs/architecture.md`, `docs/superpowers/specs/2026-09-17-fungus-deletion-runtime-design.md`, `docs/superpowers/plans/2026-09-17-sequence-runtime-phase1.md`, 본 index
- 공유 자원: 씬, 프리팹, `FungusVariableKeys`, Checkpoint, QA adapters — 단계 1에서 고치지 않음
- 선행: 스펙·단계 1 계획 채택 (2026-09-17)
- 검증 명령: `.\scripts\unity-cli.cmd --project disputatio test --mode EditMode --filter FlagStoreTests` 그리고 `--filter SequencePlayerTests`
- Cursor 도구: 부모
- 위험도: R1 (단계 1). Kitchen 씬은 R2, 패키지 삭제는 R3
- independentReview: false
- 상태: running
- verificationStatus: passed (단계 1 EditMode만. independentReview: false) — **현재 브랜치에서 재실행하지 않음. listener down.**
- 증거: 아래 결과

## 동결 (다른 에이전트도 동일)

하지 말 것: 새 Fungus 블록, `Assets/Fungus/` 패치, `*SceneMigrator` 추가, `FungusDialogueQaAdapter` 확장, 새 방 콘텐츠.

저번 Kitchen/Corridor/GlassMenu 마이그레이터처럼 **그래프를 남기는 툴을 성공으로 두지 않는다.**

## 문서

- 스펙: `docs/superpowers/specs/2026-09-17-fungus-deletion-runtime-design.md`
- 단계 1 계획: `docs/superpowers/plans/2026-09-17-sequence-runtime-phase1.md`
- P0 원장: `docs/development/tasks/fungus-deletion/migration-inventory.md`

## 인계

| 필드 | 내용 |
|------|------|
| runner | Codex Agent (부모) |
| modelId | current session inherited |
| checkedAt | 2026-09-22T02:01Z |
| capabilities | repo-read, file-write, shell, Unity CLI (`disputatio` ready) |
| assignment | 위임 없음. P0는 부모 순차 |
| Editor 소유권 | Unity PID 48440 ready (2026-09-22 status). QA playtester lease 미확인. Play/QA 전 lease 재확인. |
| stash 보존 | `stash@{qa}` 메시지 `preserve-qa-tool-integration-wip-2026-09-17` — qa-tool-integration 미커밋. 이 브랜치에서 pop 하지 말 것 |
| 다음 단계 | 제품 씬 Save Point 명령 제거됨. 이어하기는 `Checkpoint.Latest.v1`. 다음은 대사. 커밋은 사용자 요청 시에만. 플레이 QA 전 |

## 결과 (P0)

- 브랜치: `feature/fungus-deletion-framework` from `develop`/`e1de9505`
- 스캔: 씬 129, 제품 56, 제품 블록 397, 제품 명령 2063, GUID 참조 파일 241, C# Fungus 파일 149
- 미등록 씬 0, 미분류 명령/블록 0
- 리스너: 2026-09-22 `status` → ready, exit 0, PID 48440 (`P0-infra-unity-cli-listener.md` verified)
- 라이브 QA: 미실행. EditMode는 P1·P3에서 아래 결과로 재실행함.
- independentReview: false
- 다음 명령: `.\scripts\unity-cli.cmd --project disputatio status`

## 결과 (P1 Sequence 계약)

- 구현: 키별 단일 FlagStore 타입, 공백 키 거부, JSON 필수 필드·값 타입 검사, 전체 문서의 schema/블록/명령/참조/순환/깊이/명령 수 사전 검증
- RED: `FlagStoreTests` 3개와 `SequencePlayerTests` 8개 계약 실패를 확인한 뒤 구현함
- GREEN: `editor refresh --compile` exit 0, console error `[]`, `FlagStoreTests` 8/8, `SequencePlayerTests` 18/18
- independentReview: true. 별도 read-only 리뷰가 두 결함(문서 내 타입 충돌, 공유 꼬리 깊이 우회)을 찾아 수정·재리뷰했고 Critical/Important 0건으로 P1 패킷을 verified 판정.

## 결과 (단계 1 EditMode)

- 실제 변경 파일: Sequence 런타임 4파일 + EditMode 테스트 2파일 + `.meta`, 스펙/계획/index, `docs/architecture.md` 동결 배너

## P3 첫 실제 교체 — C# route state (verified)

- 패킷: [P3-route-state.md](P3-route-state.md)
- 범위: `SceneTracker`와 `BackNavigator`가 Flowchart `PrevScene`을 쓰거나 읽지 않고 `SceneRouteState`의 세션 route 상태만 사용한다. 고정 복귀 경로, 모달 입력 차단, fallback은 유지한다.
- 제외: `SceneName`·`SavePointKey`는 아직 Fungus SavePoint/블록이 읽으므로 유지한다. 씬·프리팹 직렬화와 Flowchart 블록은 이 패킷에서 수정하지 않는다.
- GREEN (2026-09-22): `editor refresh --compile` exit 0, console `[]`; `SceneRouteStateTests` 2/2, `BackNavigatorTests` 10/10, `FlagStoreTests` 8/8, `SequencePlayerTests` 18/18.
- independentReview: true (2026-09-21 R3 리뷰). 씬·프리팹 `PrevScene` 직렬화 정리는 후속 패킷.

## P2 첫 슬라이스 — 저장 전 검증 (EditMode만, 리뷰 전)

- 패킷: [P2-checkpoint-save-guard.md](P2-checkpoint-save-guard.md)
- 범위: `CheckpointRepository.Save`가 공백 `resumeSceneName`, 공백 Fungus 키, 키 중복·타입 충돌을 쓰기 전에 거절하고 이전 체크포인트를 유지한다.
- GREEN (2026-09-22): compile exit 0, console `[]`; RED 4 failed 후 `CheckpointRepositoryTests` 7/7, `CheckpointServiceTests` 6/6.
- independentReview: false. G2(변환·원본 보존 전체·독립 리뷰)는 미달.

## P2 세이브 키 대응표 (문서)

- 패킷: [P2-save-key-map.md](P2-save-key-map.md)
- 이어하기는 `Checkpoint.Latest.v1`. 새 게임은 설정 4키만 남기고 `DoSaveReset()`은 호출하지 않는다. 씬 YAML의 Save Point 명령은 남아 있다.
- 빌드 씬 변수 76개와, JSON 밖 PlayerPrefs 진행 키를 적었다. 코드 변경 없음. 플레이 미실행.
