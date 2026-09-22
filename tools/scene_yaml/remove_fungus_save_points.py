"""Remove Fungus Save Point commands from a Unity scene YAML text.

A start Save Point is the command SaveManager uses to begin the block.
Those blocks in this project also have a Game Started handler, which
starts at the first remaining command. The Save Point has to be that
first command, or deleting it would run earlier commands on scene load.
"""

from __future__ import annotations

import argparse
import re
from dataclasses import dataclass
from pathlib import Path

SAVE_POINT_GUID = "0b115619cb83b4d6ab8047d0e9407403"
GAME_STARTED_GUID = "d2f6487d21a03404cb21b245f0242e79"

_DOC_HEADER = re.compile(r"^--- !u!(\d+) &(\d+)\n", re.MULTILINE)
_FILE_ID = re.compile(r"\{fileID: (\d+)\}")


class UnsafeSavePoint(ValueError):
    """The scene would not keep the same block start if the command were removed."""


@dataclass(frozen=True)
class _Document:
    start: int
    end: int
    type_id: str
    file_id: str
    body: str


def remove_save_points(text: str) -> str:
    """Return scene YAML with Save Point commands unlinked and deleted."""

    newline = "\r\n" if "\r\n" in text else "\n"
    normalized = text.replace("\r\n", "\n")
    if not normalized.endswith("\n"):
        normalized += "\n"
    documents = _documents(normalized)
    save_points = [
        document
        for document in documents
        if f"guid: {SAVE_POINT_GUID}" in document.body
    ]
    if not save_points:
        return text

    remove_ids = {document.file_id for document in save_points}
    for document in save_points:
        _require_safe(normalized, documents, document)

    kept_parts: list[str] = []
    cursor = 0
    for document in documents:
        if document.file_id in remove_ids:
            kept_parts.append(normalized[cursor:document.start])
            cursor = document.end
    kept_parts.append(normalized[cursor:])
    updated = "".join(kept_parts)
    updated = _drop_references(updated, remove_ids)
    if newline != "\n":
        updated = updated.replace("\n", newline)
    return updated


def remove_save_points_in_scenes(scene_root: Path) -> list[Path]:
    """Rewrite product scenes under scene_root. Fungus example scenes are not here."""

    changed: list[Path] = []
    for path in sorted(scene_root.rglob("*.unity")):
        original = path.read_text(encoding="utf-8")
        if SAVE_POINT_GUID not in original:
            continue
        updated = remove_save_points(original)
        if updated != original:
            path.write_text(updated, encoding="utf-8", newline="")
            changed.append(path)
    return changed


def _documents(text: str) -> list[_Document]:
    matches = list(_DOC_HEADER.finditer(text))
    documents: list[_Document] = []
    for index, match in enumerate(matches):
        end = matches[index + 1].start() if index + 1 < len(matches) else len(text)
        documents.append(
            _Document(
                start=match.start(),
                end=end,
                type_id=match.group(1),
                file_id=match.group(2),
                body=text[match.end():end],
            )
        )
    return documents


def _require_safe(text: str, documents: list[_Document], save_point: _Document) -> None:
    file_id = save_point.file_id
    references = [
        match.group(0)
        for match in re.finditer(rf"\{{fileID: {file_id}\}}", text)
    ]
    component_lines = [
        line
        for line in text.split("\n")
        if line.strip() == f"- component: {{fileID: {file_id}}}"
    ]
    command_lines = [
        line
        for line in text.split("\n")
        if line.strip() == f"- {{fileID: {file_id}}}"
    ]
    if len(component_lines) != 1 or len(command_lines) != 1 or len(references) != 2:
        raise UnsafeSavePoint(
            f"Save Point {file_id} has unexpected references: {references}"
        )

    block = _block_document(documents, file_id)
    is_start = _field(save_point.body, "isStartPoint") == "1"
    if not is_start:
        return

    handler_id = _field(block.body, "eventHandler")
    handler = next((doc for doc in documents if doc.file_id == handler_id), None)
    if handler is None or f"guid: {GAME_STARTED_GUID}" not in handler.body:
        raise UnsafeSavePoint(
            f"Start Save Point {file_id} is not on a Game Started block"
        )
    command_ids = _command_ids(block.body)
    if not command_ids or command_ids[0] != file_id:
        raise UnsafeSavePoint(
            f"Start Save Point {file_id} is not the first command"
        )


def _block_document(documents: list[_Document], file_id: str) -> _Document:
    matches = [
        document
        for document in documents
        if f"- {{fileID: {file_id}}}" in document.body and "commandList:" in document.body
    ]
    if len(matches) != 1:
        raise UnsafeSavePoint(f"Save Point {file_id} is not in exactly one command list")
    return matches[0]


def _command_ids(block_body: str) -> list[str]:
    lines = block_body.split("\n")
    ids: list[str] = []
    in_list = False
    for line in lines:
        if line.startswith("  commandList:"):
            in_list = True
            continue
        if not in_list:
            continue
        if line.startswith("  - {fileID: "):
            match = _FILE_ID.search(line)
            if match is not None:
                ids.append(match.group(1))
            continue
        if line.startswith("  ") and not line.startswith("   "):
            break
    return ids


def _field(body: str, name: str) -> str:
    match = re.search(rf"^  {name}: (.+)$", body, re.MULTILINE)
    if match is None:
        raise UnsafeSavePoint(f"Missing {name}")
    value = match.group(1).strip()
    file_match = _FILE_ID.fullmatch(value)
    if file_match is not None:
        return file_match.group(1)
    return value


def _drop_references(text: str, remove_ids: set[str]) -> str:
    kept: list[str] = []
    for line in text.split("\n"):
        stripped = line.strip()
        if any(
            stripped == f"- component: {{fileID: {file_id}}}"
            or stripped == f"- {{fileID: {file_id}}}"
            for file_id in remove_ids
        ):
            continue
        kept.append(line)
    updated = "\n".join(kept)
    updated = re.sub(
        r"(?m)^  commandList:\n(?!  - \{fileID: )",
        "  commandList: []\n",
        updated,
    )
    return updated


def _parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--scene-root", type=Path, required=True)
    return parser.parse_args()


def main() -> None:
    args = _parse_args()
    changed = remove_save_points_in_scenes(args.scene_root)
    print(f"updated {len(changed)} scenes")
    for path in changed:
        print(path.as_posix())


if __name__ == "__main__":
    main()
