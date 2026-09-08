from __future__ import annotations

import json
from pathlib import Path

from services.local_ai.cuda_manifest import cuda_manifest_is_pinned
from services.local_ai.paths import default_cuda_dir, resolve_cuda_dir


def test_missing_manifest_does_not_enable_cuda(tmp_path: Path) -> None:
    assert cuda_manifest_is_pinned(tmp_path / "missing.json") is False


def test_unpinned_manifest_does_not_enable_cuda(tmp_path: Path) -> None:
    path = tmp_path / "cuda_candidate_manifest.json"
    path.write_text(json.dumps({"gate_passed": False, "runtime": "llama.cpp"}), encoding="utf-8")
    assert cuda_manifest_is_pinned(path) is False


def test_truthy_non_boolean_does_not_enable_cuda(tmp_path: Path) -> None:
    path = tmp_path / "cuda_candidate_manifest.json"
    path.write_text(json.dumps({"gate_passed": "true"}), encoding="utf-8")
    assert cuda_manifest_is_pinned(path) is False


def test_corrupt_manifest_does_not_enable_cuda(tmp_path: Path) -> None:
    path = tmp_path / "cuda_candidate_manifest.json"
    path.write_text("{not-json", encoding="utf-8")
    assert cuda_manifest_is_pinned(path) is False


def test_gate_passed_true_enables_pin(tmp_path: Path) -> None:
    path = tmp_path / "cuda_candidate_manifest.json"
    path.write_text(json.dumps({"gate_passed": True}), encoding="utf-8")
    assert cuda_manifest_is_pinned(path) is True


def test_repo_candidate_manifest_is_pinned_after_gate1() -> None:
    repo_manifest = (
        Path(__file__).resolve().parents[1] / "data" / "cuda_candidate_manifest.json"
    )
    assert repo_manifest.is_file()
    assert cuda_manifest_is_pinned(repo_manifest) is True


def test_default_cuda_dir_is_under_game_local_ai() -> None:
    path = default_cuda_dir()
    assert path.name == "cuda"
    assert path.parent.name == "local-ai"
    assert path.parent.parent.name == "Disputatio"


def test_resolve_cuda_dir_prefers_configured_path(tmp_path: Path) -> None:
    configured = tmp_path / "custom-cuda"
    assert resolve_cuda_dir(str(configured)) == configured
    assert resolve_cuda_dir("  ") == default_cuda_dir()
    assert resolve_cuda_dir("") == default_cuda_dir()
