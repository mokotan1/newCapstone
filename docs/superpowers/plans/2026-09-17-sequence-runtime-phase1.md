# Sequence Runtime Phase 1 Implementation Plan

> **2026-09-17 실행 기준 갱신:** 사용자 목표는 Kitchen 단독 이전이 아니라 **전체 씬과 공용 자산의 Fungus 완전 제거 및 프레임워크 재정립**이다. [전체 실행 계획](../plans/2026-09-17-fungus-deletion-framework-master-plan.md)의 범위·책임 분리·게이트·완료조건을 우선 적용한다. 본 문서의 단계 1은 선행 실험 S1 기록으로 보존하며, 다음 실행은 전체 계획 P0 inventory부터 시작한다. 게임 규칙의 소유자는 C# 도메인 로직이고 Sequence는 제한된 연출 실행을 맡는다.


> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans (this session is parent-inline) or superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship a Fungus-free `FlagStore` and `SequencePlayer` that executes JSON `set_bool` / `set_int` / `set_string` / `if_bool` and fails loud on bad data.

**Architecture:** Pure C# under `Godlotto.Sequence`. No `using Fungus`. `SequencePlayer.Play` walks one document; `if_bool` jumps to another block in the same document. Tests are EditMode NUnit with no scene load.

**Tech Stack:** Unity 6000.0.36f1, `JsonUtility`, NUnit EditMode, `.\scripts\unity-cli.cmd`

## Global Constraints

- Do not edit `Assets/Fungus/`, scenes, prefabs, or `*SceneMigrator`.
- Do not add Fungus `Command` subclasses.
- Do not expand `FungusDialogueQaAdapter`.
- Commits only if the user asks.
- Success of this plan is EditMode green for `FlagStoreTests` and `SequencePlayerTests`, not Fungus deletion.

---

### Task 1: FlagStore

**Files:**
- Create: `disputatio/Assets/godlotto/Script/Sequence/FlagStore.cs`
- Test: `disputatio/Assets/Editor/Tests/EditMode/Sequence/FlagStoreTests.cs`

**Interfaces:**
- Consumes: nothing
- Produces: `Godlotto.Sequence.FlagStore` with `GetBool`/`SetBool`/`GetInt`/`SetInt`/`GetString`/`SetString`/`Clear`/`Has`. Empty or null keys throw `System.ArgumentException`.

- [ ] **Step 1: Write the failing test**

```csharp
using Godlotto.Sequence;
using NUnit.Framework;
using System;

[TestFixture]
public class FlagStoreTests
{
    [Test]
    public void SetBool_GetBool_RoundTrips()
    {
        var store = new FlagStore();
        store.SetBool("GetBottle", true);
        Assert.That(store.GetBool("GetBottle"), Is.True);
        Assert.That(store.Has("GetBottle"), Is.True);
    }

    [Test]
    public void GetBool_MissingKey_ReturnsDefault()
    {
        var store = new FlagStore();
        Assert.That(store.GetBool("missing"), Is.False);
        Assert.That(store.GetBool("missing", true), Is.True);
    }

    [Test]
    public void SetInt_And_SetString_RoundTrip()
    {
        var store = new FlagStore();
        store.SetInt("CorrectAnswerCount", 3);
        store.SetString("SceneName", "Kitchen");
        Assert.That(store.GetInt("CorrectAnswerCount"), Is.EqualTo(3));
        Assert.That(store.GetString("SceneName"), Is.EqualTo("Kitchen"));
    }

    [Test]
    public void Clear_RemovesAllKeys()
    {
        var store = new FlagStore();
        store.SetBool("GetBottle", true);
        store.Clear();
        Assert.That(store.Has("GetBottle"), Is.False);
    }

    [Test]
    public void SetBool_EmptyKey_Throws()
    {
        var store = new FlagStore();
        Assert.Throws<ArgumentException>(() => store.SetBool("", true));
        Assert.Throws<ArgumentException>(() => store.SetBool(null, true));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `.\scripts\unity-cli-open-status-cmd.cmd` then `.\scripts\unity-cli.cmd --project disputatio test --mode EditMode --filter FlagStoreTests`

Expected: compile error `FlagStore` does not exist, or test class missing.

- [ ] **Step 3: Write minimal implementation**

```csharp
using System;
using System.Collections.Generic;

namespace Godlotto.Sequence
{
    public sealed class FlagStore
    {
        readonly Dictionary<string, bool> bools = new Dictionary<string, bool>();
        readonly Dictionary<string, int> ints = new Dictionary<string, int>();
        readonly Dictionary<string, string> strings = new Dictionary<string, string>();

        public bool Has(string key)
        {
            RequireKey(key);
            return bools.ContainsKey(key) || ints.ContainsKey(key) || strings.ContainsKey(key);
        }

