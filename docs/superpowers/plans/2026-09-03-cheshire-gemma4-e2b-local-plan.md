# Cheshire Gemma 4 E2B Local Dialogue Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the cloud LLM dependency for Cheshire's short dialogue generation with local Gemma 4 E2B while preserving the existing Unity-facing API and SSE stream.

**Architecture:** The first release is Windows desktop only. Unity continues calling the local FastAPI `/chat` and `/chat/stream` endpoints; FastAPI calls a local Gemma 4 E2B runtime through a provider adapter. Dialogue requests are text-only and have tools disabled; quiz grading, puzzle state, emotions, and game actions remain deterministic application logic.

**Tech Stack:** Python 3, FastAPI, httpx, LiteRT-LM (preferred local runtime; Ollama-compatible fallback may be evaluated), Unity C#, UnityWebRequest, existing SSEEvent schema, pytest, Unity EditMode tests.

**Spec:** This plan records the approved direction from the conversation; no separate design document has been created yet.

## Global Constraints

- Use `gemma4:e2b-it-qat`/the equivalent LiteRT-LM E2B mobile-optimized artifact for the desktop MVP; do not use Gemma 2 or Gemma 3n.
- Dialogue output is text-only, 1–2 short Korean sentences, with thinking disabled and a bounded context/output budget.
- `use_tools=false` for Cheshire dialogue. Do not let the dialogue model decide quiz correctness, puzzle state, emotion state, or object actions.
- Unity-facing request and response contracts remain backward compatible: `/chat`, `/chat/stream`, `ChatResponse`, and `SSEEvent`.
- Bind release services to `127.0.0.1`; do not expose a player's local model server to the public internet.
- First-run installation must show consent and download size, support retry/offline failure, and retain model files after game exit.
- Preserve existing cloud providers behind a development/fallback flag until local quality and latency are accepted.

---

### Task 1: Freeze the dialogue-only contract

**Files:**
- Modify: `backend_ai/models/requests.py`
- Modify: `backend_ai/services/chat_service.py`
- Modify: `disputatio/Assets/mokotan/mokotan/script/AI/ChatHttpClient.cs`
- Test: `backend_ai/tests/test_chat_request_model.py`, `backend_ai/tests/test_chat_service.py`, `disputatio/Assets/Editor/Tests/EditMode/AI/ChatHttpClientTests.cs`

**Interfaces:**
- Produce a normalized dialogue request with `use_tools=False`, bounded prompt/context, and explicit `character_facts`/`dialogue_context` fields if needed.
- Preserve the existing `SSEEvent` event names and JSON fields.

- [x] Add failing tests proving Cheshire dialogue requests never pass the game tool registry and that quiz requests still use the deterministic grading path.
- [x] Run the focused Python and Unity tests and verify the new assertions fail.
- [x] Implement the smallest request-path change: force tools off for dialogue-only Cheshire calls while leaving the explicit tutor grade endpoint unchanged.
- [x] Run focused tests and verify they pass.
- [ ] Commit: `feat: isolate cheshire dialogue from game decisions`

### Task 2: Implement the local Gemma 4 E2B provider

**Files:**
- Create: `backend_ai/providers/litert_provider.py`
- Modify: `backend_ai/providers/__init__.py`
- Modify: `backend_ai/config.py`
- Test: `backend_ai/tests/test_providers.py`, create `backend_ai/tests/test_litert_provider.py`

**Interfaces:**
- `LiteRTProvider(AIProvider).__init__(base_url: str, model: str, num_ctx: int, think: bool = False)`
- `LiteRTProvider.stream_chat(messages, tools=None, temperature=0.8, max_tokens=120) -> AsyncIterator[SSEEvent]`
- The provider accepts the local runtime's NDJSON/OpenAI-compatible stream and emits `text_delta`, `function_call` (only for future non-dialogue use), `error`, and `done` events.

- [x] Write mocked-stream tests for text chunks, malformed chunks, mid-stream errors, and graceful completion.
- [x] Run `pytest backend_ai/tests/test_litert_provider.py -v` and verify failure before implementation.
- [x] Implement HTTP streaming with a persistent line buffer; never discard a partial NDJSON line.
- [x] Suppress `thinking` fields from player-visible text and accumulate final text for the `done` event.
- [x] Run provider tests and the complete provider test module.
- [ ] Commit: `feat: add local gemma4 e2b provider`

### Task 3: Select and bootstrap the local runtime

**Files:**
- Modify: `backend_ai/main.py`
- Modify: `backend_ai/config.py`
- Create: `backend_ai/local_runtime.py`
- Create: `scripts/check_local_ai.py`
- Test: create `backend_ai/tests/test_local_runtime.py`

**Interfaces:**
- `LocalRuntimeStatus(ollama_or_litert_available: bool, model_available: bool, error: str | None)`
- `check_local_runtime() -> LocalRuntimeStatus`
- `start_local_runtime() -> subprocess.Popen | None`

