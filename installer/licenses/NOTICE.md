# Local AI notices

This Windows desktop path may download and run:

- **Gemma 4 E2B** from `litert-community/gemma-4-E2B-it-litert-lm`
  - Google Gemma terms: https://ai.google.dev/gemma/terms
- **LiteRT-LM** CLI (`litert-lm` 0.16.1 via `uvx`)
  - Upstream license: Apache License 2.0
  - Project: https://github.com/google-ai-edge/LiteRT

Approximate first download: **2.4 GB** model artifact. Keep about **15 GB** free disk for caches and headroom.

Loopback only: LiteRT-LM `127.0.0.1:9379`, FastAPI `127.0.0.1:8000`. Do not bind these services to a public interface.

Model files stay on disk after the game exits. Remove them only with:

```powershell
.\scripts\install_local_ai.ps1 -RemoveModel -Execute
```
