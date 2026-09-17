# P5-interaction-bridge / RoomInteraction ↔ Sequence

- state: done (cloud)
- phase: P5
- allowedFiles: `Interaction/RoomInteractionController.cs`, `RoomInteractionSequenceHost.cs`, `SayDialogSequenceHost.cs`, `Sequence/SequenceDocumentLoader.cs`, EditMode Interaction·Sequence 테스트, `docs/**`

## AC

- `InteractionRoute`에 `sequenceDocument`(TextAsset) + `sequenceStartBlock`이 있으면 Fungus 블록 대신 Sequence 재생
- Sequence 라우트 실패 시 Fungus 폴백 없음 (로그만)
- Fungus-only 라우트(`fungusBlockName`만) 회귀 없음
- `RoomInteractionSequenceHost`: 씬 로컬 `FlagStore` + `SequenceCatalog` (전역 싱글톤 금지)
- `SayDialogSequenceHost`: `say` → Fungus `SayDialog` (Interaction/Fungus 참조는 Interaction 층만)

## Inspector 연결 (씬 YAML 일괄 변경 없이)

1. `RoomInteractionController`와 같은 GameObject에 `RoomInteractionSequenceHost` + (선택) `SayDialogSequenceHost` 추가
2. `InteractionRoute` 한 줄에 `interactionId`, `sequenceDocument`(예: `Assets/godlotto/SequenceExamples/*.json`), `sequenceStartBlock`(`start`) 지정
3. Fungus 블록명은 비우거나 유지해도 Sequence 필드가 있으면 Fungus는 실행되지 않음

## 검증

- standalone NUnit (`/tmp/p5-sequence-tests`): Loader·Router·Session·OutcomeMapper 포함 18 pass
- `CSharpSyntaxChecker disputatio/Assets`: exit 0
- Unity EditMode `RoomInteractionSequenceControllerTests`, `SequenceDocumentLoaderTests`: Windows Editor 필요
