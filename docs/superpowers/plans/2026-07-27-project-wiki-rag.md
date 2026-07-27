# Project Wiki and RAG Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Convert the project's current planning, scenario, report, and technical artifacts into traceable Markdown, publish a navigable in-repository wiki, and build a citation-capable RAG corpus from the approved knowledge pages.

**Architecture:** Original PDFs, PPTX files, HWP files, and existing Markdown remain immutable evidence. A source manifest assigns every in-scope artifact a deterministic Markdown transcript and records its hash, extraction status, category, and RAG eligibility. Curated wiki pages link to those transcripts; a separate generated RAG corpus contains only approved, normalized pages with source IDs and metadata. The FastAPI backend indexes that corpus and retrieves evidence for the explicit `project` or existing `tutor` RAG profile.

**Tech Stack:** Python 3.10, PyYAML, pypdf, python-pptx, Windows Hangul (HWP) COM via pywin32, FastAPI, Gemini `models/text-embedding-004`, pytest, GitHub Actions.

## Global Constraints

- Preserve all originals under `시나리오/`, `기획서/`, `보고서/`, and the root-level project reports; no conversion task deletes, moves, or rewrites an original.
- Treat `시나리오/`, `기획서/`, `보고서/`, `미니게임_구현_리포트.md`, `report.pdf`, and selected existing technical documents in `docs/` as project knowledge sources; do not ingest tool dependency files, test fixtures, generated outputs, or Unity vendor assets.
- Current inventory is 20 PDFs, 12 PPTX files, 7 HWP files, 41 Markdown files, and 13 text files outside Unity-generated directories. The generated manifest, not this count, is the release gate.
- Create a Markdown transcript for every in-scope original, including a PPTX and its corresponding PDF export when both exist. A curated wiki page may consolidate duplicate logical content but must link to both transcripts.
- Every generated Markdown file uses UTF-8, includes YAML front matter with `source_id`, `source_path`, `source_sha256`, `category`, `status`, and `rag_eligible`, and retains page/slide provenance.
- Do not put secrets, API keys, game-player telemetry, or personal report details into the RAG corpus. Weekly reports are preserved as internal wiki transcripts with `rag_eligible: false` unless the project owner later changes the manifest entry.
- Keep generated transcripts and curated wiki pages under `docs/wiki/`; keep generated runtime RAG files under `docs/wiki/rag/`; never make `backend_ai/data/tutor_rag_index.json` the editable source of truth.
- RAG retrieval is evidence-only: the system prompt must tell the model to use retrieved sources, cite source IDs in its answer when a profile requests citations, and state that it lacks evidence when retrieval is empty or insufficient.
- RAG text and all generated manifests must pass encoding validation: no UTF-8 replacement character (`U+FFFD`), no unreadable-character ratio above the agreed threshold, and no blank extraction of a text-bearing source.
- Existing unrelated user edits in `disputatio/Assets/Font/NanumGothic SDF.asset` and `disputatio/Assets/godlotto/KTH/서재버튼.png.meta` are out of scope and must remain untouched.

---

## File Structure

### Knowledge source and wiki tree

- `docs/wiki/_meta/source-manifest.yaml`: one authoritative record for every in-scope artifact and its conversion result.
- `docs/wiki/_meta/source-manifest.schema.json`: JSON Schema for manifest validation.
- `docs/wiki/_meta/coverage-report.md`: generated human-readable conversion and RAG coverage report.
- `docs/wiki/sources/scenario/`: one Markdown transcript per scenario source file.
- `docs/wiki/sources/planning/`: one Markdown transcript per project-planning PDF/PPTX/HWP source file.
- `docs/wiki/sources/reports/`: one Markdown transcript per weekly/capstone report; excluded from RAG by default.
- `docs/wiki/sources/technical/`: normalized copies of selected existing project technical Markdown and text sources.
- `docs/wiki/Home.md`: wiki start page and source-of-truth statement.
- `docs/wiki/Game-Overview.md`, `docs/wiki/Story-and-World.md`, `docs/wiki/Rooms-and-Progression.md`, `docs/wiki/AI-and-Dialogue.md`, `docs/wiki/Architecture.md`, `docs/wiki/Development-History.md`: curated, hand-reviewed navigation pages.
- `docs/wiki/rag/`: generated, metadata-preserving chunks ready for embedding; this folder is the only corpus passed to the RAG index builder.

### Conversion and validation tooling

