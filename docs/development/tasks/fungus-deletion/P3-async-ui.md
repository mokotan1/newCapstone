# P3-async-ui / wait·say 호스트와 입력 게이트

- state: running
- phase: P3 (R1)
- allowedFiles: `disputatio/Assets/godlotto/Script/Sequence/**`, `disputatio/Assets/godlotto/Script/Interaction/SequenceInputGateLock.cs`, `disputatio/Assets/Editor/Tests/EditMode/Sequence/SequenceSessionTests.cs`, `docs/architecture.md`, 본 task 폴더
- excluded: 씬/프리팹, SayDialog 연결, Variablemanager 이중 기록, FlagStore 싱글톤, Kitchen Flowchart 제거, 벤더 패치

## 실행

- [x] AC 확정
- [x] 실패 테스트 (빈 Session, wait는 invalid_document, 호스트 미호출)
- [x] SequenceSession + wait/say 검증 + 입력 잠금
- [ ] Unity EditMode `--filter SequenceSessionTests` (Editor 없음)
- [x] independentReview: false

## 증거

- RED: standalone NUnit 7 failed / 31 passed (빈 Session, wait는 invalid_document)
- GREEN: standalone NUnit 38 passed / 0 failed. Unity 미실행 (Editor 없음)
- CSharpSyntaxChecker: exit 0. CSharpSyntaxChecker.Tests: 3 passed
