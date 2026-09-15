from __future__ import annotations

import json
import sys
from pathlib import Path

from services.local_ai.package_manifest import (
    build_package_manifest,
    verify_package_manifest,
)

_SCRIPTS_DIR = Path(__file__).resolve().parents[1] / "scripts"
if str(_SCRIPTS_DIR) not in sys.path:
    sys.path.insert(0, str(_SCRIPTS_DIR))


def test_manifest_hashes_files_and_rejects_tamper(tmp_path: Path) -> None:
    root = tmp_path / "pkg"
    (root / "backend_ai").mkdir(parents=True)
    target = root / "backend_ai" / "main.py"
    target.write_text("print('ok')\n", encoding="utf-8")
    manifest = build_package_manifest(
        root,
        files=["backend_ai/main.py"],
        embedding_model="local-hash-v1",
        embedding_dimension=256,
        model_id="gemma4-e2b",
        model_sha256="abc",
    )
    path = root / "package_manifest.json"
    path.write_text(json.dumps(manifest), encoding="utf-8")
    assert verify_package_manifest(root, manifest) == []
    target.write_text("tampered\n", encoding="utf-8")
    errors = verify_package_manifest(root, manifest)
    assert errors
    assert any("backend_ai/main.py" in item for item in errors)


def test_stage_offline_package_hashes_backend_tree(tmp_path: Path) -> None:
    from build_offline_package import stage_offline_package

    repo = Path(__file__).resolve().parents[2]
    out = stage_offline_package(repo, tmp_path / "pkg")
    manifest = json.loads((out / "package_manifest.json").read_text(encoding="utf-8"))
    assert (out / "backend_ai" / "main.py").is_file()
    assert (out / "backend_ai" / "services" / "local_ai" / "supervisor.py").is_file()
    assert (out / "run-supervisor.cmd").is_file()
    assert verify_package_manifest(out, manifest) == []