- `tools/wiki_rag/__init__.py`: package marker.
- `tools/wiki_rag/models.py`: typed manifest and extraction-result data models.
- `tools/wiki_rag/inventory.py`: discovers in-scope artifacts and writes deterministic manifest records.
- `tools/wiki_rag/extract.py`: conversion CLI and extractor registry.
- `tools/wiki_rag/extractors/pdf.py`: PDF page text extraction with explicit page markers.
- `tools/wiki_rag/extractors/pptx.py`: slide, notes, image-caption, and slide-order Markdown extraction.
- `tools/wiki_rag/extractors/hwp.py`: HWP COM text export adapter using the existing HWP automation conventions.
- `tools/wiki_rag/normalize.py`: front matter, filenames, headings, Unicode, links, and provenance normalization.
- `tools/wiki_rag/build_wiki.py`: renders curated navigation pages and the coverage report from reviewed manifest records.
- `tools/wiki_rag/build_rag_corpus.py`: renders only `rag_eligible: true` documents into citation-bearing RAG corpus files.
- `tools/wiki_rag/validate.py`: validates manifest coverage, extraction quality, links, RAG eligibility, and encoding.
- `tools/requirements-wiki-rag.txt`: pinned conversion dependencies separate from runtime API dependencies.
- `tools/tests/test_wiki_rag_*.py`: unit and fixture-based tests for the pipeline.

### Backend and automation changes

- `backend_ai/config.py`: RAG corpus path, index path, profile-specific limits, and an explicit allowed-profile list.
- `backend_ai/scripts/build_tutor_rag_index.py`: reads metadata-rich generated corpus, preserves source metadata in each chunk, and writes atomically.
- `backend_ai/services/tutor_rag_service.py`: locale-aware metadata filtering, score thresholding, deduplication, and citation-ready context blocks.
- `backend_ai/services/chat_service.py`: uses `tutor` and `project` profiles only; injects matching RAG context without allowing client text to impersonate trusted documents.
- `backend_ai/models/requests.py`: validates `rag_profile` against `None`, `tutor`, and `project`.
- `backend_ai/tests/test_project_rag_*.py`: backend profile, metadata, citation, and empty-index tests.
- `.github/workflows/wiki-rag.yml`: validates knowledge coverage and, when API credentials are deliberately supplied in the release environment, builds the production embedding index.
- `docs/wiki/OPERATIONS.md`: repeatable local and release runbook.

---

### Task 1: Establish the source manifest and scope gate

**Files:**
- Create: `tools/wiki_rag/__init__.py`
- Create: `tools/wiki_rag/models.py`
- Create: `tools/wiki_rag/inventory.py`
- Create: `docs/wiki/_meta/source-manifest.schema.json`
- Create: `docs/wiki/_meta/source-manifest.yaml`
- Create: `tools/tests/test_wiki_rag_inventory.py`

**Interfaces:**
- Produces `SourceRecord` with `source_id`, `source_path`, `source_sha256`, `source_type`, `category`, `title`, `transcript_path`, `status`, `rag_eligible`, and `canonical_group`.
- Produces `python tools/wiki_rag/inventory.py --write-manifest docs/wiki/_meta/source-manifest.yaml`.
- Consumes the explicit source roots and exclusion rules in Global Constraints.

- [ ] **Step 1: Write failing inventory tests for scope, duplicate source IDs, and stable hashes**

```python
def test_inventory_includes_each_in_scope_original_once(tmp_path: Path) -> None:
    records = discover_sources(tmp_path, roots=["시나리오", "기획서", "보고서"])
    assert {record.source_path for record in records} == {
        "시나리오/a.pdf", "기획서/b.pptx", "보고서/c.hwp"
    }
    assert all(record.source_sha256 for record in records)

def test_inventory_excludes_generated_and_tool_files(tmp_path: Path) -> None:
    records = discover_sources(tmp_path, roots=["docs", "tools"])
    assert all("docs/wiki/" not in record.source_path for record in records)
    assert all("requirements" not in record.source_path for record in records)
```

- [ ] **Step 2: Run the focused inventory tests to confirm missing module failure**

Run: `pytest tools/tests/test_wiki_rag_inventory.py -q`

Expected: FAIL because `tools.wiki_rag.inventory` and `SourceRecord` do not yet exist.

- [ ] **Step 3: Implement deterministic source discovery and manifest serialization**

Implement `SourceRecord` as a frozen dataclass and use repository-relative POSIX paths. Generate `source_id` as `<category>:<sha256 first 12 characters>`, calculate the complete SHA-256 over original bytes, sort records by `source_path`, and assign the following default categories:

```python
CATEGORY_BY_ROOT = {
    "시나리오": "scenario",
    "기획서": "planning",
    "보고서": "report",
    "docs": "technical",
}
DEFAULT_RAG_ELIGIBILITY = {
    "scenario": True,
    "planning": True,
    "report": False,
    "technical": True,
}
```

Write `source-manifest.yaml` with `schema_version: 1`, an `inputs` section, and a `sources` list. Each source has a deterministic transcript path: `docs/wiki/sources/<category>/<normalized-stem>--<hash12>.md`.

- [ ] **Step 4: Generate and review the initial manifest**

Run:

```powershell
python tools/wiki_rag/inventory.py --repo-root . --write-manifest docs/wiki/_meta/source-manifest.yaml
```

Expected: the manifest lists every artifact selected by the source roots, has no duplicate `source_id` or `transcript_path`, and records the paired PPTX/PDF files separately with a shared `canonical_group` where their normalized basename matches.

- [ ] **Step 5: Run tests and commit the scope gate**

Run: `pytest tools/tests/test_wiki_rag_inventory.py -q`

Expected: PASS.

