# Cheshire Multilingual Localization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `.cursor/skills/cheshire-localization-sdd/` with Superpowers `subagent-driven-development` (recommended) or `executing-plans`. Steps use checkbox (`- [ ]`) syntax for tracking. **Do not git commit unless the user asks.**

**Goal:** Make every player-facing Cheshire chat turn follow Fungus language selection (`ko`/`ja`/`en`) without a second locale system or scene reload.

**Architecture:** Read-only `CheshireLocaleResolver` + `CheshirePromptCatalog` under `Assets/Resources/CheshirePrompts/{locale}/`; Unity sends canonical `locale` on chat payloads; backend normalizes, enforces trusted response language, localizes player errors, and selects tutor quiz/RAG by locale with Korean fallback.

**Tech Stack:** Unity 6 C# (mokotan AI), Fungus `SetLanguage`/`Localization`, FastAPI/`ChatRequest`, pytest, EditMode NUnit, unity-cli.

**Design:** `docs/superpowers/specs/2026-07-13-cheshire-multilingual-localization-design.md`  
**Orchestration:** `.cursor/skills/cheshire-localization-sdd/`

---

## File map (create / modify)

| Responsibility | Path |
|----------------|------|
| Locale normalize + resolve | Create `disputatio/Assets/mokotan/mokotan/script/AI/Localization/CheshireLocaleResolver.cs` |
| Prompt resource catalog | Create `disputatio/Assets/mokotan/mokotan/script/AI/Localization/CheshirePromptCatalog.cs` |
| Locale resolver tests | Create `disputatio/Assets/Editor/Tests/EditMode/AI/Localization/CheshireLocaleResolverTests.cs` |
| Catalog tests | Create `disputatio/Assets/Editor/Tests/EditMode/AI/Localization/CheshirePromptCatalogTests.cs` |
| KO/JA/EN prompts | Create `disputatio/Assets/Resources/CheshirePrompts/{ko,ja,en}/*.txt` |
| History / bots / HTTP | Modify `ChatHistoryManager.cs`, room `*Chatbot.cs`, `ParrotChatbot.cs`, `ChatHttpClient.cs` (`LocalLlamaPayload`) |
| Heuristics | Modify `HintInformationPolicy.cs` (+ tests) |
| Backend request | Modify `backend_ai/models/requests.py` |
| Backend locale helpers | Create `backend_ai/services/locale_support.py` (normalize + player messages + response-language line) |
| Chat / defense | Modify `chat_service.py`, `llm_defense/message_builder.py` as needed |
| Tutor | Modify `quiz_bank.py`, `quiz_validation.py`, `tutor_grade.py`, `tutor_rag_service.py`, `quiz_bank.csv` |
| Docs | Update `docs/architecture.md` AI section |

---

### Task 1: Locale resolver + prompt catalog + EditMode tests

**Files:**
- Create: `disputatio/Assets/mokotan/mokotan/script/AI/Localization/CheshireLocaleResolver.cs`
- Create: `disputatio/Assets/mokotan/mokotan/script/AI/Localization/CheshirePromptCatalog.cs`
- Create: `disputatio/Assets/Editor/Tests/EditMode/AI/Localization/CheshireLocaleResolverTests.cs`
- Create: `disputatio/Assets/Editor/Tests/EditMode/AI/Localization/CheshirePromptCatalogTests.cs`
- Create (test fixtures): `disputatio/Assets/Resources/CheshirePrompts/ko/CatalogTestProbe.txt` and `en/CatalogTestProbe.txt` only if needed for Resources.Load tests; prefer injecting a testable load hook instead of permanent probe files.

- [ ] **Step 1: Write failing locale tests**

