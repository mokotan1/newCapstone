# Cheshire Local Gemma 4 E2B Design

## Goal

Run Cheshire dialogue locally on Windows with Gemma 4 E2B and LiteRT-LM, without EC2 or cloud AI API keys. Quiz grading and game progress remain deterministic.

## Decisions

- First release: Windows desktop only; direct Android/iOS inference is deferred.
- Runtime: LiteRT-LM with `litert-community/gemma-4-E2B-it-litert-lm`, imported as `gemma4-e2b`.
- Local-only network: LiteRT-LM at `127.0.0.1:9379`; FastAPI at `127.0.0.1:8000`.
- Unity keeps its existing `POST /chat/stream` SSE contract. FastAPI translates LiteRT-LM's OpenAI-compatible stream to the existing `SSEEvent` schema.
- Cheshire receives no game tools. It receives game-authored facts and returns only a Korean, two-sentence-or-less dialogue line.
- Puzzle answers, hints, state transitions, quiz scoring, and animations remain in Unity/backend rules.

## Dialogue contract

- Text-only; thinking disabled; no function tools.
- Context limited to 4,096 tokens and output to 120 tokens.
- Model sampling starts at temperature 0.8, top-p 0.95, top-k 64.
- Empty, JSON/tool-like, overlong, or policy-invalid replies use the game-authored fallback line.

## End-user delivery

The Windows installer contains the game, pinned LiteRT-LM runtime, FastAPI runtime, and licenses. First launch requests consent before downloading/importing the model, requires 15 GB free disk, starts both loopback-only child processes, waits for health checks, then starts Unity. Later launches work offline and reuse the installed model.

## Acceptance criteria

1. No cloud API key, EC2 endpoint, or network connection is required after initial model installation.
2. ParrotChatbot receives streamed `text_delta` events and exactly one terminal `done` event.
3. Split SSE frames never lose or duplicate characters.
4. ParrotChatbot sends `use_tools: false`; `/tutor/grade` remains the quiz authority.
5. A 50-case Korean dialogue suite has at least 90% valid replies and no JSON/tool leakage or invented puzzle facts.

