"""Remove root Flowchart with only a Start→FadeScreen path; add BasementRoomEnterFade."""

from __future__ import annotations

import re
from dataclasses import dataclass
from pathlib import Path

_FLOWCHART_SCRIPT_GUID = "7a334fe2ffb574b3583ff3b18b4792d3"
_ENTER_FADE_SCRIPT_GUID = "0e1f2a3b4c5d6e7f8091a2b3c4d5e6f7"
_DEFAULT_DURATION = 1.0
_DEFAULT_TARGET_ALPHA = 0.0

_DOC_SPLIT = re.compile(r"(?=^--- !u!)", re.MULTILINE)
_DOC_HEADER = re.compile(r"^--- !u!(\d+) &(\d+)\r?\n", re.MULTILINE)
_FILE_ID = re.compile(r"\{fileID:\s*(\d+)")
_BLOCK_NAME = re.compile(r"^\s*blockName:\s*(.+)$", re.MULTILINE)
_GAMEOBJECT_NAME = re.compile(r"^\s*m_Name:\s*(.+)$", re.MULTILINE)
_COMPONENT_LIST = re.compile(
    r"^\s*m_Component:\s*\n((?:\s*-\s*component:\s*\{fileID:\s*\d+\}\s*\n)+)",
    re.MULTILINE,
)
_TARGET_ALPHA = re.compile(r"^\s*targetAlpha:\s*([0-9.+-]+)\s*$", re.MULTILINE)
_DURATION = re.compile(r"^\s*duration:\s*([0-9.+-]+)\s*$", re.MULTILINE)


@dataclass(frozen=True)
class StripResult:
    scene_path: str
    applied: bool
    reason: str
    target_alpha: float | None = None
    host_object_name: str | None = None


def _parse_documents(text: str) -> list[tuple[int, int, str]]:
    chunks = [c for c in _DOC_SPLIT.split(text) if c.strip()]
    docs: list[tuple[int, int, str]] = []
    for chunk in chunks:
        if not chunk.startswith("--- !u!"):
            continue
        header = _DOC_HEADER.match(chunk)
        if not header:
            continue
        type_id = int(header.group(1))
        file_id = int(header.group(2))
        docs.append((type_id, file_id, chunk))
    return docs


def _max_file_id(docs: list[tuple[int, int, str]]) -> int:
    return max((file_id for _, file_id, _ in docs), default=1000)


def _flowchart_block_names(text: str) -> list[str]:
    return [m.group(1).strip() for m in _BLOCK_NAME.finditer(text)]


def _is_start_only_flowchart(text: str) -> bool:
    if _FLOWCHART_SCRIPT_GUID not in text:
        return False
    names = _flowchart_block_names(text)
    if not names:
        return False
    return all(name == "Start" for name in names)


def _extract_fade_params(text: str) -> tuple[float, float]:
    alphas = [float(v) for v in _TARGET_ALPHA.findall(text)]
    durations = [float(v) for v in _DURATION.findall(text)]
    target_alpha = alphas[0] if alphas else _DEFAULT_TARGET_ALPHA
    duration = durations[0] if durations else _DEFAULT_DURATION
    return target_alpha, duration


def _gameobject_ids_by_name(docs: list[tuple[int, int, str]]) -> dict[int, str]:
    mapping: dict[int, str] = {}
    for type_id, file_id, chunk in docs:
        if type_id != 1:
            continue
        name_match = _GAMEOBJECT_NAME.search(chunk)
        if name_match:
            mapping[file_id] = name_match.group(1).strip().strip('"')
    return mapping


def _gameobject_for_component(
    docs: list[tuple[int, int, str]], component_id: int
) -> int | None:
    for type_id, file_id, chunk in docs:
        if type_id != 114 or file_id != component_id:
            continue
        match = re.search(r"m_GameObject:\s*\{fileID:\s*(\d+)", chunk)
        if match:
            return int(match.group(1))
    return None


def _components_for_gameobject(
    docs: list[tuple[int, int, str]], gameobject_id: int
) -> list[int]:
    for type_id, file_id, chunk in docs:
        if type_id != 1 or file_id != gameobject_id:
            continue
        comp_match = _COMPONENT_LIST.search(chunk)
        if not comp_match:
            return []
        return [int(m.group(1)) for m in _FILE_ID.finditer(comp_match.group(1))]
    return []


def _transform_for_gameobject(docs: list[tuple[int, int, str]], gameobject_id: int) -> int | None:
    for type_id, file_id, chunk in docs:
        if type_id != 4:
            continue
        match = re.search(r"m_GameObject:\s*\{fileID:\s*(\d+)", chunk)
        if match and int(match.group(1)) == gameobject_id:
            return file_id
    return None


def _collect_flowchart_subtree(
    docs: list[tuple[int, int, str]], flowchart_go_id: int
) -> set[int]:
    remove: set[int] = {flowchart_go_id}
    queue = [flowchart_go_id]
    while queue:
        go_id = queue.pop()
        for comp_id in _components_for_gameobject(docs, go_id):
            remove.add(comp_id)
        for type_id, file_id, chunk in docs:
            if type_id != 4:
                continue
            match = re.search(r"m_GameObject:\s*\{fileID:\s*(\d+)", chunk)
            if not match or int(match.group(1)) != go_id:
                continue
            remove.add(file_id)
            children = _FILE_ID.findall(chunk.split("m_Children:")[-1] if "m_Children:" in chunk else "")
            for child_tid in children:
                child_go = None
                for t2, fid2, ch2 in docs:
                    if t2 == 4 and fid2 == int(child_tid):
                        m = re.search(r"m_GameObject:\s*\{fileID:\s*(\d+)", ch2)
                        if m:
                            child_go = int(m.group(1))
                        break
                if child_go and child_go not in remove:
                    remove.add(child_go)
                    queue.append(child_go)
    return remove


