---
name: newcapstone-unity-automation
description: >-
  Use after importing unity-cli or when working with Unity automation in
  newCapstone/disputatio. Chooses between unity-cli shell commands and the
  existing Unity MCP package for tests, console logs, play mode, scene/prefab
  changes, C# exec, and editor inspection.
---

# newCapstone Unity Automation

## When to use

Use this skill for Unity-side work in `newCapstone`, especially under
`disputatio/`, after `unity-cli` has been imported or when deciding whether to
use CLI commands or Unity MCP.

Always pair this with `newcapstone-architecture` for implementation work. Read
`docs/architecture.md` before changing game code, scenes, prefabs, AI chat,
checkpoints, or tests.

## Project facts

- Unity project root: `disputatio/`
- Unity version: `6000.0.36f1`
- Existing MCP package: `com.coplaydev.unity-mcp` in `disputatio/Packages/manifest.json`
- Expected unity-cli connector after import:
  `com.youngwoocho02.unity-cli-connector`
- Core game code: `disputatio/Assets/godlotto/Script/`
- AI chat client code: `disputatio/Assets/mokotan/mokotan/script/AI/`
- EditMode tests: `disputatio/Assets/Editor/Tests/EditMode/`
- Main scenes: `disputatio/Assets/Scenes/`

## Routing

Before using `unity-cli`, confirm both sides exist:

```powershell
.\scripts\unity-cli-open-status-cmd.cmd
.\scripts\unity-cli.cmd --project disputatio status
```

AI 에이전트가 Unity CLI를 사용한다면, 첫 CLI 명령 전에 반드시 `.\scripts\unity-cli-open-status-cmd.cmd`로 별도 CMD 상태창을 띄운다. 이미 열린 상태창이 보이면 새로 띄우지 않아도 된다.

If the command is missing or Unity reports no connector, do not assume the
import is complete. Ask the user to open `disputatio` in Unity and import the
connector package first:

```text
https://github.com/youngwoocho02/unity-cli.git?path=unity-connector
```

Prefer `unity-cli` for repeatable command-line automation:

| Task | Use |
|------|-----|
| Show live Unity CLI status in CMD | `.\scripts\unity-cli-open-status-cmd.cmd` |
| Check Unity connection | `.\scripts\unity-cli.cmd --project disputatio status` |
| Run EditMode tests | `.\scripts\unity-cli.cmd --project disputatio test --mode EditMode` |
| Run PlayMode tests | `.\scripts\unity-cli.cmd --project disputatio test --mode PlayMode` |
| Read compile/runtime errors | `.\scripts\unity-cli.cmd --project disputatio console --type error,warning --lines 80` |
| Enter/exit play mode | `.\scripts\unity-cli.cmd --project disputatio editor play --wait` / `editor stop` |
| Refresh assets or compile | `.\scripts\unity-cli.cmd --project disputatio editor refresh --compile` |
| Re-save changed scenes/prefabs/assets | `.\scripts\unity-cli.cmd --project disputatio reserialize <asset-path>` |
| Query Unity state with short C# | `.\scripts\unity-cli.cmd --project disputatio exec "<code>"` |
| CI-like verification loop | `.\scripts\unity-cli.cmd` |

Prefer Unity MCP for interactive editor inspection:

| Task | Use |
|------|-----|
| Browse scene hierarchy visually/structurally | Unity MCP |
| Inspect selected GameObjects and components | Unity MCP |
| Make guided editor changes from an MCP-capable AI client | Unity MCP |
| Use tool schemas/permissions instead of shell commands | Unity MCP |
| Work when `unity-cli` is not installed yet | Unity MCP or normal Unity Editor |

## Safety rules

- Do not treat YAML scene/prefab editing as the default path.
- Prefer C# code, editor utilities, `unity-cli exec`, or MCP tools for object
  creation, component wiring, references, and scene hierarchy changes.
- Direct YAML edits are acceptable only for small, obvious serialized value
  changes. Afterward run `reserialize` on the changed assets and check console
  errors.
