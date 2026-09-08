from __future__ import annotations

from pathlib import Path

from services.local_ai.settings_store import (
    LocalAiSettings,
    load_settings,
    save_settings,
)


def test_load_settings_missing_file_defaults_to_gpu(tmp_path: Path) -> None:
    path = tmp_path / "settings.json"
    settings = load_settings(path)
    assert settings.mode == "gpu"
    assert settings.gpu_offload == "medium"


def test_load_settings_corrupt_file_defaults_to_cpu(tmp_path: Path) -> None:
    path = tmp_path / "settings.json"
    path.write_text("{not-json", encoding="utf-8")
    settings = load_settings(path)
    assert settings.mode == "cpu"


def test_load_settings_rejects_unknown_mode(tmp_path: Path) -> None:
    path = tmp_path / "settings.json"
    path.write_text('{"mode": "turbo", "gpu_offload": "medium"}', encoding="utf-8")
    settings = load_settings(path)
    assert settings.mode == "cpu"


def test_save_settings_is_atomic_and_round_trips(tmp_path: Path) -> None:
    path = tmp_path / "settings.json"
    save_settings(
        path,
        LocalAiSettings(mode="gpu", gpu_offload="high", runtime_id="litert-0.16.1"),
    )
    loaded = load_settings(path)
    assert loaded.mode == "gpu"
    assert loaded.gpu_offload == "high"
    assert loaded.runtime_id == "litert-0.16.1"
    assert not (tmp_path / "settings.json.tmp").exists()
