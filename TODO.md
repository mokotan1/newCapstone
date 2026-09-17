# TODO — QA 오토런 라이브 경로 강화

## 목표
1. 통신 장애(timeout/health down) 시 즉시 중단, 이후 Unity 명령 0회.
2. 파일 lease·journal·qa_status 격리 대조·qa_recover 복원이 라이브 runner에 연결.
3. cleanup/recovery/isolation 실패가 PASS로 남지 않음. run_play_hops 종료코드 = 판정.
4. hop 뒤 Fungus Say/Menu를 probe→advance/choose로 실제 진행.
5. 같은 run 안에서 screenshot(hop별)·console delta 수집 후 validate.
6. 다른 방 확대는 이번 범위 밖(확장 지점만).

## 완료한 항목
- transport.py: 첫 장애 trip, 이후 CLI 거절
- verdict.py: isolation/recovery/transport/dialogue reason code
- lease.py: docs/qa/runs/_lease.json, 죽은 pid만 회수
- coordinator: recover/fail/finish가 실패를 complete로 표시하지 않음
- lifecycle.py: qa_status/qa_cancel/qa_recover/screenshot/console, health-down 시 CLI 생략
- dialogue.py: probe→advance/choose, missing cap은 unsettled
- runner.py: 위 모두 선택 주입, transport 시 hop 중단
- live_vertical.py / run_play_hops.py 라이브 배선. 종료코드 = 판정. transport-down이면 editor stop 생략
- FungusDialogueQaAdapter + factory 등록
- architecture.md, .gitignore (_lease.json, evidence, journal)

## 진행 중인 항목
- 없음

## 남은 항목
- 다른 방·복도 확대: `FUNGUS_BLOCK_CAPABILITIES`에 씬 YAML 블록만 추가
- 독립 리뷰 전 featureVerified=false 유지
- 라이브 Hall→Kitchen Play Mode 재실행은 이번 세션에서 하지 않음 (Editor 컴파일/EditMode 검증만)

## 검증 결과
- `python -m pytest scripts/qa/tests -q` → 170 passed
- `ruff check scripts/qa/tool scripts/qa/tests` → All checks passed
- unity-cli status: ready (PID 45700)
- editor refresh --compile: 성공. 새 error 없음 (기존 warning만)
- EditMode FungusDialogueQaAdapterTests: 4 passed
- EditMode DeveloperQaServiceFactoryMultiRoomTests: 3 passed

## 목표 달성 여부
달성 (항목 1–5). 항목 6은 확장 지점만 남김.

## 변경 파일
- scripts/qa/tool/{transport,lease,lifecycle,dialogue,runner,verdict,coordinator,hall_route,live,live_vertical,run_play_hops}.py
- scripts/qa/tests/test_tool_{transport,lease,lifecycle,dialogue,runner,verdict,coordinator,hall_route}.py
- disputatio/.../FungusDialogueQaAdapter.cs (+ Tests, factory)
- docs/architecture.md, .gitignore, TODO.md

## 다음에 실행할 작업
- 사용자가 요청하면 `python -u scripts/qa/tool/run_play_hops.py`로 라이브 재실행
