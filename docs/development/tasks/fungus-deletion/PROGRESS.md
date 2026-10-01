# Fungus 삭제 — 진행률 (공식 지표)

갱신: 2026-10-01 — 2층 오른쪽 4씬 Flowchart 제거. Unity EditMode 20/20, PlayMode 1/1. Codex `gpt-6-sol` 재검증 PASS. A 26/55.

2026-10-01: `BetaEnd` 제외. Flowchart 완전 제거는 **26/55씬**이다. `2floorMainHall`, `2floorRight`, `2floorRightCross`, `2floorHallway_Right`가 이 기록에 포함된다.

## 1. 전체 게임 Flowchart 제거율 (씬 기준)

| 지표 | 값 |
|------|-----|
| 제거 대상 플레이 씬 (`BetaEnd` 제외) | **55** |
| Flowchart **완전 제거** | **26** |
| **제거율 A** | **26 ÷ 55** |

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
전체 게임 제거율 **A**는 **26/55**이다. Codex `gpt-6-sol`이 2층 오른쪽 4씬을 PASS로 확인했다. Unity 실행은 구현 세션 증거이며 Codex가 직접 재실행하지는 않았다.

## 3. 잔여 (R1/R2)

| 항목 | 상태 |
|------|------|
| `Hall_playerble.unity` | Flowchart_hall+Variablemanager 제거; HallPlayableController + HallGlobalStateHost; EditMode 8/8; PlayMode 1/1; Codex PASS |
| `Opening_Office.unity` | Variablemanager 제거·HallGlobalStateHost (Hall_playerble 동일 패킷) |
| 오른쪽 복도 5씬 | `RightHallCorridorController`. Flowchart 제거. 커밋 `df007309`. Codex PASS |
| 2층 오른쪽 4씬 | `SecondFloorRightController`. Flowchart 제거. EditMode 20/20, PlayMode 1/1. Codex 재검증 PASS |

## 4. 검증 (cloud)

```bash
cd scripts && PYTHONPATH=. python3 -m pytest fungus_deletion/tests -q
dotnet test scripts/FungusDeletionSequenceTests/FungusDeletionSequenceTests.csproj
dotnet run --project scripts/CSharpSyntaxChecker/CSharpSyntaxChecker.csproj -- disputatio/Assets
PYTHONPATH=. python3 -m fungus_deletion.cli --repo-root .. inventory
```
