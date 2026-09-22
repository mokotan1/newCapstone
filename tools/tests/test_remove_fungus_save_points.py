from __future__ import annotations

import pytest

from scene_yaml.remove_fungus_save_points import (
    UnsafeSavePoint,
    remove_save_points,
)

_SAVE_GUID = "0b115619cb83b4d6ab8047d0e9407403"
_GAME_STARTED_GUID = "d2f6487d21a03404cb21b245f0242e79"


def _scene(*, start_first: bool, is_start_point: int, game_started: bool) -> str:
    first = "13" if start_first else "14"
    second = "14" if start_first else "13"
    handler_guid = _GAME_STARTED_GUID if game_started else "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
    return f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &10
GameObject:
  m_Component:
  - component: {{fileID: 11}}
  - component: {{fileID: 12}}
  - component: {{fileID: 13}}
  - component: {{fileID: 14}}
--- !u!114 &11
MonoBehaviour:
  m_Script: {{fileID: 11500000, guid: 3d3d73aef2cfc4f51abf34ac00241f60, type: 3}}
  eventHandler: {{fileID: 12}}
  commandList:
  - {{fileID: {first}}}
  - {{fileID: {second}}}
--- !u!114 &12
MonoBehaviour:
  m_Script: {{fileID: 11500000, guid: {handler_guid}, type: 3}}
--- !u!114 &13
MonoBehaviour:
  m_Script: {{fileID: 11500000, guid: {_SAVE_GUID}, type: 3}}
  isStartPoint: {is_start_point}
  customKey: '{{SavePointKey}}'
--- !u!114 &14
MonoBehaviour:
  m_Script: {{fileID: 11500000, guid: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, type: 3}}
  itemId: 2
"""


def test_removes_start_save_point_and_keeps_the_next_command() -> None:
    updated = remove_save_points(_scene(start_first=True, is_start_point=1, game_started=True))

    assert _SAVE_GUID not in updated
    assert "fileID: 13" not in updated
    assert "customKey" not in updated
    assert "fileID: 14" in updated
    assert "fileID: 12" in updated
    command_list = updated.split("commandList:", 1)[1].split("---", 1)[0]
    assert command_list.strip().startswith("- {fileID: 14}")


def test_refuses_start_save_point_that_is_not_the_first_command() -> None:
    source = _scene(start_first=False, is_start_point=1, game_started=True)

    with pytest.raises(UnsafeSavePoint):
        remove_save_points(source)


def test_refuses_start_save_point_without_game_started() -> None:
    source = _scene(start_first=True, is_start_point=1, game_started=False)

    with pytest.raises(UnsafeSavePoint):
        remove_save_points(source)


def test_removes_non_start_save_point_in_the_middle() -> None:
    updated = remove_save_points(_scene(start_first=False, is_start_point=0, game_started=True))

    assert _SAVE_GUID not in updated
    command_list = updated.split("commandList:", 1)[1].split("---", 1)[0]
    assert "{fileID: 14}" in command_list
    assert "{fileID: 13}" not in command_list
