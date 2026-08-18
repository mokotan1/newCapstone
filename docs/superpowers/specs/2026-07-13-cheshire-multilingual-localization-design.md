# Cheshire Multilingual Localization Design

**Date:** 2026-07-13  
**Branch:** `codex/cheshire-localization`  
**Supported languages:** Korean (`ko`), Japanese (`ja`), English (`en`)

## Goal

Make every player-facing Cheshire interaction follow the language already selected by the game's Fungus localization flow. Changing the game language must affect the next Cheshire request without reloading the scene or creating a second language-setting system.

## Scope

This feature covers:

- Cheshire room prompts, the common Cheshire voice block, heuristic hint policy, and runtime prompt fragments.
- Cheshire-triggered synthetic user turns and local chat validation/error messages.
- The parrot and tutor variants that use the same chat pipeline.
- The Unity-to-backend locale field, trusted response-language rules, and localized backend error responses.
- Tutor quiz questions, accepted answer aliases, and reference snippets for all three languages.
- Automated tests for locale resolution, resource fallback, payload serialization, backend locale handling, and prompt selection.

This feature does not localize unrelated menus, ordinary Fungus dialogue, inventory text, or other game UI. Those systems continue using their existing localization data.

## Existing Language Source

The existing `Fungus.SetLanguage` command remains the authority for runtime language changes. It calls `Localization.SetActiveLanguage(...)` and records the latest selection in `SetLanguage.mostRecentLanguage`.

Cheshire will not introduce a new settings singleton or PlayerPrefs key. A small adapter will read the existing state whenever a chat request is built:

1. Read `SetLanguage.mostRecentLanguage` because it reflects commands executed during play.
2. If it is empty, read the active scene's `Localization.ActiveLanguage`.
3. Normalize aliases and region tags to `ko`, `ja`, or `en`.
4. Fall back to `ko` for empty or unsupported values.

Accepted normalization examples include `KO`, `ko-KR`, and `Korean` to `ko`; `JA`, `JP`, `ja-JP`, and `Japanese` to `ja`; and `EN`, `en-US`, and `English` to `en`.

The adapter reads the value for every outgoing request. A language change therefore affects the next Cheshire response. Text that has already been displayed is not rewritten.

## Approaches Considered

### 1. Read Fungus state directly from every chatbot

This is the smallest code change, but it duplicates normalization and fallback behavior throughout the chatbot subclasses and tightly couples every room bot to Fungus.

### 2. Modify Fungus to publish a language-changed event

This supports immediate subscriptions, but Cheshire only needs the language when building a request. Editing third-party Fungus code increases upgrade and regression risk without improving the player-visible result.

### 3. Add a read-only Cheshire locale adapter

This is the selected approach. One adapter reads the existing Fungus language state, normalizes it, and exposes a stable canonical code. It avoids a second source of truth and avoids modifying Fungus.

## Architecture

### Unity locale adapter

`CheshireLocaleResolver` owns canonicalization and existing-state lookup. It exposes a pure normalization method for EditMode tests and a runtime `ResolveCurrentLocale()` method for chat requests.

### Prompt catalog

`CheshirePromptCatalog` loads language-specific `TextAsset` resources by stable prompt key. Resources are organized as:

```text
Assets/Resources/CheshirePrompts/
├── ko/
│   ├── BaseSystem.txt
│   ├── ChesterVoiceCommon.txt
│   ├── KitchenPrompt.txt
│   ├── MainBedroomPrompt.txt
│   ├── SonRoomPrompt.txt
│   ├── StudyRoomPrompt.txt
│   ├── TutorRoomPrompt.txt
│   ├── WifeRoomPrompt.txt
│   └── ParrotPrompt.txt
├── ja/
│   └── same prompt keys
└── en/
    └── same prompt keys
```

Lookup uses the requested locale first and Korean second. If both resources are missing, it returns an empty string and emits one diagnostic warning naming the prompt key and locale. The catalog does not silently substitute one room's prompt for another.

### Dynamic prompt fragments

Inline Korean instructions currently generated in room chatbot classes and `HintInformationPolicy` must become locale-aware. Fixed blocks move into the prompt catalog. Blocks containing runtime values use localized format templates whose placeholders remain invariant, such as `{pageStart}` and `{pageEnd}`.

All prompt composition receives one resolved locale per request. The same locale is used for the base system text, room prompt, common voice block, heuristic block, and dynamic fragments so a request cannot mix languages.

### Chat request payload

Unity adds a canonical `locale` field to both `/chat` and `/chat/stream` payloads:

```json
{
  "prompt": "Give me a hint.",
  "system": "...localized Cheshire prompt...",
  "locale": "en"
}
```

Tool and JSON identifiers such as `give_hint`, `update_quiz`, `hint_level`, and `is_correct` remain unchanged.

### Backend locale enforcement

`ChatRequest` accepts `locale`, normalizes supported aliases, and defaults to `ko` for older clients that omit it. The backend adds a trusted language instruction to the system channel so the model must produce player-facing prose in the requested language. This rule is separate from the untrusted client scene configuration.

