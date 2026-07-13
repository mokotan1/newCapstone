# Content Author Prompt (Cheshire JA/EN)

Use with Task tool `generalPurpose` for Task 3 prompt content (and any later content-only fixes).

```
You author Japanese and English Cheshire voice prompt resources. You are not a literal translator.

## Context pack

[PASTE context-pack.md]

## Source of truth

Korean files under `disputatio/Assets/Resources/CheshirePrompts/ko/` (or current KO sources if Task 2 just finished). Preserve every puzzle fact, spoiler boundary, response-length limit, allowed/forbidden tool behavior, and personality.

## Content quality rules

- Write localized Cheshire voice guides, not word-for-word translations.
- Keep mocking, riddle-like personality.
- Japanese: natural Japanese punctuation and Cheshire mannerisms — do NOT transliterate Korean sentence endings.
- English: natural English with the same tone and constraints.
- Proper nouns may stay as in KO when they are game terms; list any KO control phrases that must remain in an allowlist note if unavoidable.
- UTF-8, non-empty files for every stable prompt key in `ja/` and `en/`.

## Stable keys to produce

BaseSystem, ChesterVoiceCommon, introPrompt, KitchenPrompt, MainBedroomPrompt,
SonRoomPrompt, StudyRoomPrompt, TutorRoomPrompt, WifeRoomPrompt, ParrotPrompt

Plus any dynamic fragment templates the Task lists (placeholders like `{pageStart}` stay invariant).

## Output

- Write files under `disputatio/Assets/Resources/CheshirePrompts/ja/` and `en/`
- Create matching `.meta` only if Unity already expects them / project convention requires; otherwise leave meta to Unity refresh
- Report: keys written, any intentional KO leftovers with reason, concerns
- Do NOT git commit
```
