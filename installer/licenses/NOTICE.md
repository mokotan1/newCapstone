# Local AI notices

This Windows desktop path may run:

- **Gemma 4 E2B** (`gemma-4-E2B-it.litertlm`) from `litert-community/gemma-4-E2B-it-litert-lm`
  - Google Gemma terms: https://ai.google.dev/gemma/terms
- **LiteRT-LM** (`litert-lm` 0.16.1)
  - Upstream license: Apache License 2.0
  - Project: https://github.com/google-ai-edge/LiteRT
- **Local RAG embeddings** identified as `local-hash-v1` (deterministic hashing trick, dimension 256). This is not a third-party neural embedding checkpoint.
- **Quiz bank and RAG index** shipped under `backend_ai/data/`.

Approximate first model import: **2.4 GB** LiteRT artifact. Keep about **15 GB** free disk for caches and headroom. The folder package hashes backend source, the RAG index, and this notice. It does **not** yet vendor a portable Python interpreter or the Gemma weights; those remain a Gate 0 / AC05 packaging gap.

Loopback only: LiteRT-LM and FastAPI bind `127.0.0.1`. Do not bind these services to a public interface.

Model files stay on disk after the game exits. Remove them only with:

```powershell
.\scripts\install_local_ai.ps1 -RemoveModel -Execute
```
