"""Tests for Start+Fade Flowchart stripping."""

from __future__ import annotations

import shutil
from pathlib import Path

from fungus_deletion.simple_room_strip import strip_start_fade_scene

_FIXTURE = Path(__file__).resolve().parent / "fixtures" / "StartFadeOnly.unity"


def test_strip_fixture_dry_run() -> None:
    result = strip_start_fade_scene(_FIXTURE, dry_run=True)
    assert result.applied is True
    assert result.reason == "dry_run_ok"
    assert result.target_alpha == 1.0
    assert result.host_object_name == "Room_Test"


def test_apply_roundtrip_on_fixture_copy(tmp_path: Path) -> None:
    copy = tmp_path / "StartFadeOnly.unity"
    shutil.copy(_FIXTURE, copy)
    result = strip_start_fade_scene(copy, dry_run=False)
    assert result.applied is True
    text = copy.read_text(encoding="utf-8")
    assert "m_Name: Flowchart" not in text
    assert "0e1f2a3b4c5d6e7f8091a2b3c4d5e6f7" in text
    assert "blockName: Start" not in text


def test_basement_unity_already_stripped() -> None:
    scene = Path("/workspace/disputatio/Assets/Scenes/Mokotan/Basement.unity")
    text = scene.read_text(encoding="utf-8")
    assert "m_Name: Flowchart" not in text
    assert "0e1f2a3b4c5d6e7f8091a2b3c4d5e6f7" in text
