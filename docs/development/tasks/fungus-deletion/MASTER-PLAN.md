# Fungus 삭제 프레임워크 — 마스터 계획

브랜치: `feature/fungus-deletion-framework` → `develop`

## 패킷 요약

| ID | 제목 | cloud 구현 | Unity EditMode | 비고 |
|----|------|------------|----------------|------|
| **P0** | Flowchart inventory + C# 의존 scanner | ✅ | — | `scripts/fungus_deletion/` |
| **P1** | FlagStore 타입 + SequenceValidator/Player | ✅ | ⏸ Editor 필요 | `P1-sequence-contract.md` |
| **P2** | Checkpoint `sequence*` 분리 | ✅ | ⏸ | `P2-save-contract.md` |
| **P3** | wait/say + Session + 입력 게이트 | ✅ | ⏸ | `P3-async-ui.md` |
| **P4** | Catalog + Router | ✅ | ⏸ | `P4-sequence-routing.md` |
| **P5** | RoomInteraction Sequence 브리지 | ✅ | ⏸ | `P5-interaction-bridge.md` |
| **P6** | outcome 예약 플래그 + 체크포인트 제외 | ✅ | ⏸ | `P6-outcome-flags.md` |
| **P7** | standalone NUnit harness (저장소 내) | ✅ | — | `scripts/FungusDeletionSequenceTests/` |
| **R1** | 씬 Fungus → Sequence 삭제 (4단계) | 🔧 진행 중 | Editor | [R1-deletion-process.md](./R1-deletion-process.md), BasementHallway 파일럿 |
| **R2** | Windows SayDialog PlayMode 검증 | ⏸ | 수동 | unity-cli |

⏸ = 이 Linux cloud 환경에서 실행 불가. 🚫 = 정책상 본 브랜치 범위 밖.

## 동결 (전 패킷 공통)

- 새 Fungus 블록, `Assets/Fungus/` 패치, `*SceneMigrator` 확장
- FlagStore 전역 싱글톤, Variablemanager 이중 기록
- `FungusDialogueQaAdapter` 확장

## 검증 명령

```bash
# Linux / CI (Sequence 런타임, Fungus·Unity 제외)
dotnet test scripts/FungusDeletionSequenceTests/FungusDeletionSequenceTests.csproj

# Unity C# 구문
dotnet run --project scripts/CSharpSyntaxChecker/CSharpSyntaxChecker.csproj -- disputatio/Assets

# P0 리포트 생성
python3 -m pytest scripts/fungus_deletion/tests -q
python3 -m fungus_deletion.cli --repo-root . inventory

# Windows Editor
.\scripts\unity-cli.cmd --project disputatio test --mode EditMode --filter Sequence
```

## 완료 기준 (cloud)

P0–P7 코드·문서·standalone 테스트 GREEN. Unity EditMode·씬 마이그레이션은 인계(R1/R2).