```powershell
git add tools/wiki_rag docs/wiki/_meta/source-manifest.schema.json docs/wiki/_meta/source-manifest.yaml tools/tests/test_wiki_rag_inventory.py
git commit -m "feat: define project knowledge source manifest"
```

---

### Task 2: Convert PDF and PPTX sources into provenance-preserving Markdown

**Files:**
- Create: `tools/wiki_rag/extractors/__init__.py`
- Create: `tools/wiki_rag/extractors/pdf.py`
- Create: `tools/wiki_rag/extractors/pptx.py`
- Create: `tools/wiki_rag/normalize.py`
- Create: `tools/wiki_rag/extract.py`
- Create: `tools/requirements-wiki-rag.txt`
- Create: `tools/tests/test_wiki_rag_pdf_extraction.py`
- Create: `tools/tests/test_wiki_rag_pptx_extraction.py`

**Interfaces:**
- Produces `ExtractionResult(markdown: str, page_or_slide_count: int, warnings: list[str])`.
- Produces `python tools/wiki_rag/extract.py --manifest docs/wiki/_meta/source-manifest.yaml --types pdf,pptx`.
- Consumes `SourceRecord` and writes only to its manifest-assigned `transcript_path`.

- [ ] **Step 1: Add failing PDF and PPTX fixture tests**

```python
def test_pdf_markdown_has_page_markers_and_source_metadata(pdf_fixture: Path) -> None:
    result = extract_pdf(pdf_fixture)
    assert "<!-- page: 1 -->" in result.markdown
    assert "첫 번째 본문" in result.markdown

def test_pptx_markdown_keeps_slide_order_and_speaker_notes(pptx_fixture: Path) -> None:
    result = extract_pptx(pptx_fixture)
    assert "## Slide 1" in result.markdown
    assert "발표자 메모" in result.markdown
    assert result.page_or_slide_count == 2
```

- [ ] **Step 2: Run focused extractor tests to verify they fail**

Run: `pytest tools/tests/test_wiki_rag_pdf_extraction.py tools/tests/test_wiki_rag_pptx_extraction.py -q`

Expected: FAIL because extractor modules are missing.

- [ ] **Step 3: Implement PDF extraction with a visible quality signal**

Add `pypdf==<tested version>` and `python-pptx==<tested version>` to `tools/requirements-wiki-rag.txt`. In `extract_pdf`, read each page with `PdfReader`, emit `<!-- page: N -->` before its text, normalize line endings, and record a warning `pdf_text_empty_page:<N>` for a page that returns no non-whitespace text. Do not silently run OCR; write the warning to the manifest so scanned pages can be reviewed and retried with an approved OCR tool.

- [ ] **Step 4: Implement PPTX extraction without dropping non-text slide evidence**

For every slide, emit `## Slide N`, then text in shape order, table cells as Markdown tables, and notes under `### Speaker notes`. For an image, chart, SmartArt, or embedded object without usable text, emit an explicit provenance marker such as `> Visual asset present: slide 4, shape 7; inspect original PPTX/PDF.` This prevents the RAG corpus from claiming visual information it cannot read.

- [ ] **Step 5: Add front matter and safe normalization before writing transcripts**

Make `normalize_transcript(record, result)` prepend this exact shape and write atomically through a same-directory temporary file:

```yaml
---
source_id: planning:0123456789ab
source_path: 기획서/example.pptx
source_sha256: 0123456789abcdef
source_type: pptx
category: planning
status: extracted
rag_eligible: true
---
```

The actual values come from the record. Retain Hangul filenames in YAML, escape YAML-special scalar values, normalize headings, and preserve all page/slide comments.

- [ ] **Step 6: Convert the current PDF and PPTX records, inspect failures, and rerun tests**

Run:

```powershell
pip install -r tools/requirements-wiki-rag.txt
python tools/wiki_rag/extract.py --manifest docs/wiki/_meta/source-manifest.yaml --types pdf,pptx
pytest tools/tests/test_wiki_rag_pdf_extraction.py tools/tests/test_wiki_rag_pptx_extraction.py -q
```

Expected: every PDF/PPTX record ends as `extracted` or `needs_review` with a concrete page/slide warning; no record remains unclassified.

- [ ] **Step 7: Commit the PDF/PPTX conversion path**

```powershell
git add tools/wiki_rag docs/wiki/sources/scenario docs/wiki/sources/planning docs/wiki/_meta/source-manifest.yaml tools/requirements-wiki-rag.txt tools/tests/test_wiki_rag_pdf_extraction.py tools/tests/test_wiki_rag_pptx_extraction.py
git commit -m "feat: convert project PDFs and slides to Markdown"
```

---

### Task 3: Convert HWP sources through a tested Windows Hangul adapter

**Files:**
- Create: `tools/wiki_rag/extractors/hwp.py`
- Modify: `tools/hwp-mcp/hwp_mcp/automation.py`
- Create: `tools/tests/test_wiki_rag_hwp_extraction.py`
- Modify: `tools/wiki_rag/extract.py`
- Modify: `docs/wiki/_meta/source-manifest.yaml`

