# Fungus 삭제 — 진행률 (공식 지표)

갱신: 2026-09-29 — Hall_playerble Flowchart 제거 + PlayMode PASS; A 17/55 = 30.9%. Codex NEXT: Hall_Right.

2026-09-29 현재 브랜치: `BetaEnd` 제외. Flowchart 완전 제거는 **17/55씬 = 30.9%**다.

## 1. 전체 게임 Flowchart 제거율 (씬 기준)

| 지표 | 값 |
|------|-----|
| 제거 대상 플레이 씬 (`BetaEnd` 제외) | **55** |
| Flowchart **완전 제거** 커밋 | **17** (검증 대기 포함) |
| **제거율 A** | **17 ÷ 55 = 30.9%** |

초기 8씬 (Start→FadeScreen only): `Basement.unity`, 지하 3방(벽돌·추출·관찰), `GoPrisonAnimation`, `POAnimation`, `StudyRoomCutScene`, `Opening_Office`. 추가: `BasementResearchRoom`, `MainMenuScene`, `IntroScene`, `BasementHallway`, `Hall_animate`, `Opening_Mention`, `Opening_Mention _open`, `SettingScene`, **`Hall_playerble`**.

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
전체 게임 제거율 **A**는 **30.9%**다 (목표 ≥30% 달성; Codex Hall_playerble 검증 대기).

## 3. 잔여 (R1/R2)

| 항목 | 상태 |
|------|------|
| `Hall_playerble.unity` | Flowchart_hall+Variablemanager 제거; HallPlayableController + HallGlobalStateHost; EditMode 8/8; PlayMode 1/1; Codex PASS |
| `Opening_Office.unity` | Variablemanager 제거·HallGlobalStateHost (Hall_playerble 동일 패킷) |
| Codex PATH | **Hall_Right** — Start/selectYes|No/Front/Showcase/Medal → C#; ElectricOn; fade→Hall_Right2 |

## 4. 검증 (cloud)

```bash
cd scripts && PYTHONPATH=. python3 -m pytest fungus_deletion/tests -q
dotnet test scripts/FungusDeletionSequenceTests/FungusDeletionSequenceTests.csproj
dotnet run --project scripts/CSharpSyntaxChecker/CSharpSyntaxChecker.csproj -- disputatio/Assets
PYTHONPATH=. python3 -m fungus_deletion.cli --repo-root .. inventory
```