Backend-generated player messages, including provider failure and rate-limit responses, come from a locale-keyed message catalog. Logs and exception details remain language-neutral and are not returned to the player.

### Tutor localization

The tutor quiz bank expands from Korean-only fields to locale-specific fields:

- `question_ko`, `question_ja`, `question_en`
- `acceptable_answers_ko`, `acceptable_answers_ja`, `acceptable_answers_en`
- `reference_snippet_ko`, `reference_snippet_ja`, `reference_snippet_en`

`question_id`, difficulty, and tags remain shared. The backend selects question text, accepted answers, and reference snippets using `request.locale`, falling back to Korean only when a localized cell is empty. Deterministic grading uses the accepted-answer column for the current locale.

Tutor RAG documents are selected by locale. Korean remains the fallback when the requested locale has no matching indexed document. The RAG response-language rule still follows the requested locale, even when fallback source material is Korean.

## Request Flow

1. The player changes language through the existing Fungus language command.
2. The next Cheshire send action asks `CheshireLocaleResolver` for the canonical locale.
3. `CheshirePromptCatalog` loads every prompt component for that locale with Korean fallback.
4. Unity builds one localized system prompt and includes the canonical locale in the request payload.
5. The backend applies trusted security, tool, and response-language policies.
6. Tutor data and backend error messages are selected using the same locale.
7. Cheshire's next displayed line is Korean, Japanese, or English according to the current game setting.

## Language Change During a Request

The locale is snapshotted when a request begins. If the player changes language while that HTTP request is in progress, its response remains in the language with which it was sent. The following request uses the new language. This avoids mismatching a response with the prompt and tutor data that produced it.

## Fallback and Error Handling

- Unsupported or empty language selection resolves to `ko`.
- Missing localized prompt resource falls back to the Korean resource with a warning.
- Missing tutor cell falls back to the matching Korean cell, not to another question.
- Missing backend message key falls back to the Korean message for the same error.
- Missing Korean source is treated as a content/configuration error and is covered by tests; chat composition continues with the remaining blocks instead of throwing.
- Locale errors never change tool schemas or function-call parsing.

## Content Quality Rules

English and Japanese prompts are authored as localized Cheshire voice guides, not literal word-for-word translations. Each version must preserve:

- The same puzzle facts and spoiler boundaries.
- Equivalent response-length limits.
- Cheshire's mocking, riddle-like personality.
- The same allowed and forbidden tool behavior.
- Locale-appropriate sentence endings and sound effects.

Japanese responses use natural Japanese punctuation and Cheshire-style verbal mannerisms rather than Korean sentence endings transliterated into Japanese.

## Testing Strategy

### Unity EditMode tests

- Normalize Korean, Japanese, and English aliases and region tags.
- Fall back to Korean for empty and unsupported values.
- Prefer the locale selected through `SetLanguage.mostRecentLanguage`.
- Load the requested prompt and fall back to Korean when it is absent.
- Compose one request without mixed-language prompt blocks.
- Serialize canonical `locale` in non-streaming and streaming payload construction.
- Verify dynamic prompt templates preserve runtime values in all locales.

### Backend tests

- Parse and normalize the three supported locales while preserving old-client compatibility.
- Add the correct trusted response-language instruction.
- Return localized provider and rate-limit errors.
- Select localized tutor questions, accepted answers, and snippets.
- Fall back to Korean for empty localized tutor cells.
- Grade Korean, Japanese, and English accepted-answer aliases independently.
- Keep tool names and JSON argument keys invariant across locales.

### Content validation

- Verify every required prompt key exists for `ko`, `ja`, and `en`.
- Verify locale files are UTF-8 and non-empty.
- Verify quiz-bank language columns are present and question IDs are unchanged.
- Scan English and Japanese prompt resources for accidental Korean-only control phrases, allowing proper nouns explicitly listed in a small allowlist if needed.

### Manual verification

In a scene containing Cheshire:

1. Select Korean and confirm the next response is Korean.
2. Switch to Japanese without reloading and confirm the next response is Japanese.
3. Switch to English without reloading and confirm the next response is English.
4. Trigger a room hint, a tutor question, and a simulated backend error in each locale.
5. Confirm function calls still execute and no internal JSON appears in displayed dialogue.

## Delivery Sequence

1. Add locale resolution, normalization, and resource catalog with tests.
2. Move Korean prompt sources into the catalog and preserve current Korean behavior.
3. Add Japanese and English prompt content and localize dynamic fragments.
4. Add `locale` to Unity payloads and backend request handling.
5. Localize backend errors and trusted response-language enforcement.
6. Expand tutor quiz/RAG locale selection and grading.
7. Run focused Unity/backend tests, content validation, and the three-language manual matrix.

## Success Criteria

- Existing game language selection is the only locale source used by Cheshire.
- `ko`, `ja`, and `en` each produce a fully localized next Cheshire response without a scene reload.
- No supported locale mixes room, voice, heuristic, or dynamic prompt blocks from different languages except documented Korean fallback for missing content.
- Korean behavior remains compatible with the current game.
- Tutor questions and deterministic grading work in all three languages.
- Backend errors match the request locale.
- Tool calls and structured payload contracts remain backward compatible.