def _pick_host_gameobject(
    docs: list[tuple[int, int, str]], go_names: dict[int, str]
) -> int | None:
    for go_id, name in go_names.items():
        if name.startswith("Room_"):
            return go_id
    for go_id, name in go_names.items():
        if name in {"Flowchart", "Main Camera"}:
            continue
        if name.startswith("SceneBackNavigator"):
            continue
        return go_id
    for go_id, name in go_names.items():
        if name == "Main Camera":
            return go_id
    return None


def _inject_enter_fade(
    docs: list[tuple[int, int, str]],
    host_go_id: int,
    target_alpha: float,
    duration: float,
) -> list[tuple[int, int, str]]:
    new_id = _max_file_id(docs) + 37
    host_chunk_idx = next(i for i, (t, fid, _) in enumerate(docs) if t == 1 and fid == host_go_id)
    type_id, file_id, host_chunk = docs[host_chunk_idx]
    if _ENTER_FADE_SCRIPT_GUID in host_chunk:
        return docs
    comp_line = f"  - component: {{fileID: {new_id}}}\n"
    if "m_Component:" in host_chunk:
        host_chunk = host_chunk.replace("m_Component:\n", "m_Component:\n" + comp_line, 1)
    else:
        raise ValueError("Host GameObject has no m_Component list")
    fade_doc = (
        f"--- !u!114 &{new_id}\n"
        "MonoBehaviour:\n"
        "  m_ObjectHideFlags: 0\n"
        "  m_CorrespondingSourceObject: {fileID: 0}\n"
        "  m_PrefabInstance: {fileID: 0}\n"
        "  m_PrefabAsset: {fileID: 0}\n"
        f"  m_GameObject: {{fileID: {host_go_id}}}\n"
        "  m_Enabled: 1\n"
        "  m_EditorHideFlags: 0\n"
        f"  m_Script: {{fileID: 11500000, guid: {_ENTER_FADE_SCRIPT_GUID}, type: 3}}\n"
        "  m_Name: \n"
        "  m_EditorClassIdentifier: \n"
        f"  durationSeconds: {duration}\n"
        f"  targetAlpha: {target_alpha}\n"
    )
    updated = list(docs)
    updated[host_chunk_idx] = (type_id, file_id, host_chunk)
    updated.append((114, new_id, fade_doc))
    return updated


def _scrub_scene_roots(chunk: str, removed_transform_ids: set[int]) -> str:
    lines = chunk.splitlines(keepends=True)
    out: list[str] = []
    for line in lines:
        if line.strip().startswith("- {fileID:"):
            match = _FILE_ID.search(line)
            if match and int(match.group(1)) in removed_transform_ids:
                continue
        out.append(line)
    return "".join(out)


def strip_start_fade_scene(scene_path: Path, *, dry_run: bool = True) -> StripResult:
    text = scene_path.read_text(encoding="utf-8", errors="replace")
    rel = scene_path.as_posix()
    if not _is_start_only_flowchart(text):
        return StripResult(rel, False, "not_start_only_flowchart")

    target_alpha, duration = _extract_fade_params(text)
    docs = _parse_documents(text)
    go_names = _gameobject_ids_by_name(docs)
    flowchart_ids = [go_id for go_id, name in go_names.items() if name == "Flowchart"]
    if len(flowchart_ids) != 1:
        return StripResult(rel, False, f"expected_one_flowchart_found_{len(flowchart_ids)}")

    flowchart_go_id = flowchart_ids[0]
    remove_ids = _collect_flowchart_subtree(docs, flowchart_go_id)
    removed_transforms = {
        fid
        for t, fid, _ in docs
        if t == 4 and fid in remove_ids
    }

    host_go_id = _pick_host_gameobject(docs, go_names)
    if host_go_id is None:
        return StripResult(rel, False, "no_host_gameobject")

    host_name = go_names.get(host_go_id, "?")
    if dry_run:
        return StripResult(
            rel,
            True,
            "dry_run_ok",
            target_alpha=target_alpha,
            host_object_name=host_name,
        )

    kept = [(t, fid, ch) for t, fid, ch in docs if fid not in remove_ids]
    kept = _inject_enter_fade(kept, host_go_id, target_alpha, duration)
    rebuilt: list[str] = []
    prefix = text.split("--- !u!", 1)[0]
    rebuilt.append(prefix)
    for t, fid, chunk in kept:
        if t == 1660057539 and "SceneRoots:" in chunk:
            chunk = _scrub_scene_roots(chunk, removed_transforms)
        rebuilt.append(chunk if chunk.startswith("---") else "--- !u!" + chunk.lstrip("-"))
    new_text = "".join(rebuilt)
    scene_path.write_text(new_text, encoding="utf-8")
    return StripResult(
        rel,
        True,
        "applied",
        target_alpha=target_alpha,
        host_object_name=host_name,
    )


def strip_start_fade_scenes(scene_paths: list[Path], *, dry_run: bool = True) -> list[StripResult]:
    return [strip_start_fade_scene(path, dry_run=dry_run) for path in scene_paths]


def discover_start_only_scenes(scenes_root: Path) -> list[Path]:
    paths: list[Path] = []
    for scene_path in sorted(scenes_root.glob("**/*.unity")):
        text = scene_path.read_text(encoding="utf-8", errors="replace")
        if _is_start_only_flowchart(text):
            paths.append(scene_path)
    return paths
