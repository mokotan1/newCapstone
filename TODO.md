# TODO — Fungus 삭제 P3

## 목표
Sequence wait/say를 호스트에 넘기고, 세션 재생 동안만 입력을 잠근다.

## 진행 중인 항목
- P3 비동기·UI 계약. Unity EditMode는 이 환경에서 미실행.

## 남은 항목
- Windows unity-cli EditMode: SequenceSessionTests 포함
- 씬 이전. FlagStore 싱글톤 금지
- Unity SayDialog 호스트 연결

## 검증 결과
- standalone NUnit `/tmp/p3-async-ui-tests`: RED 7 fail/31 pass → GREEN 38 pass
- CSharpSyntaxChecker: exit 0. Tests 3 pass
- Unity compile/EditMode: blocked (no Editor)
- independentReview: false
