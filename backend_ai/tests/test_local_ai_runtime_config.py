from __future__ import annotations

import json
from pathlib import Path

from services.local_ai.runtime_config import write_game_runtime_config


def test_gpu_config_writes_game_path_not_user_home(tmp_path: Path) -> None:
    dest = tmp_path / "Disputatio" / "local-ai" / "runtime-config.json"
    written = write_game_runtime_config(
        dest,
        backend="gpu",
        model_id="gemma4-e2b",
        max_num_tokens=2048,
    )
    assert written == dest
    payload = json.loads(dest.read_text(encoding="utf-8"))
    assert payload["models"]["gemma4-e2b"]["backend"] == "gpu"
    assert payload["models"]["gemma4-e2b"]["max_num_tokens"] == 2048
    assert payload["default"]["backend"] == "gpu"
    home_config = Path.home() / ".litert-lm" / "config.json"
    assert dest != home_config
    if home_config.is_file():
        original = home_config.read_bytes()
        assert dest.read_bytes() != original or dest.resolve() != home_config.resolve()


def test_cpu_config_does_not_request_gpu(tmp_path: Path) -> None:
    dest = tmp_path / "runtime-config.json"
    write_game_runtime_config(
        dest,
        backend="cpu",
        model_id="gemma4-e2b",
        max_num_tokens=2048,
    )
    payload = json.loads(dest.read_text(encoding="utf-8"))
    assert payload["default"]["backend"] == "cpu"
    assert payload["models"]["gemma4-e2b"]["backend"] == "cpu"