        public bool GetBool(string key, bool defaultValue = false)
        {
            RequireKey(key);
            return bools.TryGetValue(key, out bool value) ? value : defaultValue;
        }

        public void SetBool(string key, bool value)
        {
            RequireKey(key);
            bools[key] = value;
        }

        public int GetInt(string key, int defaultValue = 0)
        {
            RequireKey(key);
            return ints.TryGetValue(key, out int value) ? value : defaultValue;
        }

        public void SetInt(string key, int value)
        {
            RequireKey(key);
            ints[key] = value;
        }

        public string GetString(string key, string defaultValue = "")
        {
            RequireKey(key);
            return strings.TryGetValue(key, out string value) ? value : defaultValue;
        }

        public void SetString(string key, string value)
        {
            RequireKey(key);
            strings[key] = value ?? "";
        }

        public void Clear()
        {
            bools.Clear();
            ints.Clear();
            strings.Clear();
        }

        static void RequireKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Flag key must be non-empty.", nameof(key));
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `.\scripts\unity-cli.cmd --project disputatio editor refresh --compile` then `--filter FlagStoreTests`

Expected: all FlagStoreTests passed.

- [ ] **Step 5: Commit**

Skip unless the user asks.

---

### Task 2: Sequence document parse

**Files:**
- Create: `disputatio/Assets/godlotto/Script/Sequence/SequenceDocument.cs`
- Modify: `disputatio/Assets/Editor/Tests/EditMode/Sequence/SequencePlayerTests.cs` (create; parse tests live here)

**Interfaces:**
- Consumes: Unity `JsonUtility`
- Produces: `SequenceDocument.FromJson(string json)`, `TryGetBlock(string blockId, out SequenceBlock block)`, serializable `SequenceBlock` / `SequenceOp` with fields `command`, `key`, `bool_value`, `int_value`, `string_value`, `then_block`, `else_block`

- [ ] **Step 1: Write the failing parse tests** (in `SequencePlayerTests`)

```csharp
using Godlotto.Sequence;
using NUnit.Framework;

[TestFixture]
public class SequencePlayerTests
{
    const string TwoBlockJson =
        "{\"schema_version\":1,\"blocks\":["
        + "{\"block_id\":\"start\",\"commands\":["
        + "{\"command\":\"set_bool\",\"key\":\"GetBottle\",\"bool_value\":true}"
        + "]},"
        + "{\"block_id\":\"other\",\"commands\":[]}"
        + "]}";

    [Test]
    public void FromJson_ReadsBlockIdAndCommand()
    {
        SequenceDocument doc = SequenceDocument.FromJson(TwoBlockJson);
        Assert.That(doc.schema_version, Is.EqualTo(1));
        Assert.That(doc.TryGetBlock("start", out SequenceBlock block), Is.True);
        Assert.That(block.commands[0].command, Is.EqualTo("set_bool"));
        Assert.That(block.commands[0].key, Is.EqualTo("GetBottle"));
        Assert.That(block.commands[0].bool_value, Is.True);
    }

    [Test]
    public void FromJson_Empty_ReturnsEmptyDocument()
    {
        SequenceDocument doc = SequenceDocument.FromJson("");
        Assert.That(doc.blocks.Length, Is.EqualTo(0));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `.\scripts\unity-cli.cmd --project disputatio test --mode EditMode --filter SequencePlayerTests`

Expected: `SequenceDocument` missing.

- [ ] **Step 3: Write SequenceDocument**

```csharp
using System;
using UnityEngine;

namespace Godlotto.Sequence
{
    [Serializable]
    public sealed class SequenceDocument
    {
        public int schema_version = 1;
        public SequenceBlock[] blocks = Array.Empty<SequenceBlock>();

        public static SequenceDocument FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new SequenceDocument();

            SequenceDocument doc = JsonUtility.FromJson<SequenceDocument>(json);
            if (doc == null)
                return new SequenceDocument();
            if (doc.blocks == null)
                doc.blocks = Array.Empty<SequenceBlock>();
            return doc;
        }

        public bool TryGetBlock(string blockId, out SequenceBlock block)
        {
            block = null;
            if (string.IsNullOrWhiteSpace(blockId) || blocks == null)
                return false;

            for (int i = 0; i < blocks.Length; i++)
            {
                SequenceBlock candidate = blocks[i];
                if (candidate != null && string.Equals(candidate.block_id, blockId, StringComparison.Ordinal))
                {
                    block = candidate;
                    return true;
                }
            }

            return false;
        }
    }

    [Serializable]
    public sealed class SequenceBlock
    {
        public string block_id;
        public SequenceOp[] commands = Array.Empty<SequenceOp>();
    }

