# R1 — BasementHallway Sequence 적용

- 상태: 구현·EditMode 확인, 플레이 QA·독립 리뷰 blocked (2026-09-28)
- 기준: `feature/fungus-deletion-framework`, `1acde426`; 기존 미커밋 변경 보존
- 위험도: R3 (씬 전환)
- 적용 전 확인: Unity 전체 컴파일 성공, 대상 씬과 Editor의 미저장 변경 없음

## 행동 AC

1. 지하 복도의 벽돌·추출·관찰·연구실 문과 위층 입구를 클릭하면 각 Sequence 경로가 정확한 씬으로 한 번 전환한다.
2. 해당 다섯 `ObjectClicked` 처리기는 비활성이고, `Start` 입장 페이드는 유지한다.
3. 재진입 후 입력 잠금과 화면 페이드가 복구되며 새 Console 오류가 없다.

## 허용 범위

- 소유: `disputatio/Assets/Scenes/Mokotan/Basement/BasementHallway.unity`, `Assets/godlotto/Script/Editor/FungusDeletion/BasementHallwaySequencePilot.cs`, `Assets/godlotto/Script/Interaction/GameplayScreenFade.cs`, `Assets/godlotto/Script/Sequence/{SequenceDocument,SequenceDocumentLoader}.cs`
- 테스트 쌍: `Assets/Editor/Tests/EditMode/{Interaction/{BasementHallwayInteractionControllerTests,RoomInteractionSequenceControllerTests},Sequence/SequenceDocumentLoaderTests}.cs`, `scripts/fungus_deletion/tests/test_simple_room_strip.py`
- 임포트 복구: 잘못된 GUID 6개의 `.cs.meta` (위 소유·테스트 파일과 `SayDialogSequenceHost.cs`, `SequenceBlockOutcomeMapperTests.cs`)
- 기록: 이 문서, `index.md`, `docs/architecture.md`
- 제외: `Assets/Fungus/`, 다른 씬·프리팹, 진행 중인 체크포인트 변경, 커밋·push

## 실행과 증거

`unity-cli status`는 Editor 6000.0.36f1에서 ready였다. 적용 전 활성 `Hall_playerble` 씬은 dirty/playing 모두 false였다. 처음 컴파일은 `SayDialogSequenceHost` 누락으로 실패했다. Unity Editor.log의 원인은 `.meta` GUID 6개의 길이 오류(31/33자리)와 그에 따른 에셋 임포트 제외였다. 해당 GUID는 다른 직렬화 자산에서 참조되지 않아 유효한 새 GUID로 바꾸었다. 이어 드러난 C# 오류(쓰기 전용 `ScreenFadeTexture` 읽기, Unity에서 `System.Text.Json` 타입 참조, 테스트 람다, `EventHandler` 모호성)를 수정했다.

`editor refresh --compile`: exit 0. 사전 검사에서 다섯 대상 각각 Collider2D, Clickable2D, JSON TextAsset가 있었다. 첫 파일럿 실행 후 diff 검토에서 `block.GetComponent<EventHandler>()`가 공유 Flowchart의 첫 처리기(`Start`)만 찾는 버그를 발견했다. 도구를 `block._EventHandler`와 `ParentBlock` 연결 검증으로 고치고 `Start`를 원래 상태로 복구한 후 재적용했다. 최종 저장·재로드 확인: `Start` 처리기는 활성, 다섯 문 처리기는 비활성. 컨트롤러/Host와 라우트·월드 클릭 바인딩이 각각 5개, JSON·Collider·Clickable 참조는 모두 유효하며 Missing Script는 0개였다. Unity에서 다섯 JSON 문서를 파싱·검증해 목적지 `BasementBrickRoom`, `BasementExtractionRoom`, `BasementObservationRoom`, `BasementResearchRoom`, `Basement`를 확인했다. Unity가 부가한 무관한 EventSystem/파티클 override는 씬 diff에서 제거했다.

EditMode: `BasementHallwayInteractionControllerTests` 1/1, `RoomInteractionSequenceControllerTests` 5/5, `SequenceDocumentLoaderTests` 2/2, `SequenceBlockOutcomeMapperTests` 4/4, `SequenceRouterTests` 6/6, `SequenceSessionTests` 7/7, `SequencePlayerTests` 12/12 (합계 37/37). `SequenceSessionTests` 첫 호출에서 CLI가 순간적으로 `no Unity instances running`을 반환했으나 status는 ready였고 재호출 7/7이었다. Windows 경로 수정 후 `PYTHONPATH=scripts python -m pytest scripts/fungus_deletion/tests -q`: 6/6. Console의 새 관련 오류는 확인되지 않았다.

플레이 QA는 미실행이다. `qa_status`에서 활성 프로필/실행은 없고, `qa_list`와 `registeredSceneNames`에 BasementHallway 시나리오/씬이 없어 기존 gateway로 해당 씬을 검증할 수 없다. 독립 리뷰는 Cursor wrapper가 인증 요구로 시작되지 못했다(`agent login` 또는 `CURSOR_API_KEY` 필요). `independentReview: false`.

## 재개 순서

1. ~~BasementHallway 격리 QA 경로~~ — live `qa_run` Passed.
2. ~~Hallway `Start` 페이드 C# 이전~~ — `BasementRoomEnterFade` + Flowchart 제거. `qa_run` 재통과.
3. Cursor 인증 후 R3 독립 리뷰. live QA만으로 verified가 되지 않는다.
4. 그 후 대사 이전 패킷으로 진행한다.

`verified`는 컴파일, 씬 참조, 플레이, R3 독립 리뷰가 모두 충족될 때만 기록한다.
