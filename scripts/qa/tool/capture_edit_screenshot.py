"""Edit Mode Scene 뷰 PNG를 런 디렉터리에 저장한다. Play Mode exec는 쓰지 않는다."""

from __future__ import annotations

import os
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
CLI_EXE = Path(os.environ.get("LOCALAPPDATA", "")) / "unity-cli" / "unity-cli.exe"
PNG = (
    "docs/qa/runs/2026-09-17T02-32-43Z-run-hall-to-kitchen/"
    "screenshots/hall-editmode.png"
)
CODE = (
    "var view = UnityEditor.SceneView.lastActiveSceneView;"
    "if (view == null || view.camera == null) return \"no-scene-view\";"
    "var cam = view.camera;"
    "var rt = new UnityEngine.RenderTexture(1280, 720, 24);"
    "var prev = cam.targetTexture;"
    "cam.targetTexture = rt;"
    "cam.Render();"
    "UnityEngine.RenderTexture.active = rt;"
    "var tex = new UnityEngine.Texture2D(1280, 720, UnityEngine.TextureFormat.RGB24, false);"
    "tex.ReadPixels(new UnityEngine.Rect(0, 0, 1280, 720), 0, 0);"
    "tex.Apply();"
    "cam.targetTexture = prev;"
    "UnityEngine.RenderTexture.active = null;"
    "var bytes = tex.EncodeToPNG();"
    "UnityEngine.Object.DestroyImmediate(rt);"
    "UnityEngine.Object.DestroyImmediate(tex);"
    f'var path = System.IO.Path.GetFullPath("{PNG}");'
    "System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));"
    "System.IO.File.WriteAllBytes(path, bytes);"
    "return path;"
)


def main() -> None:
    """Scene 뷰 카메라를 렌더해 PNG를 쓴다. Game 뷰 Play 캡처가 아니다."""
    # TODO(AC19): GetFullPath 상대경로는 Unity 프로젝트(disputatio) 기준이다. 저장소 루트 절대경로를 쓸 것.
    completed = subprocess.run(
        [
            str(CLI_EXE),
            "--project",
            "disputatio",
            "--timeout",
            "30000",
            "exec",
            CODE,
        ],
        cwd=str(ROOT),
        capture_output=True,
        check=False,
    )
    log = ROOT / "docs" / "qa" / "runs" / "_screenshot-last.txt"
    log.write_bytes(
        b"rc="
        + str(completed.returncode).encode("ascii")
        + b"\nstdout:\n"
        + (completed.stdout or b"")
        + b"\nstderr:\n"
        + (completed.stderr or b"")
        + b"\n"
    )
    print("rc", completed.returncode)
    raise SystemExit(completed.returncode)


if __name__ == "__main__":
    main()