    [Serializable]
    public sealed class SequenceOp
    {
        public string command;
        public string key;
        public bool bool_value;
        public int int_value;
        public string string_value;
        public string then_block;
        public string else_block;
    }
}
```

- [ ] **Step 4: Run parse tests**

Expected: `FromJson_*` pass. `Play` tests may still be absent.

- [ ] **Step 5: Commit**

Skip unless the user asks.

---

### Task 3: SequencePlayer set_* and unknown command

**Files:**
- Create: `disputatio/Assets/godlotto/Script/Sequence/SequencePlayException.cs`
- Create: `disputatio/Assets/godlotto/Script/Sequence/SequencePlayer.cs`
- Modify: `disputatio/Assets/Editor/Tests/EditMode/Sequence/SequencePlayerTests.cs`

**Interfaces:**
- Consumes: `SequenceDocument`, `FlagStore`
- Produces: `SequencePlayer.Play(SequenceDocument document, string blockId)` returns `void`; mutates the injected `FlagStore`. Throws `SequencePlayException` or `ArgumentNullException`.

- [ ] **Step 1: Add failing Play tests** to `SequencePlayerTests`

```csharp
    [Test]
    public void Play_SetBool_WritesFlagStore()
    {
        var flags = new FlagStore();
        var player = new SequencePlayer(flags);
        player.Play(SequenceDocument.FromJson(TwoBlockJson), "start");
        Assert.That(flags.GetBool("GetBottle"), Is.True);
    }

    [Test]
    public void Play_UnknownCommand_Throws()
    {
        const string json =
            "{\"blocks\":[{\"block_id\":\"x\",\"commands\":[{\"command\":\"explode\"}]}]}";
        var player = new SequencePlayer(new FlagStore());
        var ex = Assert.Throws<SequencePlayException>(
            () => player.Play(SequenceDocument.FromJson(json), "x"));
        StringAssert.Contains("explode", ex.Message);
        StringAssert.Contains("x", ex.Message);
    }

    [Test]
    public void Play_MissingBlock_Throws()
    {
        var player = new SequencePlayer(new FlagStore());
        Assert.Throws<SequencePlayException>(
            () => player.Play(SequenceDocument.FromJson(TwoBlockJson), "nope"));
    }
```

Also add tests for `set_int` and `set_string` in the same fixture.

- [ ] **Step 2: Run to verify fail**

Expected: `SequencePlayer` missing.

- [ ] **Step 3: Implement SequencePlayer** (unknown command + set_* only; `if_bool` in Task 4)

See Task 4 for the full class so later tasks do not drift. Implement `if_bool` in Task 4; until then `if_bool` may throw as unknown.

- [ ] **Step 4: Run SequencePlayerTests set_* cases**

Expected: set/unknown/missing pass.

- [ ] **Step 5: Commit**

Skip unless the user asks.

---

### Task 4: if_bool jump and cycle detection

**Files:**
- Modify: `disputatio/Assets/godlotto/Script/Sequence/SequencePlayer.cs`
- Modify: `disputatio/Assets/Editor/Tests/EditMode/Sequence/SequencePlayerTests.cs`

**Interfaces:**
- Consumes: `SequenceOp.then_block`, `SequenceOp.else_block`, `FlagStore.GetBool`
- Produces: `Play` executes the taken branch in the same call. Visiting a `block_id` already on the stack throws `SequencePlayException`.

- [ ] **Step 1: Write failing branch tests**

```csharp
    const string BranchJson =
        "{\"blocks\":["
        + "{\"block_id\":\"gate\",\"commands\":["
        + "{\"command\":\"if_bool\",\"key\":\"GetBottle\",\"then_block\":\"yes\",\"else_block\":\"no\"}"
        + "]},"
        + "{\"block_id\":\"yes\",\"commands\":[{\"command\":\"set_int\",\"key\":\"path\",\"int_value\":1}]},"
        + "{\"block_id\":\"no\",\"commands\":[{\"command\":\"set_int\",\"key\":\"path\",\"int_value\":2}]}"
        + "]}";

    [Test]
    public void Play_IfBool_TakesThenWhenTrue()
    {
        var flags = new FlagStore();
        flags.SetBool("GetBottle", true);
        new SequencePlayer(flags).Play(SequenceDocument.FromJson(BranchJson), "gate");
        Assert.That(flags.GetInt("path"), Is.EqualTo(1));
    }

    [Test]
    public void Play_IfBool_TakesElseWhenFalse()
    {
        var flags = new FlagStore();
        new SequencePlayer(flags).Play(SequenceDocument.FromJson(BranchJson), "gate");
        Assert.That(flags.GetInt("path"), Is.EqualTo(2));
    }

