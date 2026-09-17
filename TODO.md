# TODO — Fungus 삭제 P5/P6

## 목표
RoomInteraction Sequence 브리지 + outcome 예약 플래그.

## 달성
- P5: InteractionRoute sequence 필드, Loader, SequenceHost, SayDialog 호스트, Controller 연동
- P6: SequenceBlockOutcomeMapper + ApplySequenceOutcomes

## 남은 항목
- Windows Unity EditMode: RoomInteractionSequenceControllerTests, SequenceDocumentLoaderTests, SequenceBlockOutcomeMapperTests
- 씬 Inspector에 Sequence JSON (승인된 마이그레이션만)
- P0 inventory/scanner (원격 미포함)

## 검증
- standalone `/tmp/p5-sequence-tests`: 18 pass
- CSharpSyntaxChecker disputatio/Assets: exit 0
- Unity EditMode: blocked (no Editor)
- independentReview: false
