# Cheshire Local Gemma 4 E2B Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace cloud-backed Cheshire dialogue with local Gemma 4 E2B through LiteRT-LM, keeping Unity's SSE API and deterministic game logic.

**Architecture:** LiteRT-LM serves an OpenAI-compatible local stream. A FastAPI `LiteRtProvider` converts it to the existing internal `SSEEvent` schema; Unity calls only FastAPI. Cheshire dialogue opts into streaming and explicitly opts out of tools.

**Tech Stack:** Unity C#, FastAPI, Python `httpx`, LiteRT-LM CLI, Inno Setup, pytest, Unity EditMode tests.

**Spec:** `docs/superpowers/specs/2026-09-03-cheshire-local-gemma4-e2b-design.md`

## Global Constraints

- Windows desktop only in this release.
- Bind LiteRT-LM to `127.0.0.1:9379` and FastAPI to `127.0.0.1:8000`; do not expose either on LAN.
- Use model id `gemma4-e2b` from `litert-community/gemma-4-E2B-it-litert-lm`.
- Cheshire: text-only, no thinking, no tools, 4,096-token context, 120-token output.
- Do not remove `/tutor/grade` or delegate scoring/progression to the LLM.
- Preserve the current Unity-facing SSE JSON schema.

## Planned files

| Path | Change |
|---|---|
| `backend_ai/providers/litert_provider.py` | New adapter for LiteRT-LM OpenAI-compatible streaming. |
| `backend_ai/config.py`, `backend_ai/main.py` | Select LiteRT without cloud keys and configure loopback URLs/model. |
| `backend_ai/services/cheshire_dialogue.py` | Fixed persona, validation, and authored fallback. |
| `backend_ai/tests/test_litert_provider.py`, `test_cheshire_dialogue.py` | Provider and dialogue-policy tests. |
| `ChatHttpClient.cs`, `BaseChatbot.cs`, `ParrotChatbot.cs` | Toolless Parrot requests and SSE opt-in. |
| `SseEventFrameBuffer.cs` + EditMode tests | Correctly preserve partial SSE records. |
| `ServerConfig.cs` | Default to `http://127.0.0.1:8000/chat`. |
| `deploy/windows/*` | First-run model install, child-process startup, installer, notices. |

### Task 1: Pin and prove the LiteRT-LM runtime contract

**Files:**
- Create: `deploy/windows/litert-model-manifest.json`
- Create: `backend_ai/tests/integration/test_litert_contract.py`
- Modify: `backend_ai/README.md`

**Interfaces:**
- Produces manifest fields: `runtime_version`, `model_repo`, `model_file`, `model_id`, `server_url`.
- Requires `GET /v1/models` to list `gemma4-e2b` and `POST /v1/chat/completions` to stream.

- [ ] **Step 1: Write the failing contract test**

```python
def test_litert_server_lists_pinned_e2b(litert_server):
    response = requests.get("http://127.0.0.1:9379/v1/models", timeout=5)
    assert response.status_code == 200
    assert "gemma4-e2b" in [model["id"] for model in response.json()["data"]]
```

- [ ] **Step 2: Verify it fails before the runtime/model is present**

Run: `pytest tests/integration/test_litert_contract.py -v`

Expected: a clear missing-runtime or missing-model failure.

- [ ] **Step 3: Import and serve the exact model, then create the pinned manifest**

```powershell
litert-lm import --from-huggingface-repo=litert-community/gemma-4-E2B-it-litert-lm gemma-4-E2B-it.litertlm gemma4-e2b
litert-lm serve --host 127.0.0.1 --port 9379
```

The manifest records the verified LiteRT-LM version and model artifact hash. Re-run the test and commit with `test: pin LiteRT E2B local server contract`.

### Task 2: Add the LiteRT streaming provider

**Files:**
- Create: `backend_ai/providers/litert_provider.py`
- Modify: `backend_ai/providers/__init__.py`, `backend_ai/config.py`, `backend_ai/main.py`
- Create: `backend_ai/tests/test_litert_provider.py`
- Modify: `backend_ai/tests/test_config.py`

**Interfaces:**
- `LiteRtProvider(base_url: str, model: str)` implements `AIProvider`.
- `stream_chat(messages, tools, temperature, max_tokens) -> AsyncIterator[SSEEvent]`.

- [ ] **Step 1: Write failing provider tests**

```python
@pytest.mark.asyncio
async def test_litert_maps_openai_deltas_to_internal_events(httpx_mock):
    httpx_mock.add_response(
        url="http://127.0.0.1:9379/v1/chat/completions",
        content=b'data: {"choices":[{"delta":{"content":"체"}}]}\n\n'
                b'data: {"choices":[{"delta":{"content":"셔"}}]}\n\n'
                b'data: [DONE]\n\n',
    )
    provider = LiteRtProvider("http://127.0.0.1:9379", "gemma4-e2b")
    events = [event async for event in provider.stream_chat([{"role": "user", "content": "hi"}])]
    assert [event.content for event in events if event.type == "text_delta"] == ["체", "셔"]
    assert events[-1].type == "done"
```

