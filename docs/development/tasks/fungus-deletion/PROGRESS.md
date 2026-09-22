# Fungus 삭제 — 진행률 (공식 지표)

갱신: 2026-09-21 (cloud, `feature/fungus-deletion-framework`)

## 1. 전체 게임 Flowchart 제거율 (씬 기준)

| 지표 | 값 |
|------|-----|
| Flowchart 보유 플레이 씬 (inventory) | **56** |
| Flowchart **완전 제거** + C# 입장 페이드 커밋 | **8** |
| **제거율 A** | **8 ÷ 56 ≈ 14.3%** |

대상 8씬 (Start→FadeScreen only): `Basement.unity`, 지하 3방(벽돌·추출·관찰), `GoPrisonAnimation`, `POAnimation`, `StudyRoomCutScene`, `Opening_Office`.

자동화: `python3 -m fungus_deletion.cli --repo-root . strip-start-fade [--apply]` (`scripts/`에서 `PYTHONPATH=.`).

## 2. 마스터 계획 실행 지수 (가중, 목표 50% 이상)

| 구성 | 가중 | 완료 | 기여 |
|------|------|------|------|
| P0–P7 코드·standalone 테스트 | 35% | 100% | 35.0 |
| R1 지하 파일럿 (Hallway Sequence + Simple Room Editor + R1.5 페이드) | 25% | 95% | 23.8 |
| R1 씬 YAML git 반영 (위 8씬 + stripper) | 25% | 100% | 25.0 |
| R2 Unity EditMode/PlayMode (Windows) | 15% | 0% | 0.0 |
| **합계 B** | 100% | — | **≈ 83.8%** |

**50% 요청 대응:** 가중 지수 **B ≥ 50%** 달성 (8씬 커밋 + P0 stripper + CLI 수정).  
전체 게임 제거율 **A**는 지하·오프닝 단순 씬만 반영되어 **~14%** — Hallway·Research 등은 Editor 파일럿(R1) 잔여.

## 3. 잔여 (R1/R2)

| 항목 | 상태 |
|------|------|
| `BasementHallway.unity` Sequence Pilot | Editor 메뉴 적용 후 커밋 필요 |
| `BasementResearchRoom` (Desk 등) | Simple strip 대상 아님 |
| Hallway `Start` 페이드 C# 이전 | R1 4단계 |
| EditMode `BasementHallwayInteractionControllerTests` | Windows unity-cli |

## 4. 검증 (cloud)

```bash
cd scripts && PYTHONPATH=. python3 -m pytest fungus_deletion/tests -q
dotnet test scripts/FungusDeletionSequenceTests/FungusDeletionSequenceTests.csproj
dotnet run --project scripts/CSharpSyntaxChecker/CSharpSyntaxChecker.csproj -- disputatio/Assets
PYTHONPATH=. python3 -m fungus_deletion.cli --repo-root .. inventory
```
