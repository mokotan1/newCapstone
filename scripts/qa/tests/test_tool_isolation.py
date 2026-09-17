"""Player save/settings isolation (design AC05)."""

from __future__ import annotations

from scripts.qa.tool.isolation import compare_player_store


def _store(**overrides: object) -> dict[str, object]:
    payload: dict[str, object] = {
        "files": {
            "checkpoint.json": {"exists": True, "sha256": "abc"},
        },
        "keys": {
            "BgmVolume": {"exists": True, "valueHash": "vol-a"},
            "Fullscreen": {"exists": True, "valueHash": "fs-1"},
        },
    }
    payload.update(overrides)
    return payload


def test_identical_player_store_after_qa_is_preserved() -> None:
    before = _store()
    after = _store()
    result = compare_player_store(before, after)
    assert result["preserved"] is True
    assert result["reasonCodes"] == []


def test_allowed_qa_key_change_does_not_fail_preservation() -> None:
    before = _store()
    after = _store(
        keys={
            "BgmVolume": {"exists": True, "valueHash": "vol-a"},
            "Fullscreen": {"exists": True, "valueHash": "fs-1"},
            "qa.runId": {"exists": True, "valueHash": "run-2"},
        }
    )
    result = compare_player_store(before, after)
    assert result["preserved"] is True


def test_unauthorized_setting_change_is_not_preserved() -> None:
    before = _store()
    after = _store(
        keys={
            "BgmVolume": {"exists": True, "valueHash": "vol-b"},
            "Fullscreen": {"exists": True, "valueHash": "fs-1"},
        }
    )
    result = compare_player_store(before, after)
    assert result["preserved"] is False
    assert "unauthorized-player-change" in result["reasonCodes"]
    assert "BgmVolume" in result["changedKeys"]


def test_missing_save_file_after_qa_is_not_preserved() -> None:
    before = _store()
    after = _store(files={"checkpoint.json": {"exists": False, "sha256": ""}})
    result = compare_player_store(before, after)
    assert result["preserved"] is False
    assert "unauthorized-player-change" in result["reasonCodes"]
    assert "checkpoint.json" in result["changedFiles"]
