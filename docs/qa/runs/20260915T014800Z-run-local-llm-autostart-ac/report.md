# Local LLM autostart — AC evidence (2026-09-15)

- OS: Windows 10.0.26200
- Unity: 6000.0.36f1, PID 31432, unity-cli ready, connector 0.3.21
- Branch: `codex/효과음정리` (dirty working tree). Observed HEAD: `07a06e0bc1d535706f603f172d5b3f13ee7c06e9`
- Independent review: **false**
- Feature verified: **no** (spec forbids declaring verified without AC01–AC18 pass + Gate 0 freeze + independent review + Editor/Player integration)

## Commands run this session

| Command | Result |
|---------|--------|
| `python -m pytest tests -q` in `backend_ai/` | **290 passed**, 1 warning, 4.34s |
| `python -m ruff check` on touched Python (after `--fix` on new tests) | clean for those files; pre-existing `chat_service` I001 / `runtime_manager` BLE001 not in this pass |
| `python scripts/build_offline_package.py` | `dist/local-ai-package`, 142 hashed files, `package_manifest.json` SHA-256 `1390CE110471BC878A4D0DF9308E5936B174F5E106171E552669DE93C4C5CFAB` |
| `.\scripts\unity-cli.cmd --project disputatio editor refresh --compile` | compile complete, console `error,warning` empty |
| EditMode `--filter` ChatHttpClientTests | 37/37 |
| TutorQuizGraderTests | 10/10 |
| LocalAiEndpointResolverTests | 7/7 |
| ServerConfigTests | 10/10 |
| LocalAiSettingsResumeTests | 7/7 |
| SettingsCheshirePreviewGateTests | 11/11 |
| Live `GET http://127.0.0.1:11144/` with session Bearer | 200, `readiness.state=Failed`, `error_code=gpu_initialization_failed`, `chat_ready=false`, `rag_ready=true` |
| Live `GET` with `Origin: https://evil.example` | **403** |
| Cheshire eval / Gate 0 GPU remeasure / Play×10 / clean VM | **not run** |
| qa-playtester | **not launched** |

Tokens are not copied into this report.

## AC01 classification (executable vs historical)

Search (`GROQ_API_KEY`, `GOOGLE_API_KEY`, `google-generativeai`, `from groq`, `providers.groq`, `providers.gemini`) on `*.py,*.txt,*.yml,*.yaml,*.ps1,*.sh,*.cs`: **no executable-path matches**.

| Item | Class |
|------|--------|
| Deleted Groq/Gemini providers; `local_runtime.build_chat_providers` LiteRT only | executable — removed |
| `backend_ai/requirements.txt` (no groq / google-generativeai) | executable — removed |
| CI `wiki-rag.yml` / `backend-build.yml` dummy cloud keys | executable — removed |
| `backend_ai/deploy.sh` cloud key require | executable — removed |
| Test mock provider **names** `"groq"` / `"gemini"` | labels only, not SDKs |
| `Settings(groq_api_key=...)` in tests | pydantic extra ignore; not a runtime path |
| `docs/superpowers/plans/*`, old design specs, generated wiki RAG snapshots of past architecture | historical — not deletion targets |
| Session `CHAT_API_TOKEN` / control token | local session auth, not an external AI key |
| RAG `local-hash-v1` | local hashing, not Google embeddings |

## AC verdicts

| ID | Verdict | Evidence |
|----|---------|----------|
| AC01 | **pass** (executable paths) | this classification; pytest 290 |
| AC02 | **blocked** | no Windows network-isolation harness; live engine already Failed |
| AC03 | **fail** | Editor did spawn Supervisor (`instance_id` `cc0c793c-7dae-47e2-9bf6-25e109ea432f`, `127.0.0.1:11144`) but Ready not reached; `gpu_initialization_failed` |
| AC04 | **미검증** | Play/Stop ×10 and domain reload ×3 not performed; two session JSON files exist for parent PID 31432 |
| AC05 | **blocked** | no Python-less VM; package `run-supervisor.cmd` still calls system `python`; Gemma weights not vendored |
| AC06 | **미검증** (unit only) | SettingsCheshirePreviewGateTests 11/11; no UI recording of menu-during-load |
| AC07 | **미검증** (unit only) | Job Object `KILL_ON_JOB_CLOSE` unit test; no 10s process-tree capture after kill |
| AC08 | **미검증** (unit only) | resolver rejects port-only / remote URL; live external `:8000` not placed |
| AC09 | **partial** | resolver stream/grade share root (7/7); live GET used the session URL; stream/grade HTTP not captured |
| AC10 | **pass** (contract tests) | queue 429, existing SSE/tool pytest; live hang injection not done |
| AC11 | **pass** (index contract) | known ko/ja/en query tests + corrupt/missing/dimension prepare_error; embedding is `local-hash-v1`, not a packaged neural model |
| AC12 | **fail** (this live snapshot) | requested_device gpu, effective unknown, Failed; CPU recovery not observed on this GET. GPU→CPU unit tests exist separately |
| AC13 | **partial** | install checksum refuse + RAG corrupt/missing tests; live missing-weight package copy not run |
| AC14 | **blocked** | system Python required; install dir still writable for `%LOCALAPPDATA%` sessions only |
| AC15 | **pass** (HTTP) | Origin 403 live + pytest; grade token pytest |
| AC16 | **blocked** | Gate 0 freeze missing — see `docs/superpowers/specs/2026-09-15-local-llm-gate0-decision.md`; eval not re-run; engine not Ready |
| AC17 | **partial** | backend 290 + Unity EditMode listed above; **no qa-playtester actual input** |
| AC18 | **partial** | 142-file SHA manifest + verify test; no portable interpreter / model blob; `dist/` gitignored |

## Honesty bar

This run implemented remaining **code** contracts (readiness, Origin, RAG prepare errors, package hashing, client 155s timeout, wiki/ops key text). It does **not** complete the spec. Player exe, clean VM, network cut, Job-object kill, 10× Play/Stop, and independent review remain open.
