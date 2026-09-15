# Unity CLI Postflight (legacy wrapper)

완료 기준은 `.harness/unity-verification.md`다. 위험도와 소유권은 `.harness/unity-policy.md`다. 활성 backend는 `.harness/unity-toolchain.json`이다.

Use this repo-local wrapper instead of relying on PATH:

```powershell
.\scripts\unity-cli-open-status-cmd.cmd
.\scripts\unity-cli.cmd --project disputatio status
.\scripts\unity-cli.cmd --project disputatio editor refresh --compile
.\scripts\unity-cli.cmd --project disputatio console --type error,warning --lines 80
.\scripts\unity-cli.cmd --project disputatio test --mode EditMode --filter <TestClassName>
```

`--filter`는 테스트 클래스 전체 이름이다. 매칭 0개는 검증 성공이 아니다.
If `status` reports `Unity: not responding`, do not retry mutation blindly; inspect the running command first.
