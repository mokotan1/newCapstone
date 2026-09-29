# 체크포인트 도메인 경계 시범 적용

작성일: 2026-09-28. 상태: 사용자가 구현 착수 승인, 검증 진행 중.

## 목적과 범위

게임 전체를 한 번에 DDD 구조로 옮기지 않는다. `Progress/Checkpoint`의 저장 규칙을 첫 시범 대상으로 삼아, 체크포인트의 유효성 판단과 Unity 저장·씬 전환을 분리한다. 사용자가 볼 수 있는 저장·이어하기 동작과 기존 JSON 형식은 유지한다.

이번 작업의 완료 조건은 다음과 같다.

1. 저장하려는 체크포인트의 유효성 규칙을 `UnityEngine`, `PlayerPrefs`, `Fungus` 없이 테스트할 수 있다.
2. 유효하지 않은 저장 요청은 기존 체크포인트를 바꾸기 전에 거부된다.
3. 현재 `Checkpoint.Latest.v1` 및 `Checkpoint.LatestId.v1` 키와 `CheckpointSaveData`의 JSON 필드·의미가 유지된다. 기존 저장 데이터는 계속 로드된다.
4. 새 게임·이어하기·방 해금 저장·설정 보존의 기존 동작을 회귀 검증한다.
5. Unity 정책의 R3 저장 변경 검증과 별도 세션의 명세·품질 리뷰를 통과한다.

제외: 씬·프리팹 수정, 새로운 저장 포맷이나 마이그레이션, 전체 인벤토리·퀘스트 재설계, 백엔드 변경, Fungus 이중 기록, 서버 구축.

## 확인한 현재 구조

- `CheckpointSaveData`는 공개 필드가 있는 Unity JSON 직렬화 모델이다. Fungus 스냅샷과 Sequence 스냅샷을 별도 배열에 담는다.
- `CheckpointRepository.Save`는 `resumeSceneName`과 Fungus 키의 공백·중복을 검사한 뒤, 일부 기본값을 채우고 `PlayerPrefs`에 쓴다. 규칙과 저장 장치가 한 클래스에 있다.
- `CheckpointLoadCoordinator`는 로드 결과에 따라 씬을 전환하고, 씬 로드 뒤 스냅샷을 적용한다.
- `ProgressSnapshotCollector`·`ProgressSnapshotApplier`는 인벤토리와 Fungus 런타임에 의존한다. `FlagStoreCheckpointMapper`는 Sequence 플래그를 직렬화 모델에 매핑한다.
- `RoomCheckpointDefinition`은 해금 키와 체크포인트 목적지를 연결한다. 현재 Sequence 이전과 Fungus 제거가 진행 중이므로 상태 소유자를 새로 복제하면 충돌한다.

기준 파일은 `disputatio/Assets/godlotto/Script/Progress/Checkpoint/`와 `disputatio/Assets/Editor/Tests/EditMode/Checkpoint/`이다. 이전 제안의 `Script/Checkpoint` 경로는 현재 저장소와 맞지 않는다.

## 선택한 설계

`CheckpointSaveData`를 당장 새 객체로 대체하지 않고, 저장 계약으로 유지한다. 그 위에 순수 C#의 체크포인트 검증 규칙을 한 곳에 둔다. 첫 구현은 현재 저장 거부 조건을 그 규칙으로 옮기는 데 집중한다. `version`과 `createdAtUtc` 기본값 채우기는 현행 저장 어댑터에 남겨 기존 시점과 직렬화 결과를 유지한다. `CheckpointRepository`는 규칙 호출 뒤 JSON 직렬화와 `PlayerPrefs` 접근을 담당한다. `CheckpointLoadCoordinator`는 씬 전환과 적용 순서를 계속 담당한다.

```text
방 해금 / 진행 갱신
       ↓
CheckpointSaveData ← 수집 어댑터(Fungus·인벤토리·Sequence)
       ↓
순수 체크포인트 규칙: 저장 가능 여부
       ↓
CheckpointRepository: JSON ↔ PlayerPrefs
       ↓
CheckpointLoadCoordinator: 씬 로드·상태 적용
```

