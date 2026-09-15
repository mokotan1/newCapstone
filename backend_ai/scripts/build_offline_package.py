#!/usr/bin/env python3
"""Build a folder-style offline Local AI package with SHA-256 manifest."""

from __future__ import annotations

import argparse
import json
import shutil
import sys
from pathlib import Path

_BACKEND = Path(__file__).resolve().parent.parent
if str(_BACKEND) not in sys.path:
    sys.path.insert(0, str(_BACKEND))

from services.local_ai.package_manifest import build_package_manifest
from services.local_embedding import DIMENSION, MODEL_ID

_COPY_IGNORE = shutil.ignore_patterns(
    "__pycache__",
    "*.pyc",
    ".pytest_cache",
    ".ruff_cache",
    ".venv",
    "*.egg-info",
    ".env",
)


def collect_relative_files(root: Path) -> list[str]:
    files: list[str] = []
    for path in sorted(root.rglob("*")):
        if not path.is_file():
            continue
        relative = path.relative_to(root).as_posix()
        if relative == "package_manifest.json":
            continue
        files.append(relative)
    return files


def stage_offline_package(repo: Path, out: Path) -> Path:
    if out.exists():
        shutil.rmtree(out)
    out.mkdir(parents=True)
    shutil.copytree(
        repo / "backend_ai",
        out / "backend_ai",
        ignore=_COPY_IGNORE,
        dirs_exist_ok=True,
    )
    notice = repo / "installer" / "licenses" / "NOTICE.md"
    dest_notice = out / "installer" / "licenses" / "NOTICE.md"
    dest_notice.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(notice, dest_notice)

    pin = json.loads(
        (repo / "backend_ai" / "data" / "local_ai_manifest.json").read_text(
            encoding="utf-8"
        )
    )
    files = collect_relative_files(out)
    manifest = build_package_manifest(
        out,
        files=files,
        embedding_model=MODEL_ID,
        embedding_dimension=DIMENSION,
        model_id=str(pin.get("model_id") or "gemma4-e2b"),
        model_sha256=str(pin.get("artifact_sha256") or ""),
    )
    (out / "package_manifest.json").write_text(
        json.dumps(manifest, indent=2, ensure_ascii=False) + "\n",
        encoding="utf-8",
    )
    launcher = out / "run-supervisor.cmd"
    launcher.write_text(
        "@echo off\r\n"
        "cd /d %~dp0backend_ai\r\n"
        "python -m services.local_ai.supervisor --session-id local "
        "--session-dir %LOCALAPPDATA%\\Disputatio\\local-ai\\sessions "
        "--parent-pid %PPID% --parent-start boot --project-id disputatio\r\n",
        encoding="utf-8",
    )
    return out


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--out",
        type=Path,
        default=_BACKEND.parent / "dist" / "local-ai-package",
    )
    args = parser.parse_args()
    print(stage_offline_package(_BACKEND.parent, args.out))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
