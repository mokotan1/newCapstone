# Gate 0 decision — local LLM autostart (2026-09-15)

- Feature spec: `docs/superpowers/specs/2026-09-15-local-llm-autostart-design.md`
- Recorder: parent agent in this checkout (`codex/효과음정리`)
- Working tree: dirty (autostart + cloud-path removal + packaging). Base commit observed: `07a06e0bc1d535706f603f172d5b3f13ee7c06e9`
- Verdict: **AC16 cannot pass**

## What was already measured (not frozen for this spec)

These numbers existed in `docs/architecture.md` §8 **before** this autostart implementation wave. They were **not** written as a Gate 0 freeze for AC01–AC18 (minimum PC, prompt/token budget, cold-start SLO, frame p95) prior to product code changes.

| Source | Date | Result |
|--------|------|--------|
| LiteRT GPU (`run_gate0_litert_gpu`) | 2026-09-07 | RTX 4060 Ti 8188 MiB, TTFT p50 1.83s, complete p50 1.94s, 8GB SLO 1s **slo_miss**. FastAPI `gate0_passed=false` |
| CUDA sidecar (`run_gate1_cuda`) | 2026-09-08 | llama.cpp CUDA, 50/50 valid, leak 0, fabricate 0, TTFT p50 60ms, complete p50 244ms, `gate_passed=true`. Unity frame gate unmeasured |
| Cheshire CPU eval | 2026-09-03 | 50/50 valid, leak 0, fabricate 0, TTFT p50 3.7s, complete p50 4.9s |

## Freeze that this spec still lacks

The spec requires **before implementation**:

- minimum supported PC
- fixed prompt / token length
- cold Ready budget (default 180s is a design default, not a measured hardware freeze)
- first-token and complete p50/p95 for the shipped engine
- in-game frame p95
- RAM/VRAM caps

Those product SLOs were not frozen as a Gate 0 decision file for this spec. Post-hoc use of the 2026-09-07 GPU miss or 2026-09-08 CUDA pass to declare AC16 would be criterion relaxation. This file records that fact instead of inventing a freeze.

## Runtime / model pin (code)

- LiteRT-LM 0.16.1, model id `gemma4-e2b`, artifact SHA-256 `181938105e0eefd105961417e8da75903eacda102c4fce9ce90f50b97139a63c` (`backend_ai/data/local_ai_manifest.json`)
- RAG embedding id `local-hash-v1`, dimension 256 (hashing trick, not a vendored neural embedding checkpoint)
- Live Editor session 2026-09-15: Supervisor `127.0.0.1:11144`, readiness `Failed` / `gpu_initialization_failed`, `chat_ready=false`, `rag_ready=true`

## Decision

Do not start a new cloud provider. Keep local LiteRT/CUDA pins already in tree. **Do not declare AC16 or the feature verified.** Independent review is still `false`.