비교한 접근:

| 접근 | 평가 |
|---|---|
| 모든 저장 모델을 불변 엔티티·값 객체로 즉시 교체 | 경계는 선명하지만 필드 매핑과 구버전 호환 위험이 크다. 첫 단계로 선택하지 않는다. |
| 현재 클래스를 그대로 두고 폴더만 `Domain`으로 이동 | 동작 위험은 낮지만 규칙과 Unity 의존성이 그대로 섞인다. |
| 현재 저장 계약을 유지하고 순수 규칙만 추출 | 첫 단계에서 실제 의존성 경계를 만들고 기존 저장을 보존할 수 있어 선택한다. |

추가 타입은 기존 규칙을 옮길 필요가 있을 때만 만든다. 계층별 추상 인터페이스, 범용 저장소, 이벤트 버스는 추가하지 않는다. `CheckpointSaveData`의 공개 필드나 enum 값은 이 단계에서 변경하지 않는다.

## 책임과 데이터 흐름

- 도메인 규칙: 씬 이름의 유효성, Fungus 스냅샷 키의 공백·중복·타입 충돌을 저장 전 검사한다. 실제 런타임 상태 수집이나 로그를 하지 않는다. Sequence 키에 대한 신규 저장 거부 규칙은 이 시범에서 추가하지 않는다. 현재 허용하는 입력을 새로 거부하지 않도록 기존 테스트와 저장 샘플을 기준으로 한다.
- 저장 어댑터: 규칙 검사 후 기존 키와 JSON으로 저장한다. 실패 시 기존 저장 키를 바꾸지 않는다. 로드시 배열의 null을 기존처럼 빈 배열로 정규화한다. 손상 JSON과 저장 없음의 구별은 별도 AC로 남긴다.
- 적용 계층: `CheckpointLoadCoordinator`의 대기 상태와 `SceneManager` 호출, Fungus·인벤토리·FlagStore 어댑터의 소유권을 유지한다. 첫 단계에서 새로운 게임 상태 소유자를 만들지 않는다.
- 동결된 이전 작업: 새 Fungus 블록·`*SceneMigrator`를 만들지 않고, `Variablemanager`와 `FlagStore`에 동일 키를 이중 기록하지 않는다.

## 구현 단위와 검증 게이트

1. 기존 `CheckpointRepositoryTests`의 저장 거부·이전 저장 보존 사례를 기준선으로 삼고, 순수 규칙을 직접 검증하는 테스트를 추가한다. 기본값 처리와 구버전 로드의 현재 동작도 고정한다.
2. `CheckpointRepository`의 저장 규칙을 순수 타입으로 옮긴다. JSON 필드와 PlayerPrefs 키는 변경하지 않는다.
3. Unity 컴파일, 관련 EditMode 테스트(`CheckpointRepositoryTests`, `CheckpointServiceTests`, 필요 시 `FlagStoreCheckpointMapperTests`), 새 Console 오류 확인을 수행한다. 테스트 0개 실행은 통과가 아니다.
4. QA 격리 프로파일로 새 게임·저장·재진입·이어하기·설정 보존을 확인한다. 정상 플레이어 저장을 검증에 사용하지 않는다.
5. 별도 세션에서 요구사항 대응과 코드 품질을 리뷰한다. 이 증거가 없으면 R3 `verified`가 아니다.

기존 사용자 변경은 건드리지 않는다. 실제 구현 전에 허용 파일을 구체적으로 확정하고 변경 중인 다른 작업과 겹치지 않는지 확인한다. 실패가 나오면 원인과 기존 기준선을 구분해 기록하고, 수정 뒤 관련 검증을 다시 수행한다.

## 결정이 필요한 경계

이 설계는 시범 적용의 목표를 **행동 변화 없는 의존성 분리**로 둔다. 손상 저장 데이터 처리, 저장 스키마 버전 전환, Fungus 제거 완료 후 상태 소유권 통합은 별도 작업으로 계획한다. 이번 시범의 결과를 본 뒤 인벤토리·퀘스트 등으로 확대할지 결정한다.
