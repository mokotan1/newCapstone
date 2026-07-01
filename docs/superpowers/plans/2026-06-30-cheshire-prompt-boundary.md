# Cheshire Prompt Boundary Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the first implementation slice for Cheshire hint prompt separation: client sends structured hint rewrite data, backend owns trusted rewrite policy, and backend validates/falls back when the model output violates the contract.

**Architecture:** Unity may include optional `hint_rewrite` data in the existing `/chat` payload, but the backend treats it as untrusted data. Backend trusted system policy tells the LLM to rewrite only the provided base hint, while the payload is wrapped as an external document; non-streaming `/chat` post-processes the LLM line with required/forbidden term validation and fallback.

**Tech Stack:** Unity C# + Newtonsoft JSON, FastAPI/Pydantic backend, pytest.

---

## File Structure

- Modify `backend_ai/models/requests.py`: add `HintRewritePayload` and optional `ChatRequest.hint_rewrite`.
- Create `backend_ai/services/hint_rewrite.py`: trusted server instruction, untrusted payload formatter, and response validator/fallback.
- Modify `backend_ai/services/chat_service.py`: attach hint rewrite policy/data to LLM messages and post-process non-streaming responses.
- Modify `backend_ai/tests/test_chat_request_model.py`: request model coverage for `hint_rewrite`.
- Create `backend_ai/tests/test_hint_rewrite.py`: validator coverage.
- Modify `backend_ai/tests/test_chat_service.py`: service-level coverage that hint rewrite data is sent as untrusted document and fallback is applied.
- Modify `disputatio/Assets/mokotan/mokotan/script/AI/ChatHttpClient.cs`: add serializable hint rewrite payload types and optional field to `LocalLlamaPayload`.

## Task 1: Backend Request Model

**Files:**
- Modify: `backend_ai/models/requests.py`
- Test: `backend_ai/tests/test_chat_request_model.py`

- [ ] **Step 1: Write the failing model tests**

Add these tests to `backend_ai/tests/test_chat_request_model.py`:

```python
def test_hint_rewrite_payload_is_accepted() -> None:
    r = ChatRequest.model_validate(
        {
            "prompt": "이 병 어디다 써?",
            "system": "client scene",
            "hint_rewrite": {
                "hint_id": "opaque_bottle_sink_use",
                "item_id": "opaque_bottle",
                "hint_target": "kitchen_sink",
                "hint_level": "direct",
                "base_hint": "이 병은 주방 싱크대에서 사용할 수 있다.",
                "required_terms": ["병", "싱크대"],
                "forbidden_terms": ["열쇠"],
                "fallback_line": "그 병은 주방 싱크대에서 물을 채워볼 수 있다.",
            },
        }
    )

    assert r.hint_rewrite is not None
    assert r.hint_rewrite.hint_id == "opaque_bottle_sink_use"
    assert r.hint_rewrite.required_terms == ["병", "싱크대"]
    assert r.hint_rewrite.forbidden_terms == ["열쇠"]


def test_hint_rewrite_rejects_empty_base_hint() -> None:
    with pytest.raises(ValueError):
        ChatRequest.model_validate(
            {
                "prompt": "힌트",
                "hint_rewrite": {
                    "hint_id": "h",
                    "item_id": "i",
                    "hint_target": "t",
                    "hint_level": "direct",
                    "base_hint": "",
                },
            }
        )
```

- [ ] **Step 2: Run tests to verify they fail**

Run:

```bash
cd backend_ai
python -m pytest tests/test_chat_request_model.py -q
```

Expected: FAIL because `ChatRequest` has no `hint_rewrite` attribute and/or `pytest` import is missing in the test file.

- [ ] **Step 3: Implement the minimal request model**

In `backend_ai/models/requests.py`, add:

```python
class HintRewritePayload(BaseModel):
    model_config = ConfigDict(extra="ignore")

    hint_id: str = Field(..., min_length=1, max_length=128)
    item_id: str = Field(..., min_length=1, max_length=128)
    hint_target: str = Field(..., min_length=1, max_length=128)
    hint_level: str = Field(..., min_length=1, max_length=64)
    base_hint: str = Field(..., min_length=1, max_length=1000)
    required_terms: list[str] = Field(default_factory=list, max_length=16)
    forbidden_terms: list[str] = Field(default_factory=list, max_length=32)
    fallback_line: str | None = Field(default=None, max_length=1000)
    narrative_seed: str | None = Field(default=None, max_length=1000)
    interaction_type: str | None = Field(default=None, max_length=128)
    allow_highlight: bool = True
```

Then add to `ChatRequest`:

```python
hint_rewrite: HintRewritePayload | None = None
```

Ensure `backend_ai/tests/test_chat_request_model.py` imports `pytest`.

- [ ] **Step 4: Run tests to verify they pass**

Run:

```bash
cd backend_ai
python -m pytest tests/test_chat_request_model.py -q
```

Expected: PASS.

## Task 2: Hint Rewrite Formatter And Validator

**Files:**
- Create: `backend_ai/services/hint_rewrite.py`
- Test: `backend_ai/tests/test_hint_rewrite.py`