    [Test]
    public void Play_IfBool_Cycle_Throws()
    {
        const string json =
            "{\"blocks\":[{\"block_id\":\"loop\",\"commands\":["
            + "{\"command\":\"if_bool\",\"key\":\"GetBottle\",\"then_block\":\"loop\",\"else_block\":\"loop\"}"
            + "]}]}";
        var player = new SequencePlayer(new FlagStore());
        Assert.Throws<SequencePlayException>(
            () => player.Play(SequenceDocument.FromJson(json), "loop"));
    }
```

- [ ] **Step 2: Run to verify fail** (if_bool still unknown or no jump)

- [ ] **Step 3: Full SequencePlayer**

```csharp
using System;
using System.Collections.Generic;

namespace Godlotto.Sequence
{
    public sealed class SequencePlayer
    {
        readonly FlagStore flags;

        public SequencePlayer(FlagStore flags)
        {
            this.flags = flags ?? throw new ArgumentNullException(nameof(flags));
        }

        public void Play(SequenceDocument document, string blockId)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));
            PlayBlock(document, blockId, new HashSet<string>(StringComparer.Ordinal));
        }

        void PlayBlock(SequenceDocument document, string blockId, HashSet<string> stack)
        {
            if (string.IsNullOrEmpty(blockId))
                throw new SequencePlayException("block_id must be non-empty.");
            if (!stack.Add(blockId))
                throw new SequencePlayException("Cycle detected at block '" + blockId + "'.");
            if (!document.TryGetBlock(blockId, out SequenceBlock block))
                throw new SequencePlayException("Unknown block_id '" + blockId + "'.");

            SequenceOp[] ops = block.commands ?? Array.Empty<SequenceOp>();
            for (int i = 0; i < ops.Length; i++)
            {
                SequenceOp op = ops[i];
                if (op == null || string.IsNullOrEmpty(op.command))
                    throw new SequencePlayException("Empty command in block '" + blockId + "'.");
                Execute(document, blockId, op, stack);
            }

            stack.Remove(blockId);
        }

        void Execute(SequenceDocument document, string blockId, SequenceOp op, HashSet<string> stack)
        {
            switch (op.command)
            {
                case "set_bool":
                    flags.SetBool(op.key, op.bool_value);
                    return;
                case "set_int":
                    flags.SetInt(op.key, op.int_value);
                    return;
                case "set_string":
                    flags.SetString(op.key, op.string_value);
                    return;
                case "if_bool":
                    string next = flags.GetBool(op.key) ? op.then_block : op.else_block;
                    if (string.IsNullOrEmpty(next))
                        throw new SequencePlayException(
                            "if_bool in '" + blockId + "' missing then_block/else_block.");
                    PlayBlock(document, next, stack);
                    return;
                default:
                    throw new SequencePlayException(
                        "Unknown command '" + op.command + "' in block '" + blockId + "'.");
            }
        }
    }
}
```

```csharp
using System;

namespace Godlotto.Sequence
{
    public sealed class SequencePlayException : Exception
    {
        public SequencePlayException(string message) : base(message)
        {
        }
    }
}
```

- [ ] **Step 4: Run** `.\scripts\unity-cli.cmd --project disputatio test --mode EditMode --filter SequencePlayerTests` and `--filter FlagStoreTests`

Expected: all passed. Console: no new compile errors from these files.

- [ ] **Step 5: Commit**

Skip unless the user asks.

---

### Task 5: Docs for Astra

**Files:**
- Modify: `docs/architecture.md` (동결 배너, Sequence 경로, 새 Fungus 작업 금지)
- Modify: `docs/development/tasks/fungus-deletion/index.md` (검증 결과)

- [ ] **Step 1:** architecture.md에 2026-09-17 동결 배너와 `Assets/godlotto/Script/Sequence/` 행을 넣는다. §7 “전역 Flowchart 배치” 옆에 단계 1 동안 새 Flowchart를 추가하지 말라는 한 줄을 넣는다.
- [ ] **Step 2:** task index 증거 칸에 실행한 unity-cli 명령과 pass 수를 적는다.
- [ ] **Step 3:** Commit only if the user asks.

## Spec coverage

| Spec § | Task |
|--------|------|
| §4 no Fungus in Sequence | 1–4 (`Godlotto.Sequence`, no `using Fungus`) |
| §8 set_* / if_bool / unknown / cycle | 3–4 |
| §7 freeze | Global constraints; no scene/migrator files |
| §11 EditMode filters | Task 4 step 4 |
| §13 Astra share | Spec + this plan + task index + architecture banner |

## Placeholder scan

None intended. talk/menu/Kitchen are out of this plan by spec §8.