**Interfaces:**
- Produces `export_hwp_plain_text(hwp_path: Path, text_path: Path) -> None` in the existing HWP automation module.
- Produces `extract_hwp(path: Path, exporter: Callable[..., None]) -> ExtractionResult`.
- Guarantees that a missing Hangul COM installation changes the record to `blocked_hwp_com` with a precise remediation message; it does not write an empty transcript.

- [ ] **Step 1: Write failing tests with a fake HWP COM object**

```python
def test_hwp_export_opens_source_exports_text_and_quits(tmp_path: Path) -> None:
    fake = FakeHwp(written_text="주간 보고서 내용")
    export_hwp_plain_text(tmp_path / "source.hwp", tmp_path / "out.txt", hwp_factory=lambda: fake)
    assert fake.opened == tmp_path / "source.hwp"
    assert fake.saved_as == tmp_path / "out.txt"
    assert fake.quit_called is True

def test_hwp_extractor_marks_missing_com_as_blocked(tmp_path: Path) -> None:
    result = extract_hwp(tmp_path / "source.hwp", exporter=raise_missing_hwp)
    assert result.status == "blocked_hwp_com"
    assert result.markdown == ""
```

- [ ] **Step 2: Run HWP extractor tests to confirm the new interface is absent**

Run: `pytest tools/tests/test_wiki_rag_hwp_extraction.py -q`

Expected: FAIL because `export_hwp_plain_text` and `extract_hwp` do not exist.

- [ ] **Step 3: Add text export to the existing HWP COM boundary**

Reuse `_require_windows`, `HWP_COM_PROGID`, `RegisterModule`, message-box suppression, and `Quit` cleanup from `tools/hwp-mcp/hwp_mcp/automation.py`. Open the original read-only, save a text export to a temporary path using Hangul's text export format, close Hangul in `finally`, and decode the export as UTF-8 first then CP949 only if UTF-8 decoding fails. The function must raise `HwpAutomationError` with the original cause when COM creation, document open, or text export fails.

- [ ] **Step 4: Implement the transcript adapter and run a COM preflight**

`extract_hwp` writes a transcript only when the normalized exported text is non-empty. Run:

```powershell
Push-Location tools/hwp-mcp
python -c "from hwp_mcp.automation import probe_hwp_com; print(probe_hwp_com())"
Pop-Location
python tools/wiki_rag/extract.py --manifest docs/wiki/_meta/source-manifest.yaml --types hwp
```

Expected: each HWP record is `extracted`, `needs_review` with a concrete text-quality warning, or `blocked_hwp_com` with the install/permission cause. Preserve every status in the manifest.

- [ ] **Step 5: Review all report transcripts for RAG exclusion and run tests**

Set every `category: report` record to `rag_eligible: false`. Confirm its Markdown transcript remains linked from the internal wiki source list but `build_rag_corpus` will not read it.

Run: `pytest tools/tests/test_wiki_rag_hwp_extraction.py -q`

Expected: PASS.

- [ ] **Step 6: Commit HWP conversion support and transcripts**

```powershell
git add tools/hwp-mcp/hwp_mcp/automation.py tools/wiki_rag/extractors/hwp.py tools/wiki_rag/extract.py docs/wiki/sources/reports docs/wiki/sources/planning docs/wiki/_meta/source-manifest.yaml tools/tests/test_wiki_rag_hwp_extraction.py
git commit -m "feat: convert HWP project documents to Markdown"
```

---

### Task 4: Normalize existing Markdown and text sources, then enforce conversion quality

**Files:**
- Modify: `tools/wiki_rag/extract.py`
- Create: `tools/wiki_rag/validate.py`
- Create: `tools/tests/test_wiki_rag_validation.py`
- Create: `docs/wiki/_meta/coverage-report.md`
- Modify: `docs/wiki/_meta/source-manifest.yaml`

**Interfaces:**
- Produces `python tools/wiki_rag/extract.py --manifest ... --types md,txt`.
- Produces `python tools/wiki_rag/validate.py --manifest ... --write-report docs/wiki/_meta/coverage-report.md`.
- Exit code is nonzero for an unconverted in-scope source, a missing transcript, source-hash drift, invalid UTF-8, unresolved internal link, or an RAG-eligible transcript with no meaningful text.

- [ ] **Step 1: Add failing validation tests for coverage, hash drift, and encoding**

```python
def test_validation_rejects_missing_transcript_and_hash_drift(tmp_path: Path) -> None:
    report = validate_manifest(manifest_with_missing_transcript(tmp_path))
    assert "missing_transcript" in report.error_codes
    assert "source_hash_changed" in report.error_codes

def test_validation_rejects_replacement_character_in_rag_text(tmp_path: Path) -> None:
    transcript = tmp_path / "bad.md"
    transcript.write_text("---\nrag_eligible: true\n---\n깨진 � 문자", encoding="utf-8")
    report = validate_transcript(transcript)
    assert "unicode_replacement_character" in report.error_codes
```

- [ ] **Step 2: Run validation tests to verify the validator is missing**

