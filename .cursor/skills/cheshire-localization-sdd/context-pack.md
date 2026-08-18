# Cheshire Localization — Context Pack

Inject this block into every subagent prompt for this feature.

## Anchors

- **Branch:** `codex/cheshire-localization`
- **Design:** `docs/superpowers/specs/2026-07-13-cheshire-multilingual-localization-design.md`
- **Plan:** `docs/superpowers/plans/2026-07-13-cheshire-multilingual-localization.md`
- **Architecture:** read `docs/architecture.md` §2 / §6 before coding
- **Supported locales:** `ko`, `ja`, `en` (canonical only after normalize)

## Locale authority (read-only)

1. `Fungus.SetLanguage.mostRecentLanguage`
2. Else scene `Fungus.Localization.ActiveLanguage`
3. Normalize aliases/region tags → `ko` | `ja` | `en`
4. Empty / unsupported → `ko`

Examples: `KO`, `ko-KR`, `Korean` → `ko`; `JA`, `JP`, `ja-JP`, `Japanese` → `ja`; `EN`, `en-US`, `English` → `en`.

**Forbidden:** new PlayerPrefs key, settings singleton, Fungus package edits, rewriting already-displayed chat text.

## Request rules

- Snapshot locale when a request starts; in-flight language changes affect the *next* request only.
- One resolved locale per request for base system, room prompt, Chester voice, heuristics, and dynamic fragments.
- Missing localized resource → Korean fallback + one warning naming key+locale.
- Missing Korean source → empty string + diagnostic; do not throw; do not substitute another room's prompt.

## Paths

| Area | Path |
|------|------|
| Unity AI | `disputatio/Assets/mokotan/mokotan/script/AI/` |
| Locale classes | `…/AI/Localization/CheshireLocaleResolver.cs`, `CheshirePromptCatalog.cs` |
| Prompt resources | `disputatio/Assets/Resources/CheshirePrompts/{ko,ja,en}/` |
| EditMode tests | `disputatio/Assets/Editor/Tests/EditMode/AI/` (+ `Localization/`) |
| Backend models | `backend_ai/models/requests.py` |
| Chat service | `backend_ai/services/chat_service.py` |
| Quiz bank | `backend_ai/services/quiz_bank.py`, `backend_ai/data/tutor_quiz/quiz_bank.csv` |
| RAG | `backend_ai/services/tutor_rag_service.py` |
| LLM defense | `backend_ai/llm_defense/message_builder.py` |

## Stable prompt keys

`BaseSystem`, `ChesterVoiceCommon`, `introPrompt`, `KitchenPrompt`, `MainBedroomPrompt`, `SonRoomPrompt`, `StudyRoomPrompt`, `TutorRoomPrompt`, `WifeRoomPrompt`, `ParrotPrompt`

Resource load path pattern: `CheshirePrompts/{locale}/{key}` via `Resources.Load<TextAsset>`.

## Invariant contracts

- Tool names / JSON keys unchanged: `give_hint`, `update_quiz`, `emote`, `hint_level`, `is_correct`, etc.
- Payload adds canonical `locale` on `/chat` and `/chat/stream` (and tutor grade when Task 6).
- Backend defaults omitted `locale` to `ko` for old clients.
- Player-facing backend errors use locale catalog; logs stay language-neutral.

## Verification commands

```powershell
# Unity (repo root)
.\scripts\unity-cli-open-status-cmd.cmd
.\scripts\unity-cli.cmd --project disputatio status
.\scripts\unity-cli.cmd --project disputatio editor refresh --compile
.\scripts\unity-cli.cmd --project disputatio console --type error,warning --lines 80
.\scripts\unity-cli.cmd --project disputatio test --mode EditMode --filter <TestClassName>

# Backend
cd backend_ai
ruff check .
python -m pytest tests/ -q --tb=short
```

## Commits

Subagents **must not** `git commit` or `git push`. Report files changed; parent commits only if the user asks.