- [ ] **Step 1: Write failing validator tests**

Create `backend_ai/tests/test_hint_rewrite.py`:

```python
from models.requests import HintRewritePayload
from services.hint_rewrite import (
    HINT_REWRITE_SERVER_INSTRUCTION,
    build_hint_rewrite_external_document,
    apply_hint_rewrite_fallback,
)


def _payload() -> HintRewritePayload:
    return HintRewritePayload(
        hint_id="opaque_bottle_sink_use",
        item_id="opaque_bottle",
        hint_target="kitchen_sink",
        hint_level="direct",
        base_hint="이 병은 주방 싱크대에서 사용할 수 있다.",
        required_terms=["병", "싱크대"],
        forbidden_terms=["열쇠"],
        fallback_line="그 병은 주방 싱크대에서 물을 채워볼 수 있다.",
    )


def test_instruction_requires_rewrite_only() -> None:
    assert "rewrite only" in HINT_REWRITE_SERVER_INSTRUCTION
    assert "Do not solve" in HINT_REWRITE_SERVER_INSTRUCTION


def test_external_document_contains_payload_but_not_policy() -> None:
    doc = build_hint_rewrite_external_document(_payload())

    assert "opaque_bottle_sink_use" in doc
    assert "이 병은 주방 싱크대에서 사용할 수 있다." in doc
    assert "trusted" not in doc.lower()


def test_valid_line_is_preserved() -> None:
    line = "병은 목마르다. 싱크대가 기억한다."

    assert apply_hint_rewrite_fallback(line, _payload()) == line


def test_forbidden_term_uses_fallback() -> None:
    line = "싱크대에서 열쇠를 꺼내."

    assert apply_hint_rewrite_fallback(line, _payload()) == "그 병은 주방 싱크대에서 물을 채워볼 수 있다."


def test_missing_required_term_uses_fallback() -> None:
    line = "물이 기억한다."

    assert apply_hint_rewrite_fallback(line, _payload()) == "그 병은 주방 싱크대에서 물을 채워볼 수 있다."
```

- [ ] **Step 2: Run tests to verify they fail**

Run:

```bash
cd backend_ai
python -m pytest tests/test_hint_rewrite.py -q
```

Expected: FAIL because `services.hint_rewrite` does not exist.

- [ ] **Step 3: Implement formatter and validator**

Create `backend_ai/services/hint_rewrite.py`:

```python
from __future__ import annotations

import json

from models.requests import HintRewritePayload

HINT_REWRITE_SERVER_INSTRUCTION = """
[Cheshire hint rewrite policy]
You rewrite only the provided base hint in Cheshire's voice.
Do not solve the puzzle yourself.
Do not invent new places, items, rewards, passwords, or facts.
Preserve the base_hint meaning.
Never reveal forbidden terms.
Return a short player-facing line, not analysis.
""".strip()


def build_hint_rewrite_external_document(payload: HintRewritePayload) -> str:
    data = payload.model_dump(exclude_none=True)
    return json.dumps(data, ensure_ascii=False, sort_keys=True)


def _fallback(payload: HintRewritePayload) -> str:
    if payload.fallback_line and payload.fallback_line.strip():
        return payload.fallback_line.strip()
    return payload.base_hint.strip()


def apply_hint_rewrite_fallback(line: str, payload: HintRewritePayload) -> str:
    text = (line or "").strip()
    if not text:
        return _fallback(payload)

    for forbidden in payload.forbidden_terms:
        term = (forbidden or "").strip()
        if term and term in text:
            return _fallback(payload)

    for required in payload.required_terms:
        term = (required or "").strip()
        if term and term not in text:
            return _fallback(payload)

    if len(text) > 300:
        return _fallback(payload)

    return text
```

- [ ] **Step 4: Run tests to verify they pass**

Run:

```bash
cd backend_ai
python -m pytest tests/test_hint_rewrite.py -q
```

Expected: PASS.

## Task 3: ChatService Integration

**Files:**
- Modify: `backend_ai/services/chat_service.py`
- Test: `backend_ai/tests/test_chat_service.py`

- [ ] **Step 1: Write failing service tests**

Add a provider capture test and fallback test to `backend_ai/tests/test_chat_service.py`:

```python
@pytest.mark.asyncio
async def test_hint_rewrite_adds_trusted_policy_and_untrusted_document():
    provider = CapturingProvider(events=[SSEEvent(type="done", full_text="병은 목마르다. 싱크대가 기억한다.")])
    service = ChatService(provider, None, ToolRegistry())

    await service.chat(
        ChatRequest(
            prompt="이 병 어디다 써?",
            system="client scene",
            hint_rewrite={
                "hint_id": "opaque_bottle_sink_use",
                "item_id": "opaque_bottle",
                "hint_target": "kitchen_sink",
                "hint_level": "direct",
                "base_hint": "이 병은 주방 싱크대에서 사용할 수 있다.",
                "required_terms": ["병", "싱크대"],
                "forbidden_terms": ["열쇠"],
            },
        )
    )

    assert provider.last_messages is not None
    assert "rewrite only" in provider.last_messages[0]["content"]
    user_bundle = "\n".join(m["content"] for m in provider.last_messages if m["role"] == "user")
    assert "hint_rewrite" in user_bundle
    assert "opaque_bottle_sink_use" in user_bundle


@pytest.mark.asyncio
async def test_hint_rewrite_forbidden_term_falls_back():
    provider = CapturingProvider(events=[SSEEvent(type="done", full_text="싱크대에서 열쇠를 꺼내.")])
    service = ChatService(provider, None, ToolRegistry())

    result = await service.chat(
        ChatRequest(
            prompt="이 병 어디다 써?",
            hint_rewrite={
                "hint_id": "opaque_bottle_sink_use",
                "item_id": "opaque_bottle",
                "hint_target": "kitchen_sink",
                "hint_level": "direct",
                "base_hint": "이 병은 주방 싱크대에서 사용할 수 있다.",
                "required_terms": ["병", "싱크대"],
                "forbidden_terms": ["열쇠"],
                "fallback_line": "그 병은 주방 싱크대에서 물을 채워볼 수 있다.",
            },
        )
    )

    assert result.response == "그 병은 주방 싱크대에서 물을 채워볼 수 있다."
```

If `CapturingProvider` does not exist in `test_chat_service.py`, add:

```python
class CapturingProvider(ScriptedProvider):
    def __init__(self, events):
        super().__init__(events)
        self.last_messages = None

    async def stream_chat(self, messages, tools=None, temperature=0.7, max_tokens=512):
        self.last_messages = messages
        async for event in super().stream_chat(messages, tools=tools, temperature=temperature, max_tokens=max_tokens):
            yield event
```

- [ ] **Step 2: Run tests to verify they fail**

Run:

```bash
cd backend_ai
python -m pytest tests/test_chat_service.py -q
```

Expected: FAIL because `ChatService` does not yet add hint rewrite policy/documents or fallback.

- [ ] **Step 3: Integrate hint rewrite into ChatService**

In `backend_ai/services/chat_service.py`, import:

```python
from services.hint_rewrite import (
    HINT_REWRITE_SERVER_INSTRUCTION,
    apply_hint_rewrite_fallback,
    build_hint_rewrite_external_document,
)
```

Update `_gather_external_documents`:

```python
if request.hint_rewrite is not None:
    docs.append(("hint_rewrite", build_hint_rewrite_external_document(request.hint_rewrite)))
```

Update `_build_messages` so trusted instructions include both tool and hint policies:

```python
server_instructions: list[str] = []
if request.use_tools and len(self._registry) > 0:
    server_instructions.append(self._TOOL_INSTRUCTION)
if request.hint_rewrite is not None:
    server_instructions.append(HINT_REWRITE_SERVER_INSTRUCTION)
tool_inst = "\n\n".join(server_instructions) if server_instructions else None
```

Update `chat()` after `response_text` is built:

```python
if request.hint_rewrite is not None:
    response_text = apply_hint_rewrite_fallback(response_text, request.hint_rewrite)
```

- [ ] **Step 4: Run tests to verify they pass**

Run:

```bash
cd backend_ai
python -m pytest tests/test_chat_service.py tests/test_hint_rewrite.py tests/test_chat_request_model.py -q
```

Expected: PASS.

## Task 4: Unity Payload Type

**Files:**
- Modify: `disputatio/Assets/mokotan/mokotan/script/AI/ChatHttpClient.cs`

- [ ] **Step 1: Add serializable payload type**

In `ChatHttpClient.cs`, add after `LocalLlamaPayload` helper classes or before it:

```csharp
[Serializable]
public class HintRewritePayload
{
    public string hint_id;
    public string item_id;
    public string hint_target;
    public string hint_level;
    public string base_hint;
    public List<string> required_terms;
    public List<string> forbidden_terms;

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string fallback_line;

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string narrative_seed;

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string interaction_type;

    public bool allow_highlight = true;
}
```

Then add to `LocalLlamaPayload`:

```csharp
[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
public HintRewritePayload hint_rewrite;
```

- [ ] **Step 2: Run C# syntax checker**

Run:

```bash
dotnet run --project scripts/CSharpSyntaxChecker/CSharpSyntaxChecker.csproj -- disputatio/Assets
```

Expected: PASS.

## Task 5: Full Verification

**Files:**
- Verify all touched backend and Unity syntax.

- [ ] **Step 1: Run backend focused tests**

Run:

```bash
cd backend_ai
python -m pytest tests/test_chat_request_model.py tests/test_hint_rewrite.py tests/test_chat_service.py -q
```

Expected: PASS.

- [ ] **Step 2: Run C# syntax checker**

Run:

```bash
dotnet run --project scripts/CSharpSyntaxChecker/CSharpSyntaxChecker.csproj -- disputatio/Assets
```

Expected: PASS.

- [ ] **Step 3: Check git diff**

Run:

```bash
git diff -- backend_ai disputatio/Assets/mokotan/mokotan/script/AI/ChatHttpClient.cs docs/superpowers/plans/2026-06-30-cheshire-prompt-boundary.md
```

Expected: Only prompt-boundary implementation and the plan file changed.
