"""Open Hall_playerble in the Editor (setup only, not a Kitchen skip)."""

from __future__ import annotations

import os
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
SCENE = "Assets/Scenes/Mokotan/First Floor/Hall_playerble.unity"
CODE = (
    "UnityEditor.SceneManagement.EditorSceneManager.OpenScene("
    'string.Concat("Assets/Scenes/Mokotan/First","\\u0020Floor/Hall_playerble.unity")); '
    "return UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().name;"
)
CLI_EXE = Path(os.environ.get("LOCALAPPDATA", "")) / "unity-cli" / "unity-cli.exe"


def main() -> None:
    """Edit Mode에서 Hall_playerble만 연다. Kitchen으로 LoadScene 해서는 안 된다."""
    # TODO(AC19): 경로 공백은 Concat+\\u0020로만 넘긴다. Play Mode exec는 호출하지 말 것.
    completed = subprocess.run(
        [
            str(CLI_EXE),
            "--project",
            "disputatio",
            "--timeout",
            "60000",
            "exec",
            CODE,
        ],
        cwd=str(ROOT),
        capture_output=True,
        check=False,
    )
    out = ROOT / "docs" / "qa" / "runs" / "_open-hall-last.txt"
    out.parent.mkdir(parents=True, exist_ok=True)
    payload = (
        b"rc="
        + str(completed.returncode).encode("ascii")
        + b"\nstdout:\n"
        + (completed.stdout or b"")
        + b"\nstderr:\n"
        + (completed.stderr or b"")
        + b"\n"
    )
    out.write_bytes(payload)
    print("rc", completed.returncode)
    print("wrote output file")
    raise SystemExit(completed.returncode)


if __name__ == "__main__":
    main()
