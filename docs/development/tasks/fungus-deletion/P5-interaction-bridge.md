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

## 검증

- standalone NUnit (`/tmp/p5-sequence-tests`): Loader·Router·Session·OutcomeMapper 포함 18 pass
- `CSharpSyntaxChecker disputatio/Assets`: exit 0
- Unity EditMode `RoomInteractionSequenceControllerTests`, `SequenceDocumentLoaderTests`: Windows Editor 필요