- [x] Add tests for runtime unavailable, model unavailable, and ready states using mocked subprocess/HTTP calls.
- [x] Implement a pinned runtime/model health check and a clear error returned by `/health` or startup logs.
- [x] Configure `AI_PROVIDER=local` to instantiate `LiteRTProvider`; keep cloud providers selectable for development.
- [x] Ensure local mode starts even when `GROQ_API_KEY` and `GOOGLE_API_KEY` are empty.
- [x] Run backend startup tests and a manual local health check.
- [ ] Commit: `feat: bootstrap local ai runtime`

### Task 4: Preserve and harden SSE from FastAPI to Unity

**Files:**
- Modify: `backend_ai/main.py`
- Modify: `disputatio/Assets/mokotan/mokotan/script/AI/ChatHttpClient.cs`
- Modify: `disputatio/Assets/mokotan/mokotan/script/AI/BaseChatbot.cs`
- Test: `backend_ai/tests/test_chat_service.py`, `disputatio/Assets/Editor/Tests/EditMode/AI/ChatHttpClientTests.cs`

**Interfaces:**
- Keep server output as `data: {SSEEvent JSON}\\n\\n`.
- Unity streaming entry point remains `GetGPTResponseStreaming`.

- [x] Add a test where an SSE event is split across multiple download-buffer reads.
- [x] Verify the current parser fails or loses the split event.
- [x] Add a pending-line buffer in Unity and parse only complete newline-terminated events.
- [x] Switch the Cheshire send path to streaming while retaining non-streaming fallback for errors.
- [x] Manual `/chat/stream` against local LiteRT Gemma 4 E2B (HTTP 200, `data:` frames). Unity EditMode `ChatHttpClientTests` is blocked until this clone (`D:\Capstone\newCapstone\disputatio`) is open in the Editor; current unity-cli instance is a different repo path.
- [ ] Commit: `fix: make cheshire sse parsing chunk safe`

### Task 5: Add first-run installation and readiness UX

**Files:**
- Create: `installer/` or the selected existing packaging project
- Create: `scripts/install_local_ai.ps1`
- Modify: `backend_ai/README.md`, `backend_ai/DEPLOY.md`
- Modify: Unity startup/AI UI scripts identified during implementation
- Test: installer smoke test script and manual clean-machine checklist

**Interfaces:**
- Installer checks OS, RAM, disk space, runtime presence, and model presence.
- First run displays consent, approximate download size, progress, retry, and a CPU-only/AI-disabled fallback.

- [x] Define and pin the runtime version and model artifact checksum.
- [x] Implement installation of the local runtime and LiteRT-LM `gemma4-e2b` only after user consent.
- [x] Start the local runtime and FastAPI on `127.0.0.1`; poll health before allowing the chat UI to send.
- [x] Keep model files on uninstall/exit unless the user explicitly chooses to remove them.
- [x] Include Gemma 4 and runtime license notices in the distribution.
- [x] Test planner: clean install, already-installed model, interrupted checksum, insufficient disk, offline launch (unit). Manual clean-machine steps: `installer/CHECKLIST.md`.
- [ ] Commit: `feat: add local ai first-run bootstrap`

### Task 6: Evaluate dialogue quality and release gates

**Files:**
- Create: `backend_ai/tests/evals/cheshire_dialogue_cases.jsonl`
- Create: `backend_ai/tests/evals/run_cheshire_eval.py`
- Modify: `docs/architecture.md`, `backend_ai/README.md`

**Interfaces:**
- Evaluation reports factuality, persona adherence, Korean naturalness, length compliance, repetition, and latency.

- [x] Create at least 50 representative prompts covering greetings, hints, wrong answers, repeated questions, emotional states, and lore boundaries.
- [x] Run the same cases against Gemma 4 E2B local provider (`AI_PROVIDER=local python -m tests.evals.run_cheshire_eval`). Do not use Groq/Gemini.
- [x] Reject outputs that invent puzzle facts, expose internal prompts, exceed length, or emit invalid player-visible control tokens.
- [x] Set release gates: no game-state decisions from the model, ≥90% valid output rate (design AC5), zero JSON/tool leaks, zero invented-fact hits. Latency p95 is recorded from the live run.
- [x] Record the chosen model/runtime, tested hardware, known limitations, and fallback behavior after a local live run.
- [ ] Commit: `docs: record cheshire local model evaluation`

### Future Task 7: Mobile-native target (separate project)

Do not include this in the desktop MVP. When Android/iOS is approved, use the LiteRT-LM/MediaPipe mobile artifact directly inside a Unity native plugin, replace HTTP SSE with token callbacks, and reuse the dialogue prompt/evaluation contract. The PC local-server path and the mobile-native path should share only the model-agnostic dialogue interface.

