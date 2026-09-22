# P1-sequence-contract / FlagStore 타입 정책과 Sequence 사전 검증

- state: done (Unity EditMode 남음)
- phase: P1 (R1)
- allowedFiles: `disputatio/Assets/godlotto/Script/Sequence/**`, `disputatio/Assets/Editor/Tests/EditMode/Sequence/**`, `docs/architecture.md`, 본 task 폴더
- excluded: 씬/프리팹, Variablemanager 이중 기록, Kitchen Flowchart 제거, 벤더 패치, 커밋은 cloud 절차로 수행

## 실행

- [x] AC 확정
- [x] 실패 테스트 (타입 충돌, missing get, 잘못된 문서 부작용) 확인
- [x] FlagStore 타입 바인딩 + SequenceValidator
- [ ] Unity EditMode `--filter FlagStoreTests` / `SequencePlayerTests` (Editor 없음)
- [x] independentReview: false

## 증거

- RED: xunit 6 failed (No exception / Has touched == true)
- GREEN: xunit passed (전체). Unity 미실행