```csharp
using NUnit.Framework;

[TestFixture]
public class CheshireLocaleResolverTests
{
    [TestCase("KO", "ko")]
    [TestCase("ko-KR", "ko")]
    [TestCase("Korean", "ko")]
    [TestCase("JA", "ja")]
    [TestCase("JP", "ja")]
    [TestCase("ja-JP", "ja")]
    [TestCase("Japanese", "ja")]
    [TestCase("EN", "en")]
    [TestCase("en-US", "en")]
    [TestCase("English", "en")]
    public void NormalizeLocale_MapsAliases(string raw, string expected)
    {
        Assert.AreEqual(expected, CheshireLocaleResolver.NormalizeLocale(raw));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("fr")]
    [TestCase("zh-CN")]
    public void NormalizeLocale_UnsupportedOrEmpty_FallsBackToKo(string raw)
    {
        Assert.AreEqual("ko", CheshireLocaleResolver.NormalizeLocale(raw));
    }

    [Test]
    public void ResolveCurrentLocale_PrefersMostRecentLanguageOverActive()
    {
        string previous = Fungus.SetLanguage.mostRecentLanguage;
        try
        {
            Fungus.SetLanguage.mostRecentLanguage = "en-US";
            Assert.AreEqual("en", CheshireLocaleResolver.ResolveCurrentLocale());
        }
        finally
        {
            Fungus.SetLanguage.mostRecentLanguage = previous;
        }
    }
}
```

- [ ] **Step 2: Run EditMode filter `CheshireLocaleResolverTests` — expect FAIL (type missing)**

```powershell
.\scripts\unity-cli.cmd --project disputatio test --mode EditMode --filter CheshireLocaleResolverTests
```

- [ ] **Step 3: Minimal `CheshireLocaleResolver`**

```csharp
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CheshireLocaleResolver
{
    public const string Korean = "ko";
    public const string Japanese = "ja";
    public const string English = "en";

    public static string NormalizeLocale(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Korean;

        string s = raw.Trim();
        int sep = s.IndexOfAny(new[] { '-', '_' });
        if (sep > 0)
            s = s.Substring(0, sep);
        s = s.Trim().ToLowerInvariant();

        switch (s)
        {
            case "ko":
            case "kr":
            case "korean":
                return Korean;
            case "ja":
            case "jp":
            case "japanese":
                return Japanese;
            case "en":
            case "english":
                return English;
            default:
                // Title-case name aliases already lowercased above; handle "korean" etc.
                return Korean;
        }
    }

    public static string ResolveCurrentLocale()
    {
        if (!string.IsNullOrWhiteSpace(Fungus.SetLanguage.mostRecentLanguage))
            return NormalizeLocale(Fungus.SetLanguage.mostRecentLanguage);

        var loc = UnityEngine.Object.FindObjectOfType<Fungus.Localization>();
        if (loc != null && !string.IsNullOrWhiteSpace(loc.ActiveLanguage))
            return NormalizeLocale(loc.ActiveLanguage);

        return Korean;
    }
}
```

Fix `Korean`/`Japanese`/`English` string aliases before `ToLowerInvariant` if needed (already covered). Ensure `JP` maps via `jp` after lowercasing.

- [ ] **Step 4: Write failing catalog tests** (load via Resources path `CheshirePrompts/{locale}/{key}`)

```csharp
[TestFixture]
public class CheshirePromptCatalogTests
{
    [Test]
    public void Load_RequestedLocalePresent_ReturnsThatText()
    {
        // Requires Task-1 fixture or Step later with real ko file; for pure unit API:
        // Prefer CheshirePromptCatalog.Load(key, locale, loader) for injectability in EditMode.
        Assert.IsNotNull(CheshirePromptCatalog.Load);
    }

    [Test]
    public void Load_MissingLocale_FallsBackToKorean()
    {
        // Arrange: korean text for key "BaseSystem" available; "zz" unsupported → normalize first in callers.
        // Catalog itself: try locale then ko.
    }

    [Test]
    public void Load_MissingBoth_ReturnsEmptyAndDoesNotThrow()
    {
        string text = CheshirePromptCatalog.Load("MissingKeyThatDoesNotExist_XYZ", "en");
        Assert.AreEqual(string.Empty, text);
    }
}
```

Implement catalog with optional `Func<string, TextAsset>` injector for tests if Resources fixtures are awkward in EditMode; production default uses `Resources.Load<TextAsset>`.

- [ ] **Step 5: Implement `CheshirePromptCatalog`**

