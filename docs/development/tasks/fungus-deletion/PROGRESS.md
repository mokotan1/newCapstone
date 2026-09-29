# Fungus 삭제 — 진행률 (공식 지표)

갱신: 2026-09-29 — Hall_animate + Opening_Mention Flowchart 제거; A 15/55 = 27.3%.

2026-09-29 현재 브랜치: `BetaEnd` 제외. Flowchart 완전 제거는 **15/55씬 = 27.3%**다.

## 1. 전체 게임 Flowchart 제거율 (씬 기준)

| 지표 | 값 |
|------|-----|
| 제거 대상 플레이 씬 (`BetaEnd` 제외) | **55** |
| Flowchart **완전 제거** 커밋 | **15** (검증 대기 포함) |
| **제거율 A** | **15 ÷ 55 = 27.3%** |

초기 8씬 (Start→FadeScreen only): `Basement.unity`, 지하 3방(벽돌·추출·관찰), `GoPrisonAnimation`, `POAnimation`, `StudyRoomCutScene`, `Opening_Office`. 추가 7씬: `BasementResearchRoom`, `MainMenuScene`, `IntroScene`, `BasementHallway`, `Hall_animate`, `Opening_Mention`, `Opening_Mention _open`.

자동화: `python3 -m fungus_deletion.cli --repo-root . strip-start-fade [--apply]` (`scripts/`에서 `PYTHONPATH=.`).

## 2. 마스터 계획 실행 지수 (가중, 목표 50% 이상)

| 구성 | 가중 | 완료 | 기여 |
|------|------|------|------|
| P0–P7 코드·standalone 테스트 | 35% | 100% | 35.0 |
| R1 지하 파일럿 (Hallway Sequence + Simple Room Editor + R1.5 페이드) | 25% | 100% | 25.0 |
| R1 씬 YAML git 반영 (위 8씬 + stripper) | 25% | 100% | 25.0 |
| R2 Unity EditMode/PlayMode (Windows) | 15% | 0% | 0.0 |
| **합계 B** | 100% | — | **≈ 85.0%** |

**50% 요청 대응:** 가중 지수 **B ≥ 50%** 달성 (8씬 커밋 + P0 stripper + CLI 수정).  
전체 게임 제거율 **A**는 **27.3%**다. 커밋에는 플레이 QA·리뷰 대기 씬이 포함된다.

## 3. 잔여 (R1/R2)

| 항목 | 상태 |
|------|------|
| `Opening_Mention.unity` | Flowchart 제거·OpeningMentionController 배선 수리; EditMode 5/5; A 14/55 |
| `Opening_Mention _open.unity` | Flowchart 제거·OpeningMentionOpenController; EditMode 3/3; A 15/55 |
| `Hall_animate.unity` | Flowchart 제거·HallAnimateSequence; EditMode 1/1 |
| Codex PATH_TO_30 남음 | Hall_playerble, Hall_Right |

## 4. 검증 (cloud)

```bash
cd scripts && PYTHONPATH=. python3 -m pytest fungus_deletion/tests -q
dotnet test scripts/FungusDeletionSequenceTests/FungusDeletionSequenceTests.csproj
dotnet run --project scripts/CSharpSyntaxChecker/CSharpSyntaxChecker.csproj -- disputatio/Assets
PYTHONPATH=. python3 -m fungus_deletion.cli --repo-root .. inventory
```