Run: `pytest tools/tests/test_wiki_rag_validation.py -q`

Expected: FAIL because validation functions do not exist.

- [ ] **Step 3: Copy existing Markdown and text files through the same normalization pipeline**

For selected technical files and existing scenario text, read UTF-8 with BOM support, preserve body content, add required front matter, and use the manifest transcript path rather than editing the original file. Use the current `docs/scenario_extracts/*.txt` only as a previous extraction reference: regenerate its source transcript from the original PDF and leave the old extracts untouched until the coverage report proves their replacement is complete.

- [ ] **Step 4: Implement quality checks and the coverage report**

Validate each source record against its original hash. Validate transcript front matter types, status, `source_id`, `source_path`, and linked source existence. Reject `status: extracted` with fewer than 40 non-whitespace text characters unless the manifest explicitly records `content_kind: visual_only`. Generate a table with source path, type, transcript path, status, warnings, rag eligibility, and canonical group.

- [ ] **Step 5: Run the complete conversion and validation gate**

Run:

```powershell
python tools/wiki_rag/extract.py --manifest docs/wiki/_meta/source-manifest.yaml --types md,txt
python tools/wiki_rag/validate.py --manifest docs/wiki/_meta/source-manifest.yaml --write-report docs/wiki/_meta/coverage-report.md
pytest tools/tests/test_wiki_rag_validation.py -q
```

Expected: validator reports zero errors. Sources marked `needs_review` or `blocked_hwp_com` retain their explicit warning and cause; the report separates those records from silently missing conversion.

- [ ] **Step 6: Commit normalized sources and validation gate**

```powershell
git add tools/wiki_rag docs/wiki/sources/technical docs/wiki/_meta/source-manifest.yaml docs/wiki/_meta/coverage-report.md tools/tests/test_wiki_rag_validation.py
git commit -m "feat: validate project knowledge conversion coverage"
```

---

### Task 5: Build the human-readable wiki and source navigation

**Files:**
- Create: `tools/wiki_rag/build_wiki.py`
- Create: `docs/wiki/Home.md`
- Create: `docs/wiki/Game-Overview.md`
- Create: `docs/wiki/Story-and-World.md`
- Create: `docs/wiki/Rooms-and-Progression.md`
- Create: `docs/wiki/AI-and-Dialogue.md`
- Create: `docs/wiki/Architecture.md`
- Create: `docs/wiki/Development-History.md`
- Create: `docs/wiki/OPERATIONS.md`
- Create: `tools/tests/test_wiki_rag_build_wiki.py`

**Interfaces:**
- Produces `python tools/wiki_rag/build_wiki.py --manifest docs/wiki/_meta/source-manifest.yaml --wiki-root docs/wiki`.
- Curated pages link only with repository-relative Markdown links and cite one or more `source_id` values beside each nontrivial claim.
- Sources pages retain raw transcripts; curated pages summarize, compare, and navigate without duplicating unverified content.

- [ ] **Step 1: Write failing wiki rendering tests**

```python
def test_home_lists_only_extracted_or_reviewable_sources(tmp_path: Path) -> None:
    build_wiki(manifest=sample_manifest(), wiki_root=tmp_path)
    home = (tmp_path / "Home.md").read_text(encoding="utf-8")
    assert "Scenario" in home
    assert "blocked_hwp_com" not in home

def test_curated_page_claim_has_source_link(tmp_path: Path) -> None:
    build_wiki(manifest=sample_manifest(), wiki_root=tmp_path)
    page = (tmp_path / "Story-and-World.md").read_text(encoding="utf-8")
    assert "source_id:" in page
```

- [ ] **Step 2: Run wiki rendering tests to verify the builder is absent**

Run: `pytest tools/tests/test_wiki_rag_build_wiki.py -q`

Expected: FAIL because `build_wiki` does not exist.

- [ ] **Step 3: Implement manifest-driven navigation pages**

Generate `Home.md` with a source-of-truth statement, scope, update command, and category links. Create category indexes that list each transcript's title, source type, status, canonical group, and original-file link. Do not expose reports in the normal public-style navigation; list them only in `Development-History.md` under an internal-materials heading.

- [ ] **Step 4: Curate six top-level project pages from reviewed source transcripts**

Use the following page responsibilities:

```text
Game-Overview.md          product premise, player goal, core loop
Story-and-World.md        setting, timeline, characters, antagonist, lore constraints
Rooms-and-Progression.md  rooms, item/puzzle dependencies, scene progression
AI-and-Dialogue.md        Cheshire character role, prompts, localization, tutor behavior
Architecture.md           Unity, FastAPI, deployment, persistence, tool boundaries
Development-History.md    design evolution and internal report index; no report content in RAG
```

Every curated assertion must end with a source reference in this exact form: `([source_id: scenario:abc123def456](sources/scenario/example--abc123def456.md))`.

- [ ] **Step 5: Document local operations and run the navigation tests**

`OPERATIONS.md` must document inventory, conversion, validation, curation review, RAG corpus build, embedding index build, local backend test, and deployment order. It must state that source edits require a manifest refresh and re-index.