- [ ] **Step 2: Run the new test**

Run: `pytest tests/test_litert_provider.py -v`

Expected: FAIL because `LiteRtProvider` does not exist.

- [ ] **Step 3: Implement provider and settings**

Provider sends `model`, `messages`, `stream: true`, `temperature`, and `max_tokens` to `/v1/chat/completions`, maps every OpenAI `delta.content` to `SSEEvent(type="text_delta")`, and emits one terminal `done`. Add:

```python
ai_provider: str = "litert"
litert_base_url: str = "http://127.0.0.1:9379"
litert_model: str = "gemma4-e2b"
litert_context_tokens: int = 4096
```

`main.py` selects LiteRT with no API key when `ai_provider == "litert"`; Groq/Gemini remain explicit development choices.

- [ ] **Step 4: Verify and commit**

Run: `pytest tests/test_litert_provider.py tests/test_config.py tests/test_chat_service.py -v`

Expected: PASS.

Commit: `feat: add LiteRT local streaming provider`.

### Task 3: Make Cheshire dialogue-only and keep tutor logic intact

**Files:**
- Create: `backend_ai/services/cheshire_dialogue.py`
- Modify: `backend_ai/services/chat_service.py`
- Create: `backend_ai/tests/test_cheshire_dialogue.py`
- Modify: `BaseChatbot.cs`, `ChatHttpClient.cs`, `ParrotChatbot.cs`

**Interfaces:**
- `CheshireDialoguePolicy.validate(text: str, fallback: str) -> str`.
- `BaseChatbot.DefaultUseTools` defaults to `true`; Parrot overrides to `false`.

- [ ] **Step 1: Write failing policy tests**

```python
def test_invalid_tool_json_uses_authored_fallback():
    policy = CheshireDialoguePolicy()
    assert policy.validate('{"name":"give_hint"}', "흥, 그쪽을 다시 봐.") == "흥, 그쪽을 다시 봐."

def test_two_sentence_korean_reply_is_accepted():
    reply = "흥, 서랍이 널 기다리는군. 서두르지 말고 다시 살펴봐."
    assert CheshireDialoguePolicy().validate(reply, "기본 대사") == reply
```

- [ ] **Step 2: Run test and implement minimum policy**

Run: `pytest tests/test_cheshire_dialogue.py -v`

Expected: FAIL first, then PASS after implementation.

Policy adds the compact persona card, prohibits invented facts and JSON, limits response to two sentences/120 tokens, and returns the game-authored fallback for empty, JSON-like, or overlong output.

- [ ] **Step 3: Add per-bot tool selection**

```csharp
// BaseChatbot.cs
protected virtual bool DefaultUseTools => true;

// ParrotChatbot.cs
protected override bool DefaultUseTools => false;
```

Expose the default through `IChatHttpCallbacks`; in both `ChatHttpClient` request paths replace `?? true` with `?? _host.DefaultUseTools`. Do not alter `TutorChatbot` defaults or `/tutor/grade`.

- [ ] **Step 4: Verify and commit**

Run: `pytest tests/test_cheshire_dialogue.py tests/test_chat_service.py tests/test_tutor_grade.py -v`

Expected: PASS.

Run Unity EditMode tests for `ChatHttpClientTests` and tutor grading. Verify Parrot sends `use_tools=false`.

Commit: `feat: constrain Cheshire to dialogue-only generation`.

### Task 4: Harden Unity SSE and enable it only for ParrotChatbot

**Files:**
- Create: `disputatio/Assets/mokotan/mokotan/script/AI/SseEventFrameBuffer.cs`
- Modify: `ChatHttpClient.cs`, `BaseChatbot.cs`, `ParrotChatbot.cs`
- Create: `disputatio/Assets/Editor/Tests/EditMode/AI/SseEventFrameBufferTests.cs`

**Interfaces:**
- `SseEventFrameBuffer.Append(string chunk) -> IEnumerable<string>` returns only complete `data:` JSON payloads.
- `BaseChatbot.PreferStreamingResponses` defaults to `false`; Parrot overrides to `true`.

- [ ] **Step 1: Write failing split-frame test**

```csharp
[Test]
public void Append_SplitFrame_EmitsOnlyWhenComplete()
{
    var buffer = new SseEventFrameBuffer();
    Assert.IsEmpty(buffer.Append("data: {\\\"type\\\":\\\"text_delta\\\",\\\"content\\\":\\\"체"));
    CollectionAssert.AreEqual(
        new[] { "{\\\"type\\\":\\\"text_delta\\\",\\\"content\\\":\\\"체셔\\\"}" },
        buffer.Append("셔\\\"}\\n\\n"));
}
```