- Avoid edits under `disputatio/Assets/Fungus/` and other vendor asset folders
  unless the user explicitly asks.
- Use constants such as `SceneNames` and `FungusVariableKeys`; avoid new magic
  strings for scenes or Fungus variables.
- Do not hardcode API keys or private server secrets. Use existing config and
  environment variable patterns.

## Mandatory postflight (에이전트 필수)

Cursor 규칙 `.cursor/rules/unity-verification-postflight.mdc`를 따른다.
**코드·씬·프리팹 변경 후** 완료 응답 전에 아래를 실행한다. `CSharpSyntaxChecker`만으로는 부족하다.

### CLI 경로 (Windows)

```powershell
.\scripts\unity-cli-open-status-cmd.cmd
.\scripts\unity-cli.cmd --project disputatio status
```

### 검증 순서

```powershell
.\scripts\unity-cli-open-status-cmd.cmd
.\scripts\unity-cli.cmd --project disputatio status
.\scripts\unity-cli.cmd --project disputatio editor refresh --compile    # C# 변경 시
.\scripts\unity-cli.cmd --project disputatio console --type error,warning --lines 80
.\scripts\unity-cli.cmd --project disputatio test --mode EditMode --filter <TestClassName>
```

`--filter`는 **테스트 클래스 전체 이름** (예: `BedRoomInteractionControllerTests`).
부분 문자열·정규식·`|` 조합은 동작하지 않는다.

### Interaction 하네스 필터

| 변경 | `--filter` |
|------|------------|
| BedRoom | `BedRoomInteractionControllerTests` |
| CorridorEntrance | `CorridorEntranceControllerTests` |
| Kitchen | `KitchenInteractionControllerTests` |
| OpeningMention | `OpeningMentionControllerTests` |
| ChildRoom | `ChildRoomPuzzleControllerTests` |
| MaidRoom | `MaidRoomPuzzleControllerTests` |
| StudyRoom | `StudyRoomPuzzleControllerTests` |
| WifeRoom | `WifeRoomPuzzleControllerTests` |
| InventoryGuide | `InventoryGuideControllerTests` |
| `Interaction/` 공통 타입 | 위 8개 Interaction 테스트 **각각** 실행 |

신규 Controller는 대응 `*Tests`를 `Assets/Editor/Tests/EditMode/`에 추가한 뒤 그 클래스로 `--filter`한다.

Unity MCP(`validate_script`, `read_console`)는 **보조**. MCP 실패 + `status` ready이면 CLI만으로 계속 검증한다.

## Standard workflow

1. Read `docs/architecture.md` and map the task to the documented folder.
2. Check whether Unity is reachable:

   ```powershell
   .\scripts\unity-cli-open-status-cmd.cmd
   .\scripts\unity-cli.cmd --project disputatio status
   ```

3. If `unity-cli` is unavailable, say so and fall back to Unity MCP or explain
   that the Unity Editor must be opened/imported first.
4. Make the smallest scoped code or asset change.
5. Run **Mandatory postflight** (위 절) — 전체 EditMode 대신 **좁은 `--filter`** 우선.
6. For scene/prefab/asset changes, reserialize only changed assets when
   possible:

   ```powershell
   .\scripts\unity-cli.cmd --project disputatio reserialize Assets/Scenes/Mokotan/Opening_Office.unity
   ```

7. Summarize what changed, **which commands ran and pass/fail counts**, and any
   remaining Unity Editor steps.

## Useful focused commands

Use filters to keep outputs small:

```powershell
.\scripts\unity-cli.cmd --project disputatio test --mode EditMode --filter ServerConfig
.\scripts\unity-cli.cmd --project disputatio test --mode EditMode --filter Checkpoint
.\scripts\unity-cli.cmd --project disputatio console --type error --lines 40
.\scripts\unity-cli.cmd --project disputatio exec "return UnityEditor.EditorSceneManagement.EditorSceneManager.GetActiveScene().path;"
```

When shell escaping becomes awkward, prefer a small temporary editor utility or
MCP tool over sending a long `exec` string.