Run: `pytest tools/tests/test_wiki_rag_build_wiki.py -q`

Expected: PASS.

- [ ] **Step 6: Commit the navigable wiki**

```powershell
git add tools/wiki_rag/build_wiki.py docs/wiki tools/tests/test_wiki_rag_build_wiki.py
git commit -m "docs: add project knowledge wiki"
```

---

### Task 6: Generate a restricted, citation-bearing RAG corpus from the wiki

**Files:**
- Create: `tools/wiki_rag/build_rag_corpus.py`
- Create: `tools/tests/test_wiki_rag_corpus.py`
- Create: `docs/wiki/rag/.gitkeep`
- Modify: `docs/wiki/_meta/source-manifest.yaml`
- Modify: `docs/wiki/OPERATIONS.md`

**Interfaces:**
- Produces `python tools/wiki_rag/build_rag_corpus.py --manifest docs/wiki/_meta/source-manifest.yaml --output-dir docs/wiki/rag`.
- Produces one RAG document per eligible source transcript with stable `source_id`, title, category, source path, and normalized text.
- Excludes `rag_eligible: false`, `status: blocked_hwp_com`, missing transcripts, and report-category sources.

- [ ] **Step 1: Write failing corpus selection and provenance tests**

```python
def test_rag_corpus_excludes_reports_and_keeps_source_metadata(tmp_path: Path) -> None:
    written = build_rag_corpus(sample_manifest_with_report(), output_dir=tmp_path)
    assert [path.name for path in written] == ["scenario-abc123.md"]
    text = written[0].read_text(encoding="utf-8")
    assert "source_id: scenario:abc123" in text
    assert "source_path:" in text

def test_rag_corpus_rejects_unreviewed_or_empty_extract(tmp_path: Path) -> None:
    with pytest.raises(CorpusBuildError, match="meaningful text"):
        build_rag_corpus(manifest_with_empty_rag_source(), output_dir=tmp_path)
```

- [ ] **Step 2: Run corpus tests to confirm the generator is missing**

Run: `pytest tools/tests/test_wiki_rag_corpus.py -q`

Expected: FAIL because `build_rag_corpus` does not exist.

- [ ] **Step 3: Implement explicit eligibility and canonical duplicate handling**

Select only records where `status` is `extracted` or `needs_review`, `rag_eligible` is true, category is not `report`, and the normalized transcript has meaningful text. For a PDF/PPTX pair in the same `canonical_group`, include both transcripts only if their normalized body similarity is below 0.90; otherwise include the PPTX transcript as the canonical RAG document and store the PDF `source_id` in `related_source_ids`.

- [ ] **Step 4: Build corpus, inspect its source list, and rerun tests**

Run:

```powershell
python tools/wiki_rag/build_rag_corpus.py --manifest docs/wiki/_meta/source-manifest.yaml --output-dir docs/wiki/rag
python tools/wiki_rag/validate.py --manifest docs/wiki/_meta/source-manifest.yaml --rag-dir docs/wiki/rag
pytest tools/tests/test_wiki_rag_corpus.py -q
```

Expected: every RAG document has source metadata, every included source is traceable to an original, no weekly report transcript appears in `docs/wiki/rag/`, and validation exits 0.

- [ ] **Step 5: Commit the curated RAG corpus generator**

```powershell
git add tools/wiki_rag/build_rag_corpus.py docs/wiki/rag docs/wiki/_meta/source-manifest.yaml docs/wiki/OPERATIONS.md tools/tests/test_wiki_rag_corpus.py
git commit -m "feat: generate approved project RAG corpus"
```

---

### Task 7: Replace the empty tutor index with metadata-aware project RAG indexing

**Files:**
- Modify: `backend_ai/config.py`
- Modify: `backend_ai/scripts/build_tutor_rag_index.py`
- Modify: `backend_ai/services/tutor_rag_service.py`
- Create: `backend_ai/tests/test_project_rag_index_builder.py`
- Modify: `backend_ai/tests/test_tutor_rag_locale.py`
- Modify: `backend_ai/requirements.txt`

**Interfaces:**
- `tutor_rag_corpus_dir` defaults to `../docs/wiki/rag`, resolved from `backend_ai/`.
- Indexed chunks include `id`, `text`, `locale`, `embedding`, `source_id`, `source_path`, `category`, and `title`.
- `TutorRAGService.build_context_block(...)` returns a source ID and source path for every retrieved chunk, never a bare text block.

- [ ] **Step 1: Add failing index-builder tests for front matter, chunk metadata, and atomic output**

```python
def test_builder_preserves_wiki_source_metadata(tmp_path: Path) -> None:
    chunks = load_corpus_chunks(tmp_path)
    assert chunks[0].source_id == "scenario:abc123"
    assert chunks[0].source_path == "시나리오/world.pdf"

def test_builder_does_not_replace_existing_index_when_embedding_fails(tmp_path: Path) -> None:
    old = tmp_path / "index.json"
    old.write_text('{"chunks":["old"]}', encoding="utf-8")
    with pytest.raises(RuntimeError):
        write_index_atomically(old, failing_embeddings())
    assert old.read_text(encoding="utf-8") == '{"chunks":["old"]}'
```

