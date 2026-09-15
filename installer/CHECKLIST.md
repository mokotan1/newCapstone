# Local AI first-run checklist (Windows)

Use this on a clean machine or after deleting `%USERPROFILE%\.litert-lm`.

1. Confirm Windows desktop, at least 8 GB RAM, and 15 GB free disk.
2. Read `installer/licenses/NOTICE.md` (Gemma terms + LiteRT-LM Apache 2.0).
3. From the repo root, run:

   ```powershell
   .\scripts\install_local_ai.ps1
   ```

   Type `YES` to consent. The script must not download without that consent (or `-Consent`).
4. First install downloads/imports `gemma4-e2b` (~2.4 GB). Watch progress in the console.
5. Re-run the script on an already-installed machine: it should **skip** the download.
6. Interrupted download: if checksum mismatches, delete the partial file and retry.
7. Insufficient disk: the planner must refuse before `uvx` import starts.
8. Offline launch with no model: refuse. Offline with a matching checksum: skip/ready.
9. Start loopback services:

   ```powershell
   .\scripts\install_local_ai.ps1 -Consent -StartServices
   ```

   Poll `http://127.0.0.1:9379/v1/models` and `http://127.0.0.1:8000/` until FastAPI reports `local_runtime.model_available`.
10. Unity `ServerConfig` / chatbot URL `http://127.0.0.1:8000/chat`: chat input stays blocked until that health payload is ready. Cloud/EC2 URLs skip the gate.
11. Dialogue AI off (puzzles still work): set PlayerPrefs `LocalAi.ChatDisabled=1`.
12. Uninstall model only when requested:

    ```powershell
    .\scripts\install_local_ai.ps1 -RemoveModel -Execute
    ```

    Game exit must not delete `%USERPROFILE%\.litert-lm`.
