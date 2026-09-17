# TODO — Fungus 삭제 마스터 계획

## 목표
MASTER-PLAN P0–P7 cloud 완료.

## 달성 (2026-09-17 cloud)
- P0 inventory/scanner CLI + pytest
- P1–P6 (기존 + outcome 체크포인트 제외 + 예제 JSON)
- P7 `scripts/FungusDeletionSequenceTests` (51 pass)
- Wiki architecture transcript/hash 동기화 (validate GREEN)

## 인계 (본 환경 불가)
- R1 씬 YAML 마이그레이션 (동결)
- R2 Windows Unity EditMode 전체 Sequence 필터
- independentReview: false

## 검증
- `dotnet test scripts/FungusDeletionSequenceTests/FungusDeletionSequenceTests.csproj` → 51 pass
- `PYTHONPATH=scripts pytest scripts/fungus_deletion/tests -q` → 3 pass
- `CSharpSyntaxChecker disputatio/Assets` → exit 0
- `python3 tools/wiki_rag/validate.py ...` → passed