- [ ] **Step 2: Run the builder tests to expose the missing metadata contract**

Run: `cd backend_ai; pytest tests/test_project_rag_index_builder.py tests/test_tutor_rag_locale.py -q`

Expected: FAIL because source metadata is not currently loaded or written.

- [ ] **Step 3: Replace character-only chunking with heading-aware corpus chunking**

Read YAML front matter from each generated RAG Markdown file. Split body by `#`/`##` headings, then pack paragraphs to at most 900 characters while carrying the nearest heading and source metadata. Set each chunk ID to `<source_id>:<section-slug>:<ordinal>`. Reject a file with unknown metadata fields rather than indexing it as untrusted text.

- [ ] **Step 4: Make the builder safe to rerun and preserve the prior usable index**

Write embeddings to `tutor_rag_index.json.tmp`, validate JSON schema and vector count, then replace `tutor_rag_index.json` only after all embeddings succeed. Add `--dry-run` that reports the chunk count and source IDs without calling Gemini, and `--corpus-dir` / `--output-path` overrides for tests.

- [ ] **Step 5: Return citation-ready retrieval context**

Change each context entry to this shape:

```text
--- [1] source_id=scenario:abc123, source_path=시나리오/world.pdf, score=0.842
<retrieved source text>
```

Retain locale filtering, Korean fallback, max-context enforcement, and empty-index behavior. Add a minimum similarity setting; if no retrieved score meets it, return an empty context block.

- [ ] **Step 6: Run all backend RAG unit tests and build the first index only with a configured key**

Run:

```powershell
cd backend_ai
pytest tests/test_project_rag_index_builder.py tests/test_tutor_rag_locale.py -q
python scripts/build_tutor_rag_index.py --dry-run
python scripts/build_tutor_rag_index.py
```

Expected: unit tests pass; dry run lists only eligible source IDs; the production command replaces the current empty `chunks: []` index only after successful embeddings.

- [ ] **Step 7: Commit metadata-aware index generation**

```powershell
git add backend_ai/config.py backend_ai/scripts/build_tutor_rag_index.py backend_ai/services/tutor_rag_service.py backend_ai/tests/test_project_rag_index_builder.py backend_ai/tests/test_tutor_rag_locale.py backend_ai/requirements.txt backend_ai/data/tutor_rag_index.json
git commit -m "feat: index project wiki for grounded retrieval"
```

---

### Task 8: Expose an explicit project RAG profile safely through the chat API

**Files:**
- Modify: `backend_ai/models/requests.py`
- Modify: `backend_ai/services/chat_service.py`
- Modify: `backend_ai/tests/test_chat_request_model.py`
- Modify: `backend_ai/tests/test_tutor_chat_service.py`
- Create: `backend_ai/tests/test_project_rag_chat_service.py`
- Modify: `backend_ai/README.md`
- Modify: `docs/wiki/OPERATIONS.md`

**Interfaces:**
- `ChatRequest.rag_profile` accepts only `None`, `tutor`, or `project`.
- `tutor` keeps quiz-bank injection and tutor token cap; `project` gets wiki RAG only and does not receive quiz-bank overrides.
- Both RAG profiles can include sources only through `TutorRAGService`, and the LLM message builder continues to classify them as trusted external documents.

- [ ] **Step 1: Write failing request and chat-service tests**

```python
def test_chat_request_rejects_unknown_rag_profile() -> None:
    with pytest.raises(ValueError, match="rag_profile"):
        ChatRequest(prompt="hi", rag_profile="anything")

@pytest.mark.asyncio
async def test_project_profile_injects_rag_but_not_quiz_bank() -> None:
    service, provider = build_service_with_fake_rag_and_quiz_bank()
    await service.chat(ChatRequest(prompt="세계관", system="base", use_tools=False, rag_profile="project"))
    user_bundle = "\n".join(message["content"] for message in provider.last_messages if message["role"] == "user")
    assert "source_id=scenario:abc123" in user_bundle
    assert "quiz_bank" not in user_bundle
```

- [ ] **Step 2: Run profile tests to confirm current permissive behavior fails the contract**

Run: `cd backend_ai; pytest tests/test_chat_request_model.py tests/test_project_rag_chat_service.py tests/test_tutor_chat_service.py -q`

Expected: FAIL because arbitrary profile strings are accepted and `project` is not injected.

- [ ] **Step 3: Implement strict profile validation and profile-specific injection**

Use `Literal["tutor", "project"] | None` or an equivalent Pydantic validator. In `_gather_external_documents`, inject RAG for both allowed profiles. Include quiz-bank context and answer override only for `tutor`. In `_max_tokens_for_request`, retain the tutor cap only for `tutor`; `project` uses the normal hard-capped response budget.

- [ ] **Step 4: Add citation instructions without exposing unsupported source content**