- [ ] **Step 2: Implement the buffer and replace direct `Split('\n')` parsing**

Retain an unterminated final line between Unity polling iterations. Ignore blank/comment lines and return complete payloads without parsing their JSON. `ChatHttpClient` continues to deserialize and dispatch the existing event types.

- [ ] **Step 3: Select streaming only for Parrot**

```csharp
protected virtual bool PreferStreamingResponses => false;
// ParrotChatbot override: true
```

In both BaseChatbot send paths call `GetGPTResponseStreaming` only when the preference is true. Leave TutorChatbot's current request flow non-streaming.

- [ ] **Step 4: Verify and commit**

Run Unity EditMode tests. Live smoke test must render `체셔` exactly once when the upstream frame is split.

Commit: `feat: stream Cheshire dialogue safely over SSE`.

### Task 5: Deliver first-run local runtime installation

**Files:**
- Create: `deploy/windows/CheshireLocalLauncher.ps1`
- Create: `deploy/windows/DisputatioLocalAi.iss`
- Create: `deploy/windows/README.md`
- Create: `deploy/windows/tests/CheshireLocalLauncher.Tests.ps1`
- Modify: `ServerConfig.cs`, `backend_ai/.env.example`

**Interfaces:**
- Launcher outputs readiness JSON: `{ "ready": bool, "code": string, "message": string }`.
- Error codes: `disk_space_insufficient`, `model_download_failed`, `runtime_start_timeout`.

- [ ] **Step 1: Write failing Pester test**

```powershell
It 'reports low disk before it downloads a model' {
  $result = & $launcher -Mode Check -FreeBytesOverride 1 | ConvertFrom-Json
  $result.ready | Should -BeFalse
  $result.code | Should -Be 'disk_space_insufficient'
}
```

- [ ] **Step 2: Implement launcher lifecycle**

1. Require 15 GB free disk.
2. Show model-download consent; if accepted import the Task 1 artifact.
3. Start `litert-lm serve --host 127.0.0.1 --port 9379`.
4. Poll `/v1/models` for 60 seconds.
5. Start packaged FastAPI with `AI_PROVIDER=litert` and `127.0.0.1:8000`.
6. Poll FastAPI health for 30 seconds.
7. Write readiness JSON and launch Unity.
8. On failure, stop only child processes created by the launcher.

- [ ] **Step 3: Package and configure defaults**

Set `ServerConfig.chatUrl` to `http://127.0.0.1:8000/chat`. Inno Setup bundles runtimes, launcher, Apache-2.0 notices, and consent text, but downloads the model after consent. The environment example documents LiteRT as default and cloud keys as development-only.

- [ ] **Step 4: Verify and commit**

Run Pester tests, then use a clean Windows VM: first-run consent/download, offline second run, streamed Parrot reply, deterministic quiz grade, error recovery, and child-process cleanup.

Commit: `feat: package local Cheshire AI runtime`.

### Task 6: Gate default release on Korean dialogue quality

**Files:**
- Create: `backend_ai/tests/fixtures/cheshire_dialogue_cases.json`
- Create: `backend_ai/scripts/evaluate_cheshire_dialogue.py`
- Create: `docs/qa/cheshire-local-dialogue-evaluation.md`

**Interfaces:**
- 50 fixed prompts produce JSONL rows with prompt, streamed reply, latency, validity, and fallback flag.

- [ ] **Step 1: Add 50 cases before writing evaluator**

Cover greeting, repeats, wrong guesses, authored hints, spoiler requests, abusive input, Korean colloquialisms, tutor transitions, and unavailable-runtime fallback.

- [ ] **Step 2: Add release assertions**

```python
assert summary.valid_reply_rate >= 0.90
assert summary.tool_or_json_leak_count == 0
assert summary.invented_fact_count == 0
```

- [ ] **Step 3: Implement evaluator and human review**

Call `/chat/stream`, accumulate `text_delta` events, verify one `done`, and create a reviewer sheet scoring tone, Korean naturalness, repetition, and factual fidelity from 1 to 5.

- [ ] **Step 4: Verify and commit**

Run: `python scripts/evaluate_cheshire_dialogue.py --base-url http://127.0.0.1:8000 --cases tests/fixtures/cheshire_dialogue_cases.json`

Expected: at least 45/50 machine-valid replies, zero tool/JSON leaks, zero invented puzzle facts, and recorded human approval.

Commit: `test: add Cheshire local dialogue evaluation`.

## Self-review

- The plan covers runtime compatibility, provider replacement, deterministic game separation, SSE correctness, user installation, and dialogue quality.
- All provider calls use the existing `AIProvider.stream_chat` and all Unity stream events retain the existing schema.
- The LiteRT-LM version is deliberately pinned only after Task 1's compatibility test because its local server is fast-moving.