```csharp
using System;
using UnityEngine;

public static class CheshirePromptCatalog
{
    public const string ResourceRoot = "CheshirePrompts";

    public static Func<string, TextAsset> ResourceLoader { get; set; } =
        path => Resources.Load<TextAsset>(path);

    public static string BuildResourcePath(string locale, string promptKey)
    {
        return $"{ResourceRoot}/{locale}/{promptKey}";
    }

    public static string Load(string promptKey, string locale)
    {
        if (string.IsNullOrWhiteSpace(promptKey))
            return string.Empty;

        string canonical = CheshireLocaleResolver.NormalizeLocale(locale);
        string primary = TryLoadText(BuildResourcePath(canonical, promptKey));
        if (!string.IsNullOrEmpty(primary))
            return primary;

        if (canonical != CheshireLocaleResolver.Korean)
        {
            string fallback = TryLoadText(BuildResourcePath(CheshireLocaleResolver.Korean, promptKey));
            if (!string.IsNullOrEmpty(fallback))
            {
                GameLog.LogWarning(
                    $"[CheshirePromptCatalog] missing '{promptKey}' for locale '{canonical}', using ko");
                return fallback;
            }
        }

        GameLog.LogWarning(
            $"[CheshirePromptCatalog] missing prompt key '{promptKey}' locale '{canonical}' (and ko)");
        return string.Empty;
    }

    private static string TryLoadText(string path)
    {
        TextAsset asset = ResourceLoader(path);
        return asset != null ? asset.text : null;
    }
}
```

- [ ] **Step 6: Re-run both EditMode filters; all green**

- [ ] **Step 7: Do not commit** — report DONE with file list

---

### Task 2: Move Korean prompts into catalog; preserve KO behavior

**Files:**
- Create: `disputatio/Assets/Resources/CheshirePrompts/ko/{BaseSystem,ChesterVoiceCommon,introPrompt,KitchenPrompt,MainBedroomPrompt,SonRoomPrompt,StudyRoomPrompt,TutorRoomPrompt,WifeRoomPrompt,ParrotPrompt}.txt`
- Modify: `ChatHistoryManager.cs`, `GlobalChatbot.cs`, `KitchenChatbot.cs`, `StudyRoomChatbot.cs`, `MainBedroomChatbot.cs`, `SonRoomChatbot.cs`, `WifeRoomChatbot.cs`, `TutorChatbot.cs`, `ParrotChatbot.cs`
- Modify tests that assume flat Resources paths if any

- [ ] **Step 1: Copy existing Korean TextAsset contents into `CheshirePrompts/ko/`**
  - `BaseSystem.txt` ← current `ChatHistoryManager.DefaultSystemMessage` text
  - `ChesterVoiceCommon.txt` ← `Resources/ChesterVoiceCommon.txt` (locate if under Resources; if missing, keep empty + warning path)
  - Room prompts ← existing flat files
  - `ParrotPrompt.txt` ← extract from `ParrotChatbot.BuildFinalSystemPrompt` verbatim

- [ ] **Step 2: Update failing wiring tests** — e.g. extend `ChatHistoryManagerTests` so common rules load via catalog with injected loader returning known text for `CheshirePrompts/ko/ChesterVoiceCommon`

- [ ] **Step 3: Wire production loaders**

```csharp
// ChatHistoryManager.Initialize — use catalog for BaseSystem with locale ko for this task
content = CheshirePromptCatalog.Load("BaseSystem", CheshireLocaleResolver.Korean);

// ComposeSystemPromptWithCommonRules
string common = CheshirePromptCatalog.Load("ChesterVoiceCommon", CheshireLocaleResolver.Korean);

// Room bots
string room = CheshirePromptCatalog.Load("KitchenPrompt", CheshireLocaleResolver.Korean);
```

For Task 2 only, hard-pass `Korean` locale so behavior matches today. Task 4 switches to `ResolveCurrentLocale()`.

- [ ] **Step 4: `ParrotChatbot` loads `ParrotPrompt` from catalog**

- [ ] **Step 5: Keep flat legacy files temporarily** (do not delete in this task) to avoid breaking any overlooked loader; document deprecation in report

- [ ] **Step 6: Run `ChatHistoryManagerTests`, `StudyRoomChatbotTests`, `ParretPanelChatbotBinderTests`**

- [ ] **Step 7: Report DONE — no commit**

