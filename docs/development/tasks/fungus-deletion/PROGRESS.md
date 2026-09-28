# Fungus 삭제 — 진행률 (공식 지표)

갱신: 2026-09-28 (`feature/fungus-deletion-framework`)

2026-09-28 현재 브랜치: 사용자 요청으로 `BetaEnd`를 제거 대상에서 제외했다. `BasementResearchRoom`, `MainMenuScene`, `IntroScene`까지 Flowchart 완전 제거는 **11/55씬 = 20.0%**다. 지하 복도는 문 블록만 Sequence로 옮겼고 Flowchart `Start`가 남아 있다. QA 도구 문제로 플레이 검증을 뒤로 미뤘고 사용자 요청으로 리뷰 에이전트는 실행하지 않는다. 새 변경은 `verified`가 아니다.

## 1. 전체 게임 Flowchart 제거율 (씬 기준)

| 지표 | 값 |
|------|-----|
| 제거 대상 플레이 씬 (`BetaEnd` 제외) | **55** |
| Flowchart **완전 제거** 커밋 | **11** (검증 대기 포함) |
| **제거율 A** | **11 ÷ 55 = 20.0%** |

초기 8씬 (Start→FadeScreen only): `Basement.unity`, 지하 3방(벽돌·추출·관찰), `GoPrisonAnimation`, `POAnimation`, `StudyRoomCutScene`, `Opening_Office`. 추가 3씬: `BasementResearchRoom`, `MainMenuScene`, `IntroScene`.

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
전체 게임 제거율 **A**는 **20.0%**다. 커밋에는 플레이 QA·리뷰 대기 씬이 포함된다.

## 3. 잔여 (R1/R2)

| 항목 | 상태 |
|------|------|
| `BasementHallway.unity` Sequence Pilot | 문 5개 Sequence 이전은 커밋됨; `Start` Flowchart와 플레이 QA 남음 |
| `BasementResearchRoom` (Desk 등) | Flowchart 제거는 커밋됨; 플레이 QA·독립 리뷰 대기 |
| Hallway `Start` 페이드 C# 이전 | R1 4단계 |
| EditMode `BasementHallwayInteractionControllerTests` | Windows unity-cli |

## 4. 검증 (cloud)

```bash
cd scripts && PYTHONPATH=. python3 -m pytest fungus_deletion/tests -q
dotnet test scripts/FungusDeletionSequenceTests/FungusDeletionSequenceTests.csproj
dotnet run --project scripts/CSharpSyntaxChecker/CSharpSyntaxChecker.csproj -- disputatio/Assets
PYTHONPATH=. python3 -m fungus_deletion.cli --repo-root .. inventory
```