When `rag_profile == "project"`, add a server-controlled instruction: cite the supplied `source_id` in square brackets for factual project claims; if no source is present, say the repository knowledge does not establish the answer. Never accept citation instructions from `system`, `prompt`, or `rag_query` as trusted policy.

- [ ] **Step 5: Run backend regression tests and document the API**

Run:

```powershell
cd backend_ai
pytest tests/test_chat_request_model.py tests/test_project_rag_chat_service.py tests/test_tutor_chat_service.py tests/test_tutor_rag_locale.py -q
```

Add `project` request examples to `backend_ai/README.md`, including `rag_profile`, `rag_query`, locale, expected source citation syntax, and the rule that report sources are intentionally absent.

- [ ] **Step 6: Commit the profile boundary**

```powershell
git add backend_ai/models/requests.py backend_ai/services/chat_service.py backend_ai/tests/test_chat_request_model.py backend_ai/tests/test_tutor_chat_service.py backend_ai/tests/test_project_rag_chat_service.py backend_ai/README.md docs/wiki/OPERATIONS.md
git commit -m "feat: expose grounded project wiki RAG profile"
```

---

### Task 9: Add reproducible validation and release automation

**Files:**
- Create: `.github/workflows/wiki-rag.yml`
- Modify: `.github/workflows/backend-build.yml`
- Modify: `docs/wiki/OPERATIONS.md`
- Create: `tools/tests/test_wiki_rag_end_to_end.py`
- Modify: `README.md`

**Interfaces:**
- Pull requests that change in-scope originals, `docs/wiki/**`, `tools/wiki_rag/**`, or backend RAG files run manifest/encoding/link/corpus validation and offline pytest tests.
- Embedding generation runs only when a release environment supplies `GOOGLE_API_KEY`; pull-request CI never requires or prints a production API key.
- `README.md` links to `docs/wiki/Home.md` and `docs/wiki/OPERATIONS.md`.

- [ ] **Step 1: Write a failing offline end-to-end fixture test**

```python
def test_small_knowledge_set_runs_inventory_to_corpus_without_network(tmp_path: Path) -> None:
    manifest = run_inventory(tmp_path)
    run_extraction(manifest, allow_hwp=False)
    build_wiki(manifest, wiki_root=tmp_path / "docs/wiki")
    written = build_rag_corpus(manifest, output_dir=tmp_path / "docs/wiki/rag")
    assert written
    assert validate_manifest(manifest).ok
```

- [ ] **Step 2: Run the end-to-end test to confirm pipeline integration gaps**

Run: `pytest tools/tests/test_wiki_rag_end_to_end.py -q`

Expected: FAIL until the inventory, conversion, wiki, corpus, and validation commands use the same manifest contract.

- [ ] **Step 3: Implement the GitHub Actions workflow with offline and release jobs**

The `validate-knowledge` job installs `tools/requirements-wiki-rag.txt`, runs the manifest validator, runs all `tools/tests/test_wiki_rag_*.py`, builds the RAG corpus, and calls the backend index builder only with `--dry-run`. A protected `build-embeddings` job runs only on a manually approved release environment, passes `GOOGLE_API_KEY` as a masked environment variable, writes the new index, and fails if source IDs or vector counts change unexpectedly without a manifest change.

- [ ] **Step 4: Run local release-equivalent checks**

Run:

```powershell
python tools/wiki_rag/validate.py --manifest docs/wiki/_meta/source-manifest.yaml --rag-dir docs/wiki/rag
pytest tools/tests/test_wiki_rag_*.py -q
cd backend_ai
pytest tests -q
python scripts/build_tutor_rag_index.py --dry-run
```

Expected: all offline tests pass, validation reports every source status, and the backend test suite passes without accessing a real embedding API.

- [ ] **Step 5: Commit automation and entrypoint documentation**

```powershell
git add .github/workflows/wiki-rag.yml .github/workflows/backend-build.yml docs/wiki/OPERATIONS.md README.md tools/tests/test_wiki_rag_end_to_end.py
git commit -m "ci: verify project wiki and RAG corpus"
```

---

## Plan Self-Review Checklist

- Every in-scope original receives a manifest record and a Markdown transcript; originals remain unchanged.
- Markdown conversion, curation, RAG generation, and embedding are separate steps with one authoritative manifest between them.
- PDF, PPTX, HWP, Markdown, and text conversion paths include explicit status and quality checks instead of silent empty output.
- HWP conversion has a Windows Hangul COM path and a visible blocked state when the prerequisite is unavailable.
- Weekly reports remain documented but excluded from RAG unless the owner explicitly changes their manifest eligibility.
- PDF/PPTX pairs retain both transcripts while duplicate RAG ingestion is prevented by canonical grouping and similarity checks.
- Current empty `backend_ai/data/tutor_rag_index.json` is replaced only by an atomically written, validated embedding index.
- `tutor` behavior remains compatible with quiz grading; `project` enables wiki retrieval without quiz side effects.
- Untrusted client text cannot select arbitrary RAG profiles or rewrite trusted RAG instructions.
- Local tests and CI validate coverage and run without a production API key; embedding builds are an explicit release action.