---

### Task 3: JA/EN prompt content + dynamic fragment localization

**Files:**
- Create: full `ja/` and `en/` trees for all stable keys
- Modify: `HintInformationPolicy.cs` → `BuildPromptBlock(profile, locale)`
- Modify: dynamic builders in `KitchenChatbot`, `StudyRoomChatbot`, `MainBedroomChatbot`, `SonRoomChatbot`, `WifeRoomChatbot`, `TutorChatbot`, `TutorQuizGrader`, `CheshireHintRewritePlanner` as needed
- Modify: corresponding EditMode tests for multi-locale expectations
- Content author subagent writes JA/EN voice guides first

- [ ] **Step 1: Content author** produces JA/EN files per `content-author-prompt.md`

- [ ] **Step 2: Failing tests for heuristic locale**

```csharp
[Test]
public void BuildPromptBlock_English_DoesNotContainKoreanPolicyHeader()
{
    var profile = new PlayerSkillProfile { level = PlayerSkillLevel.Novice };
    string block = HintInformationPolicy.BuildPromptBlock(profile, "en");
    Assert.IsFalse(block.Contains("[정보량 정책]"));
    Assert.IsTrue(block.Contains("[Information Policy]") || block.Contains("Information"));
}
```

- [ ] **Step 3: Implement locale-aware policy + format templates** with placeholders `{pageStart}`, `{pageEnd}` unchanged

- [ ] **Step 4: Thread one locale through `BuildFinalSystemPrompt` composition** — bots call `CheshireLocaleResolver.ResolveCurrentLocale()` once per build and pass to catalog + fragments

- [ ] **Step 5: Test that composition for locale `en` does not embed known KO policy header when EN resources exist**

- [ ] **Step 6: Update existing KO-asserting tests to pass locale `ko` explicitly**

- [ ] **Step 7: Report DONE — no commit**

---

### Task 4: Add `locale` to Unity payloads and backend `ChatRequest`

**Files:**
- Modify: `ChatHttpClient.cs` (`LocalLlamaPayload` + both request builders)
- Modify: `backend_ai/models/requests.py`
- Modify: `disputatio/Assets/Editor/Tests/EditMode/AI/ChatHttpClientTests.cs`
- Modify: `backend_ai/tests/test_chat_request_model.py`

- [ ] **Step 1: Backend failing tests**

```python
def test_chat_request_accepts_locale_en():
    req = ChatRequest(prompt="hi", locale="en-US")
    assert req.locale == "en"

def test_chat_request_omitted_locale_defaults_ko():
    req = ChatRequest(prompt="hi")
    assert req.locale == "ko"
```

- [ ] **Step 2: Add field + normalizer on `ChatRequest`**

```python
locale: str = "ko"

@field_validator("locale", mode="before")
@classmethod
def _normalize_locale(cls, v: Any) -> str:
    return normalize_locale(v)  # shared helper in locale_support.py
```

- [ ] **Step 3: Unity — add `public string locale;` to `LocalLlamaPayload`; set at request start**

```csharp
string locale = CheshireLocaleResolver.ResolveCurrentLocale();
payload.locale = locale;
```

- [ ] **Step 4: EditMode test asserting serialized JSON contains `"locale":"en"` when resolver forced / payload set**

- [ ] **Step 5: `pytest backend_ai/tests/test_chat_request_model.py -q` + EditMode `ChatHttpClientTests`**

- [ ] **Step 6: Report DONE — no commit**

---

### Task 5: Backend trusted response-language + localized player errors

**Files:**
- Create: `backend_ai/services/locale_support.py` (if not already from Task 4)
- Modify: `backend_ai/services/chat_service.py`
- Modify: `backend_ai/llm_defense/message_builder.py` (trusted language instruction injection)
- Modify: `backend_ai/tests/test_chat_service.py`, `test_llm_defense_message_builder.py`

- [ ] **Step 1: Failing tests for localized errors and language instruction presence**

```python
def test_user_visible_error_rate_limit_en():
    assert "limit" in user_visible_ai_error(exc_429, locale="en").lower() or "rate" in ...

def test_build_messages_includes_response_language_rule_for_ja():
    # messages system channel contains Japanese response-language instruction
```

