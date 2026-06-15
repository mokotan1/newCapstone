# Unity CLI Postflight Harness

Use this repo-local wrapper instead of relying on PATH:

```powershell
.\scripts\unity-cli-open-status-cmd.cmd
.\scripts\unity-cli.cmd --project disputatio status
```

Run these after Unity C# changes:

```powershell
.\scripts\unity-cli-open-status-cmd.cmd
.\scripts\unity-cli.cmd --project disputatio status
.\scripts\unity-cli.cmd --project disputatio editor refresh --compile
.\scripts\unity-cli.cmd --project disputatio console --type error,warning --lines 80
.\scripts\unity-cli.cmd --project disputatio test --mode EditMode --filter <TestClassName>
```

If `status` reports `Unity: not responding`, restart the Unity Editor with the
`disputatio` project open and wait for package import/compilation to finish.
