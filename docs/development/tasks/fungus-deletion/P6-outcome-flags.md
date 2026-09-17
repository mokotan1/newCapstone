# P6-outcome-flags / Sequence 종료 outcome (플래그)

- state: done (cloud)
- phase: P6
- allowedFiles: `SequenceBlockOutcomeMapper.cs`, `RoomInteractionController.ApplySequenceOutcomes`, `RoomInteractionSequenceHost.TryPlay` 후처리

## AC

- 문서에서 `set_bool` / `set_string`으로 예약 키에 쓰면 Fungus `BlockOutcome`과 동일하게 씬 전환·뒤로가기
- 전용 `load_scene` 명령 없음 (스키마 v1 유지)
- 재생 직후 `SequenceBlockOutcomeMapper.Clear`로 outcome 키 제거

## 예약 키

| 키 | 타입 | 동작 |
|----|------|------|
| `__sequence.outcome.go_back` | bool | `BackNavigator` / 고정 복귀 씬 |
| `__sequence.outcome.load_scene` | string | `SceneTransitionService.LoadSceneSafely` |

## 검증

- `SequenceBlockOutcomeMapperTests` (standalone + Unity EditMode)
- `RoomInteractionSequenceControllerTests.OnInteraction_SequenceRoute_LoadSceneOutcome_InvokesSceneHandler`