- [ ] **Step 2: Message catalog keyed by locale for provider failure + rate limit**

- [ ] **Step 3: Trusted instruction** separate from untrusted client `system` / scene_config: e.g. "Respond to the player only in Japanese." for `ja`

- [ ] **Step 4: Wire `request.locale` through `ChatService._build_messages` / `_user_visible_ai_error`**

- [ ] **Step 5: pytest green; do not localize logs/exception details returned to ops**

- [ ] **Step 6: Report DONE — no commit**

---

### Task 6: Tutor quiz / RAG / grading locale columns

**Files:**
- Modify: `backend_ai/data/tutor_quiz/quiz_bank.csv` — add `question_ja`, `question_en`, `acceptable_answers_ko` (migrate from `acceptable_answers`), `acceptable_answers_ja`, `acceptable_answers_en`, `reference_snippet_ko` (migrate), `reference_snippet_ja`, `reference_snippet_en`
- Modify: `quiz_bank.py`, `quiz_validation.py`, `tutor_grade.py`, `chat_service.py` tutor override, `tutor_rag_service.py`
- Modify: `TutorGradeRequest` + Unity `TutorQuizGrader` payload to send `locale`
- Modify/add backend tests for locale selection, empty-cell KO fallback, independent grading aliases

- [ ] **Step 1: Failing grader/bank tests for `en` question text and EN acceptable answers**

- [ ] **Step 2: Expand CSV + loader with KO fallback when JA/EN cell empty**

- [ ] **Step 3: `format_bank_context_block(row, locale)`; RAG filter by locale metadata with KO fallback**

- [ ] **Step 4: Pass locale from Unity tutor grade HTTP body**

- [ ] **Step 5: `pytest` tutor suite green; tool names unchanged**

- [ ] **Step 6: Report DONE — no commit**

---

### Task 7: Verification, content validation, architecture docs

**Files:**
- Create (optional): `backend_ai/scripts/validate_cheshire_prompts.py` or Unity Editor validation test that every key exists for `ko`/`ja`/`en`, UTF-8 non-empty
- Modify: `docs/architecture.md` — document Cheshire locale adapter + `Resources/CheshirePrompts` + `locale` field
- Run full focused verification

- [ ] **Step 1: Content validation — all stable keys present for three locales; scan EN/JA for accidental Korean-only control phrases (allowlist proper nouns)**

- [ ] **Step 2: Unity compile + EditMode filters for Localization + AI chat tests**

```powershell
.\scripts\unity-cli-open-status-cmd.cmd
.\scripts\unity-cli.cmd --project disputatio status
.\scripts\unity-cli.cmd --project disputatio editor refresh --compile
.\scripts\unity-cli.cmd --project disputatio console --type error,warning --lines 80
.\scripts\unity-cli.cmd --project disputatio test --mode EditMode --filter CheshireLocaleResolverTests
.\scripts\unity-cli.cmd --project disputatio test --mode EditMode --filter CheshirePromptCatalogTests
```

- [ ] **Step 3: `ruff check backend_ai` + `pytest backend_ai/tests -q`**

- [ ] **Step 4: Update `docs/architecture.md` AI/Resources sections; note any gaps in §8**

- [ ] **Step 5: Final code-reviewer pass over the whole feature**

- [ ] **Step 6: Report DONE with evidence; no commit unless user asks**

---

## Spec coverage checklist

| Design requirement | Task |
|--------------------|------|
| Locale adapter + normalize | 1 |
| Prompt catalog + KO fallback | 1–2 |
| Move KO sources / Parrot extract | 2 |
| JA/EN content + dynamic fragments | 3 |
| `locale` on Unity + backend | 4 |
| Trusted language + localized errors | 5 |
| Tutor quiz/RAG/grade locale | 6 |
| Tests + content validation + docs | 1–7 |

## Success criteria (from design)

- Fungus language is the only Cheshire locale source
- `ko`/`ja`/`en` each produce a localized next response without reload
- No mixed-language blocks except documented KO fallback
- Korean behavior preserved
- Tutor grading works per locale
- Backend errors match request locale
- Tool/structured contracts backward compatible
