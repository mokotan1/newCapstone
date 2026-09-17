# P4-sequence-routing / interactionId → Sequence 문서

- state: done (Unity EditMode 남음)
- phase: P4 (R1)
- allowedFiles: `disputatio/Assets/godlotto/Script/Sequence/**`, `disputatio/Assets/Editor/Tests/EditMode/Sequence/SequenceRouterTests.cs`, `docs/architecture.md`, 본 task 폴더
- excluded: 씬/프리팹, RoomInteractionController 개조, Fungus 블록 실행, FlagStore 싱글톤, Kitchen Flowchart 제거

## 실행

- [x] AC 확정
- [x] 실패 테스트 (빈 Router, 미등록 Play가 예외 없음)
- [x] Catalog 검증 등록 + Router Play
- [ ] Unity EditMode `--filter SequenceRouterTests` (Editor 없음)
- [x] independentReview: false

## 증거

- RED: standalone NUnit 6 failed / 38 passed
- GREEN: standalone NUnit 44 passed / 0 failed. Unity 미실행
- CSharpSyntaxChecker: exit 0
