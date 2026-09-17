# TODO — Fungus 삭제 P4

## 목표
interactionId → Sequence 문서 라우팅. Fungus 블록을 호출하지 않음.

## 진행 중인 항목
- P4 Sequence 라우팅. Unity EditMode는 이 환경에서 미실행.

## 남은 항목
- Windows unity-cli EditMode: SequenceRouterTests 포함
- RoomInteractionController 연결 (씬 YAML 변경 없이)
- Unity SayDialog 호스트
- 씬 이전. FlagStore 싱글톤 금지

## 검증 결과
- standalone NUnit `/tmp/p4-routing-tests`: RED 6 fail/38 pass → GREEN 44 pass
- CSharpSyntaxChecker: exit 0. Tests 3 pass
- Unity compile/EditMode: blocked (no Editor)
- independentReview: false
