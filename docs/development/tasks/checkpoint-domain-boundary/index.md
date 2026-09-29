# 체크포인트 도메인 경계 시범 적용

- 상태: 구현 반영, Unity 통합 검증 blocked. `verified` 아님.
- 기준: `docs/superpowers/specs/2026-09-28-checkpoint-domain-boundary-design.md`
- 계획: `docs/superpowers/plans/2026-09-28-checkpoint-domain-boundary.md`
- 위험도: R3 (저장 규칙 경계). `independentReview: true` (별도 Cursor 세션 `a6f76f48-bdf7-43b7-bc35-866d41f229d1`, 리뷰 완료; Unity 검증은 별도 차단).
- base revision: `1acde426`. 사용자 작업 트리의 다른 변경은 보존.
- 최종 코드 SHA-256 (2026-09-28 01:49 UTC): `CheckpointRepository.cs` `58FE58C1…5245B94B`, `CheckpointSavePolicy.cs` `C9874EBF…5EC327`, `CheckpointRepositoryTests.cs` `4FBAD2CE…E566045`, `CheckpointSavePolicyTests.cs` `C0FAC656…9E1797612FB`. `git diff --check` 오류 없음.

## AC와 변경

| AC | 반영 | 증거 |
|---|---|---|
| 저장 유효성 규칙을 Unity·Fungus 없이 실행 | `CheckpointSavePolicy.ValidateForSave` 추출 | 실제 소스 파일을 연결한 Mono probe 5/5 |
| 잘못된 저장은 기존 데이터 보존 | 저장소가 PlayerPrefs 쓰기 전에 정책 호출 | 별도 Mono 스텁 하네스에서 저장소 NUnit 10/10. Unity 회귀 테스트는 0건 실행 |
| JSON·키·기본값 유지 | `CheckpointSaveData`, 저장 키, 기본값·로드 코드는 그대로 | diff 확인, 별도 Mono 스텁 하네스의 round-trip 확인. Unity round-trip 테스트는 0건 실행 |
| 새 게임·이어하기·설정 보존 | 관련 런타임 코드 변경 없음 | 격리 플레이 QA 미실행 |

허용한 변경: `CheckpointSavePolicy.cs` 및 `.meta`, `CheckpointRepository.cs`, `CheckpointSavePolicyTests.cs` 및 `.meta`, `CheckpointRepositoryTests.cs`, 설계·계획·아키텍처·본 기록. 씬·프리팹·Fungus·기타 사용자 변경은 제외.

## 실행 기록

- Unity: `unity-cli status` exit 0, ready, Unity 6000.0.36f1, PID 38152.
- `unity-cli editor refresh --compile` exit 1. `RoomInteractionSequenceHost.cs(11,26)`에서 `SayDialogSequenceHost` CS0246. `SayDialogSequenceHost.cs`는 저장소에 있지만 Unity의 `Assembly-CSharp.rsp` 컴파일 입력 목록에 없다. 원인과 복구는 이 작업 범위 밖.
- 명시적 `AssetDatabase.ImportAsset`, 강제·동기 갱신, 파일 시각 갱신 후에도 `SayDialogSequenceHost.cs`는 AssetDatabase 목록에 나타나지 않았다. 이 추적 대상 파일의 내용과 `.meta`는 수정하지 않았다.
- 최종 테스트 변경 후 `unity-cli test --mode EditMode --filter CheckpointSavePolicyTests`와 `CheckpointRepositoryTests` 각각 exit 0, **total 0**. 이는 통과로 간주하지 않는다.
- 설치된 Unity Mono 컴파일러로 `CheckpointSaveData.cs`와 검사 프로그램만 묶은 RED: `CheckpointSavePolicy is missing`, exit 1.
- 정책 소스를 포함한 같은 컴파일 후 GREEN: `CheckpointSavePolicy probe: 5/5 passed`, exit 0.
- 실제 정책 NUnit 테스트 소스와 저장소 NUnit 테스트 소스를 각각 Unity 설치본의 Mono 컴파일러에 연결했다. 전자는 **7/7**, 후자는 PlayerPrefs·JsonUtility 등 Unity 경계에 한정한 임시 스텁에서 **10/10**, 모두 exit 0. 스텁은 `.tmp_pytest/checkpoint-domain/`에 있고 Git 제외 대상이다. 이 결과는 Unity EditMode 테스트나 실제 PlayerPrefs/JsonUtility 검증을 대체하지 않는다.
- 별도 Cursor `auto` 리뷰 세션 `a6f76f48-bdf7-43b7-bc35-866d41f229d1`: 저장 규칙 추출의 행동 차이는 찾지 못했다. 저장소 `Save(null)`과 거부된 입력의 기본값 비변경을 확인하는 테스트 2건을 요구해 추가했다. 정책 직접 테스트의 배열 내 중복과 두 허용 사례도 분리해 보강했다. 수정 후 로컬 하네스 재실행 완료; Unity 재실행은 0건.

## 다음 검증

1. Unity 컴파일 입력에서 `SayDialogSequenceHost.cs`가 누락된 이유를 별도 작업으로 복구한 뒤 전체 컴파일과 Console 오류 확인.
2. `CheckpointSavePolicyTests`, `CheckpointRepositoryTests`, `CheckpointServiceTests`, `FlagStoreCheckpointMapperTests`를 클래스별로 실행하고 실행 수 확인.
3. QA 격리 프로파일로 새 게임·저장·재진입·이어하기·설정 보존 확인.
4. 독립 리뷰 지적 사항을 반영한 최종 diff를 확인한다. Unity 검증과 플레이 QA가 끝나기 전에는 `verified`로 표시하지 않는다.
