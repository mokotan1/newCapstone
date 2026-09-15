# unity-harness-policy

- Feature: Unity 하네스 정책 통합, legacy 정규화, 공식 CLI 조사
- 한 줄 목표: 실행 도구와 완료 기준을 분리하고, 공식 CLI는 호환성 게이트 실패로 기본 경로를 바꾸지 않는다.
- AC1–AC3, AC7, AC9–AC14: 정적 계약 테스트로 확인
- AC4–AC6, AC8, AC5: 공식 경로 미진입. legacy QA는 기존 `qa_*` 도구로 매핑만 함
- 제외: 공식 QA C# 진입점, 기본 backend 전환, 게임 기능
- 검증 명령: `python -m pytest scripts/unity-harness/tests -q`
- checkout: `feature/unity-harness-policy`
- 위험도: R3
- 검증 상태: passed (정적/파서·Pipeline 선언) / blocked (공식 QA 동등성·독립 리뷰)
- 상태: review
- independentReview: false
- 정리 보고: `cleanup-report.md`

## 결과

- `python -m pytest scripts/unity-harness/tests -q` → 25 passed
- `unity --version` → 1.0.0-beta.5
- Pipeline `0.6.0-exp.1`를 이 브랜치 `disputatio`에만 설치. `unity status` ready, 공식 `qa_*` 0개
- 활성 backend는 `legacy-unity-cli` 유지
