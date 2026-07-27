# Operations

Back to [Home](Home.md).

This page documents the local project-knowledge pipeline order.

## Pipeline order

1. **Inventory** — discover originals and write the manifest:
   `python tools/wiki_rag/inventory.py --repo-root .`
2. **Conversion** — extract transcripts for in-scope sources:
   `python tools/wiki_rag/extract.py --manifest docs/wiki/_meta/source-manifest.yaml`
3. **Validation** — gate coverage, encoding, and link integrity:
   `python tools/wiki_rag/validate.py --manifest docs/wiki/_meta/source-manifest.yaml`
4. **Curation review** — edit curated pages and verify each claim cites a
   manifest `source_id`.
5. **Wiki build** — regenerate navigation pages:
   `python tools/wiki_rag/build_wiki.py --manifest docs/wiki/_meta/source-manifest.yaml --wiki-root docs/wiki`
6. **RAG corpus build** — emit citation-bearing documents for eligible sources:
   `python tools/wiki_rag/build_rag_corpus.py --repo-root . --manifest docs/wiki/_meta/source-manifest.yaml --output-dir docs/wiki/rag`
   Then validate corpus coverage:
   `python tools/wiki_rag/validate.py --repo-root . --manifest docs/wiki/_meta/source-manifest.yaml --rag-dir docs/wiki/rag`
7. **Embedding index build** — (Task 7+) index the RAG corpus for backend
   retrieval.
8. **Local backend test** — run FastAPI tests and a smoke chat against the
   refreshed index.
   - Tutor room: `rag_profile: "tutor"` with `current_question_id`.
   - Project wiki Q&A: `rag_profile: "project"` (no quiz bank, no answer
     override). Example body:

     ```json
     {
       "prompt": "세계관 핵심 설정은?",
       "system": "프로젝트 위키 도우미",
       "use_tools": false,
       "rag_profile": "project",
       "locale": "ko"
     }
     ```

     Expect retrieved chunks to cite `source_id` / `source_path`. Report
     sources remain excluded from the corpus; HWP originals stay out of scope
     until converted.
9. **Deployment** — publish backend images and compose stack after local
   validation passes.

## Source edit policy

Any change to an original document requires:

- manifest refresh (inventory and/or conversion),
- transcript regeneration when bytes change,
- validation,
- wiki rebuild, and
- RAG corpus rebuild plus embedding re-index before relying on retrieval.

## HWP scope

HWP originals are **owner-skipped** and outside conversion scope.
Pending HWP manifest rows remain for inventory traceability but do not
appear on Home or in the RAG corpus until explicitly converted through
a supported path.

## CI and release automation

Pull requests that touch in-scope originals, `docs/wiki/**`,
`tools/wiki_rag/**`, or backend RAG integration files run the offline
**Wiki RAG Validation** workflow (`.github/workflows/wiki-rag.yml`):

1. manifest, transcript, link, encoding, and RAG corpus validation,
2. `pytest tools/tests/test_wiki_rag_*.py`,
3. RAG corpus rebuild plus re-validation,
4. scoped `backend_ai` RAG pytest (dummy API keys only; not the full
   backend suite),
5. `python backend_ai/scripts/build_tutor_rag_index.py --dry-run` (no
   production embedding API calls).

PR CI never requires or prints a production `GOOGLE_API_KEY`.

### Release embedding build

Production embeddings run only from a **manual** `workflow_dispatch` on
`wiki-rag.yml` with `build_embeddings: true`, inside the protected
`wiki-rag-release` GitHub Environment. That job supplies a masked
`GOOGLE_API_KEY` secret, rebuilds `backend_ai/data/tutor_rag_index.json`,
and fails if chunk counts drift without a manifest change.

### Local release-equivalent checks (offline)

```powershell
python tools/wiki_rag/validate.py --repo-root . --manifest docs/wiki/_meta/source-manifest.yaml --rag-dir docs/wiki/rag
pytest tools/tests/test_wiki_rag_*.py -q --basetemp=.pytest_tmp
cd backend_ai
pytest tests/test_project_rag_index_builder.py tests/test_project_rag_chat_service.py tests/test_tutor_rag_locale.py tests/test_chat_request_model.py tests/test_tutor_chat_service.py -q --basetemp=../.pytest_tmp
# Optional: run the full backend suite locally with `pytest tests -q --basetemp=../.pytest_tmp`
python scripts/build_tutor_rag_index.py --dry-run
```

Use the full pipeline order above when refreshing transcripts, wiki
pages, or corpus files locally before opening a PR.
