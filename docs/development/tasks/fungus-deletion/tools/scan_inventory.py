#!/usr/bin/env python3
"""Read-only Fungus/scene inventory scanner for P0. Does not modify assets."""

from __future__ import annotations

import csv
import json
import re
import sys
from collections import Counter, defaultdict
from datetime import datetime, timezone
from pathlib import Path

REPO = Path(__file__).resolve().parents[5]
ASSETS = REPO / "disputatio" / "Assets"
BUILD_SETTINGS = REPO / "disputatio" / "ProjectSettings" / "EditorBuildSettings.asset"
OUT_DIR = Path(__file__).resolve().parents[1]

GUID_RE = re.compile(r"guid:\s*([0-9a-f]{32})", re.I)
SCRIPT_GUID_RE = re.compile(r"m_Script:\s*\{fileID:\s*11500000,\s*guid:\s*([0-9a-f]{32})", re.I)
ANY_GUID_RE = re.compile(r"guid:\s*([0-9a-f]{32})", re.I)
FILE_ID_RE = re.compile(r"^--- !u!(\d+)\s+&(-?\d+)", re.M)
SCENE_NAME_RE = re.compile(r"stringVal:\s*(.+)$", re.M)
LOAD_SCENE_CSHARP_RE = re.compile(
    r'(?:LoadSceneSafely|SceneManager\.LoadScene(?:Async)?)\(\s*"([^"]+)"'
)
RESOURCES_LOAD_RE = re.compile(r'Resources\.Load(?:All)?(?:<[^>]+>)?\(\s*"([^"]+)"')
BLOCK_NAME_RE = re.compile(r"^\s+blockName:\s*(.+)$", re.M)

FUNGUS_PREFIXES = (
    "Assets/Fungus/",
    "Assets/FungusExamples/",
)

FLOWCHART_GUID = "7a334fe2ffb574b3583ff3b18b4792d3"
BLOCK_GUID = "3d3d73aef2cfc4f51abf34ac00241f60"
CLICKABLE2D_GUID = "cc03961113fa349c09cb06ef2911013d"
LOADSCENE_GUID = "00d03ae0919f04264b018681ed534177"
INVOKEMETHOD_GUID = "688e35811870d403f9e2b1ab2a699d98"

KNOWN_KEYS = {
    "pressTab",
    "ElectricOn",
    "UsedStudyKey",
    "UsedMaidKey",
    "UsedBedKey",
    "UsedWifeKey",
    "UsedTutorKey",
    "UsedChildKey",
    "WindowClicked",
    "isClicked",
    "CorrectAnswerCount",
    "DevModeEnabled",
    "InventoryItemIds",
    "GetFood",
    "GetFilterCard",
    "GetBookmarkMirror",
    "GetBibleCommentary",
    "HaveMaidKey",
    "HaveBasementKey",
    "HasBible",
    "DiarySolved",
    "HaveTutorKey",
    "GetBottle",
    "BottleClicked",
    "FaucetClicked",
    "BottleDragged",
    "ComeParret",
    "ParretClicked",
    "SceneName",
    "SavePointKey",
    "PrevScene",
}

PRESENTATION = {
    "Say",
    "Menu",
    "GlassMenu",
    "Wait",
    "WaitFrames",
    "FadeScreen",
    "FadeSprite",
    "FadeUI",
    "PlaySound",
    "PlayMusic",
    "StopMusic",
    "SetAudioVolume",
    "PlayUsfxrSound",
    "SetSayDialog",
    "SetMenuDialog",
    "ClearMenu",
    "Portrait",
    "MoveToView",
    "StopCameraSwipes",
    "SetSprite",
    "Show",
    "Hide",
    "SetSortingLayer",
    "Write",
    "SetText",
    "ControlWithDisplay",
    "Comment",
    "DebugLog",
    "TweenUI",
    "iTween",
    "PlayAnimState",
    "SetAnimInteger",
    "SetAnimFloat",
    "SetAnimBool",
    "SetAnimTrigger",
    "ResetAnimTrigger",
    "PlayRegisteredSfx",
    "PlayRandomRegisteredSfx",
    "StopRegisteredSfx",
    "SetBloom",
    "SetVignette",
    "SetColorAdjustments",
    "Stop",
    "StopFlowchart",
    "Destroy",
    "SpawnObject",
    "SetActive",
    "SetClickable2D",
    "SetDraggable2D",
    "SetInteractable",
    "SetCollider",
    "ShakeCamera",
    "StartSwiping",
    "StopSwiping",
    "FadeToView",
    "TalkStandingCommand",
    "ControlAudio",
    "ControlStage",
    "Conversation",
    "ShowSprite",
    "SetUIImage",
    "TransformProperty",
    "ScaleLean",
    "RotateLean",
    "MoveLean",
    "ScaleAdd",
    "MoveAdd",
    "MoveTo",
    "MoveFrom",
    "ScaleTo",
    "ScaleFrom",
    "RotateTo",
    "RotateFrom",
    "PunchPosition",
    "PunchRotation",
    "PunchScale",
    "ShakePosition",
    "ShakeScale",
    "ShakeRotation",
    "LookFrom",
    "LookTo",
    "MenuShuffle",
    "MenuTimer",
    "SetMouseCursor",
    "FungusPriorityDecrease",
    "FungusPriorityIncrease",
}

DOMAIN = {
    "SetVariable",
    "If",
    "IfVariable",
    "Else",
    "ElseIf",
    "End",
    "While",
    "AddItemToInventory",
    "CompleteTutorialQuestStep",
    "Reset",
    "DeleteSave",
}

SERVICE = {
    "LoadScene",
    "Reload",
    "ReloadScene",
    "SetLanguage",
    "SavePoint",
    "SaveVariable",
    "LoadVariable",
    "InvokeMethod",
    "InvokeEvent",
    "Call",
    "CallMethod",
    "SendMessage",
    "DestroyOnLoad",
}

RETIRE = {
    "Break",
    "OpenURL",
    "ReadTextFile",
    "Lua",
    "LuaCondition",
    "ExecuteLua",
    "FromLua",
    "ToLua",
    "CollectionRandomBag",
    "CollectionCommandUnique",
    "CollectionCommandClear",
    "PhysicsCast",
    "GameObjectProperty",
    "GetText",
    "GetMousePosition",
    "GetAxis",
    "Vector3Arithmetic",
    "Vector3Fields",
    "RandomFloat",
    "RandomInteger",
    "Round",
    "MinMax",
    "Sqrt",
    "Exp",
    "Log",
    "Trig",
    "Map",
    "Curve",
    "AddForce2D",
    "AddTorque2D",
    "StopMotionRigidBody2D",
    "LoopRange",
    "ForEach",
    "Label",
    "Jump",
}


def posix(path: Path) -> str:
    return path.as_posix().replace("\\", "/")


def rel_asset(path: Path) -> str:
    try:
        return "Assets/" + path.relative_to(ASSETS).as_posix()
    except ValueError:
        return posix(path)


def index_metas() -> dict[str, dict]:
    mapping: dict[str, dict] = {}
    for meta in ASSETS.rglob("*.meta"):
        text = meta.read_text(encoding="utf-8", errors="replace")
        match = GUID_RE.search(text)
        if not match:
            continue
        guid = match.group(1)
        asset = meta.with_suffix("")
        asset_rel = rel_asset(asset) if asset.exists() else rel_asset(meta)[:-5]
        mapping[guid] = {
            "guid": guid,
            "assetPath": asset_rel,
            "metaPath": rel_asset(meta),
            "className": asset.stem if asset.suffix == ".cs" else asset.name,
            "isFungusVendor": asset_rel.startswith(FUNGUS_PREFIXES),
        }
    return mapping


def parse_build_settings() -> list[dict]:
    text = BUILD_SETTINGS.read_text(encoding="utf-8", errors="replace")
    scenes = []
    blocks = re.split(r"\n  - ", text)
    for block in blocks[1:]:
        enabled = re.search(r"enabled:\s*(\d)", block)
        path = re.search(r"path:\s*(.+)", block)
        guid = re.search(r"guid:\s*([0-9a-f]{32})", block, re.I)
        if not path:
            continue
        scenes.append(
            {
                "path": path.group(1).strip(),
                "enabled": enabled.group(1) == "1" if enabled else False,
                "guid": guid.group(1) if guid else "",
            }
        )
    return scenes


def split_unity_docs(text: str) -> list[tuple[str, str, str]]:
    docs = []
    matches = list(FILE_ID_RE.finditer(text))
    for i, match in enumerate(matches):
        start = match.start()
        end = matches[i + 1].start() if i + 1 < len(matches) else len(text)
        docs.append((match.group(1), match.group(2), text[start:end]))
    return docs


def classify_command(class_name: str) -> tuple[str, str]:
    if class_name in PRESENTATION:
        if class_name in {"Comment", "DebugLog", "Stop", "StopFlowchart"}:
            return "retire-with-evidence", "연출 없음/디버그. 패킷에서 사용처 없으면 폐기"
        return "sequence-presentation", "대사·연출·SFX 순서는 Sequence/UI"
    if class_name in DOMAIN:
        return "csharp-rule", "퍼즐/진행/아이템/조건은 C# 상태 소유자"
    if class_name in SERVICE:
        if class_name == "LoadScene":
            return "existing-service", "SceneTransitionService만 씬 로드"
        if class_name == "SetLanguage":
            return "existing-service", "locale 설정 소유자"
        if class_name in {"SavePoint", "SaveVariable", "LoadVariable"}:
            return "existing-service", "Checkpoint 계층"
        return "existing-service", "기존 C# 서비스 호출로 대체"
    if class_name in RETIRE:
        return "retire-with-evidence", "Lua/유틸/미사용 후보는 근거 확인 후 폐기"
    if class_name.endswith("Variable") or class_name in {"BooleanVariable", "IntegerVariable", "StringVariable", "FloatVariable"}:
        return "csharp-rule", "변수 정의. 영속 키는 상태 저장소, 지역은 Sequence"
    if "Menu" in class_name or "Say" in class_name or "Fade" in class_name:
        return "sequence-presentation", "이름 기반 연출 분류"
    if class_name in {"Flowchart", "Block", "Command", "EventHandler", "GameStarted", "GameStarted"}:
        return "existing-service", "Fungus 런타임 골격. 실행기 교체 대상"
    if class_name in {"Clickable2D", "Draggable2D", "DragCancelled", "ObjectClicked"}:
        return "existing-service", "입력 컴포넌트. Interaction 경로로 이전"
    return "csharp-rule", "미표 명령은 기본 C# 규칙 후보. 패킷에서 재확인"


def classify_variable(key: str, scope: str) -> dict:
    owner = "unspecified-owner"
    if key in KNOWN_KEYS:
        if key in {"SceneName", "SavePointKey", "PrevScene"}:
            owner = "SceneNameSetter/BackNavigator"
            disposition = "existing-service"
        elif key == "isClicked":
            owner = "ClickInteractionCleanup"
            disposition = "existing-service"
        elif key == "pressTab":
            owner = "InventoryManager"
            disposition = "csharp-rule"
        elif key in {
            "GetBottle",
            "BottleClicked",
            "FaucetClicked",
            "BottleDragged",
            "ComeParret",
            "ParretClicked",
        }:
            owner = "KitchenPuzzleState"
            disposition = "csharp-rule"
        else:
            owner = "FungusVariableKeys/Variablemanager"
            disposition = "csharp-rule"
    elif scope == "2":
        owner = "Variablemanager-global"
        disposition = "csharp-rule"
    elif scope == "0":
        owner = "flowchart-private"
        disposition = "sequence-presentation"
    else:
        owner = "flowchart-public"
        disposition = "csharp-rule"
    return {
        "key": key,
        "scope": scope,
        "owner": owner,
        "disposition": disposition,
        "state": "planned",
    }


def scan_yaml_asset(path: Path, guid_index: dict[str, dict]) -> dict:
    text = path.read_text(encoding="utf-8", errors="replace")
    asset_rel = rel_asset(path)
    docs = split_unity_docs(text)
    by_id: dict[str, dict] = {}
    fungus_script_hits: list[dict] = []
    input_modes = set()

    for class_id, file_id, body in docs:
        script_match = SCRIPT_GUID_RE.search(body)
        script_guid = script_match.group(1) if script_match else ""
        meta = guid_index.get(script_guid, {})
        class_name = meta.get("className", "")
        is_vendor = bool(meta.get("isFungusVendor"))
        asset_path = meta.get("assetPath", "")
        is_custom_command = "FungusCommands/" in asset_path or "GuardedClickable" in class_name
        is_fungus = is_vendor or is_custom_command or script_guid in {
            FLOWCHART_GUID,
            BLOCK_GUID,
            CLICKABLE2D_GUID,
        }
        record = {
            "fileId": file_id,
            "unityClassId": class_id,
            "scriptGuid": script_guid,
            "className": class_name,
            "isFungus": is_fungus or bool(meta.get("isFungusVendor")),
            "body": body,
        }
        by_id[file_id] = record
        if record["isFungus"] and script_guid:
            fungus_script_hits.append(
                {
                    "fileId": file_id,
                    "scriptGuid": script_guid,
                    "className": class_name,
                    "assetPath": meta.get("assetPath", ""),
                }
            )
        if script_guid == CLICKABLE2D_GUID or class_name in {"Clickable2D", "GuardedClickable2D"}:
            input_modes.add("legacy-clickable2d")
        if "m_Name: EventSystem" in body or class_name == "EventSystem":
            input_modes.add("ui-eventsystem")
        if class_name in {"Button", "EventTrigger"}:
            input_modes.add("ui-button")
        if class_name in {"Draggable", "Draggable2D", "DraggableSnap2D", "FilterCardBoundedDrag"}:
            input_modes.add("drag-drop")
        if class_name in {"Collider2D", "BoxCollider2D", "PolygonCollider2D", "CircleCollider2D"}:
            input_modes.add("physics2d-collider")

    if re.search(r"\bEventSystem\b", text):
        input_modes.add("ui-eventsystem")
    if "BoxCollider2D" in text or "PolygonCollider2D" in text:
        input_modes.add("physics2d-collider")
    if re.search(r"\bButton\b", text) and "m_OnClick" in text:
        input_modes.add("ui-button")

    blocks = []
    commands = []
    variables = []
    load_targets = []
    invoke_methods = []
    flowcharts = 0

    for rec in by_id.values():
        class_name = rec["className"]
        body = rec["body"]
        if rec["scriptGuid"] == FLOWCHART_GUID or class_name == "Flowchart":
            flowcharts += 1
        if rec["scriptGuid"] == BLOCK_GUID or class_name == "Block":
            name_match = re.search(r"blockName:\s*(.+)", body)
            cmd_ids = re.findall(r"commandList:\n((?:\s+-\s*\{fileID:\s*-?\d+\}\n?)+)", body)
            ids = []
            if cmd_ids:
                ids = re.findall(r"fileID:\s*(-?\d+)", cmd_ids[0])
            else:
                ids = re.findall(r"commandList:.*?(?:\n  [a-zA-Z]|\n---)", body, re.S)
                ids = re.findall(r"fileID:\s*(-?\d+)", body.split("commandList:", 1)[-1].split("\n  ", 1)[0]) if "commandList:" in body else []
            cmd_classes = []
            for cid in ids:
                child = by_id.get(cid)
                if child:
                    cmd_classes.append(child["className"] or child["scriptGuid"])
            dispositions = [classify_command(c.split(".")[0])[0] for c in cmd_classes]
            unique = sorted(set(dispositions))
            if not unique:
                block_disp = "retire-with-evidence"
                note = "빈 블록"
            elif len(unique) == 1:
                block_disp = unique[0]
                note = "명령 분류 단일"
            else:
                block_disp = "csharp-rule"
                note = "혼합 블록: " + ",".join(unique) + " — 규칙/연출 분리 대상"
            blocks.append(
                {
                    "fileId": rec["fileId"],
                    "blockName": name_match.group(1).strip() if name_match else "",
                    "commandFileIds": ids,
                    "commandClasses": cmd_classes,
                    "disposition": block_disp,
                    "dispositionNote": note,
                    "state": "planned",
                }
            )
        looks_like_command = "itemId:" in body and "indentLevel:" in body and "blockName:" not in body
        if looks_like_command and rec["scriptGuid"] not in {FLOWCHART_GUID, BLOCK_GUID}:
                disp, note = classify_command(class_name)
                cmd = {
                    "fileId": rec["fileId"],
                    "className": class_name,
                    "scriptGuid": rec["scriptGuid"],
                    "disposition": disp,
                    "dispositionNote": note,
                    "state": "planned",
                }
                if rec["scriptGuid"] == LOADSCENE_GUID or class_name == "LoadScene":
                    sm = SCENE_NAME_RE.search(body)
                    if sm:
                        target = sm.group(1).strip()
                        cmd["sceneName"] = target
                        load_targets.append(target)
                if rec["scriptGuid"] == INVOKEMETHOD_GUID or class_name == "InvokeMethod":
                    method = re.search(r"methodName:\s*(.+)", body)
                    target = re.search(r"targetObject:\s*\{fileID:\s*(-?\d+)", body)
                    cmd["methodName"] = method.group(1).strip() if method else ""
                    cmd["targetFileId"] = target.group(1) if target else ""
                    invoke_methods.append(cmd.copy())
                commands.append(cmd)
        if "\n  key:" in body and "\n  scope:" in body:
            key_m = re.search(r"^\s+key:\s*(.+)$", body, re.M)
            scope_m = re.search(r"^\s+scope:\s*(.+)$", body, re.M)
            if key_m:
                key = key_m.group(1).strip()
                scope = scope_m.group(1).strip() if scope_m else ""
                var = classify_variable(key, scope)
                var["fileId"] = rec["fileId"]
                var["className"] = class_name
                variables.append(var)

    # C# interaction outcomes in the same YAML
    outcome_scenes = []
    for match in re.finditer(
        r"loadScene:\s*1\n\s+sceneName:\s*(.+)",
        text,
    ):
        outcome_scenes.append(match.group(1).strip())
    load_targets.extend(outcome_scenes)

    routes = []
    for match in re.finditer(
        r"interactionId:\s*(.+)\n\s+fungusBlockName:\s*(.+)",
        text,
    ):
        routes.append(
            {
                "interactionId": match.group(1).strip(),
                "fungusBlockName": match.group(2).strip(),
            }
        )

    referenced_guids = sorted(set(ANY_GUID_RE.findall(text)))
    fungus_guids = [
        g
        for g in referenced_guids
        if guid_index.get(g, {}).get("isFungusVendor")
        or g in {FLOWCHART_GUID, BLOCK_GUID, CLICKABLE2D_GUID}
    ]

    return {
        "assetPath": asset_rel,
        "kind": "scene" if path.suffix == ".unity" else "prefab" if path.suffix == ".prefab" else "asset",
        "flowcharts": flowcharts,
        "blocks": blocks,
        "commands": commands,
        "variables": variables,
        "loadTargets": sorted(set(t for t in load_targets if t and t not in {"{fileID: 0}", ""})),
        "invokeMethods": invoke_methods,
        "routes": routes,
        "inputModes": sorted(input_modes),
        "fungusComponentCount": len(fungus_script_hits),
        "fungusScriptGuids": sorted({h["scriptGuid"] for h in fungus_script_hits}),
        "fungusGuidRefs": fungus_guids,
        "oldPathRemoved": False,
        "state": "planned",
        "disposition": "keep" if not asset_rel.startswith(FUNGUS_PREFIXES) else "retire-pending",
    }


def scan_csharp(guid_index: dict[str, dict]) -> dict:
    using_files = []
    load_edges = []
    resource_loads = []
    for cs in ASSETS.rglob("*.cs"):
        rel = rel_asset(cs)
        if rel.startswith(FUNGUS_PREFIXES):
            continue
        text = cs.read_text(encoding="utf-8", errors="replace")
        if re.search(r"\busing\s+Fungus\s*;", text) or re.search(r"\bFungus\.", text):
            using_files.append(rel)
        for scene in LOAD_SCENE_CSHARP_RE.findall(text):
            load_edges.append({"fromFile": rel, "sceneName": scene})
        for res in RESOURCES_LOAD_RE.findall(text):
            resource_loads.append({"fromFile": rel, "resource": res})
    asmdefs = []
    for asm in ASSETS.rglob("*.asmdef"):
        rel = rel_asset(asm)
        text = asm.read_text(encoding="utf-8", errors="replace")
        if "Fungus" in text:
            asmdefs.append(rel)
    return {
        "csharpFungusRefs": using_files,
        "csharpLoadEdges": load_edges,
        "resourceLoads": resource_loads,
        "asmdefsWithFungus": asmdefs,
    }


def remaining_guid_refs(guid_index: dict[str, dict], fungus_guids: set[str]) -> dict[str, list[str]]:
    hits: dict[str, list[str]] = defaultdict(list)
    scan_suffixes = {".unity", ".prefab", ".asset", ".controller", ".overrideController", ".playable", ".asmdef", ".cs", ".json", ".uxml", ".uss"}
    for path in ASSETS.rglob("*"):
        if path.suffix not in scan_suffixes:
            continue
        rel = rel_asset(path)
        if rel.startswith(FUNGUS_PREFIXES):
            continue
        if "Library/" in rel or "/Editor/Tests/" in rel and False:
            pass
        try:
            text = path.read_text(encoding="utf-8", errors="replace")
        except OSError:
            continue
        found = set(ANY_GUID_RE.findall(text)) & fungus_guids
        if "using Fungus" in text or "namespace Fungus" in text:
            hits[rel].append("using-or-namespace-Fungus")
        for guid in sorted(found):
            hits[rel].append(guid)
    return dict(hits)


def main() -> int:
    started = datetime.now(timezone.utc).isoformat()
    guid_index = index_metas()
    fungus_guids = {g for g, info in guid_index.items() if info["isFungusVendor"]}
    fungus_guids.update({FLOWCHART_GUID, BLOCK_GUID, CLICKABLE2D_GUID})

    build = parse_build_settings()
    build_paths = {row["path"] for row in build}

    scene_paths = sorted(ASSETS.rglob("*.unity"))
    prefab_paths = sorted(
        p
        for p in ASSETS.rglob("*.prefab")
        if not rel_asset(p).startswith(FUNGUS_PREFIXES)
    )

    scenes = []
    for path in scene_paths:
        scenes.append(scan_yaml_asset(path, guid_index))

    shared = []
    for path in prefab_paths:
        scanned = scan_yaml_asset(path, guid_index)
        if scanned["fungusComponentCount"] or scanned["fungusGuidRefs"] or scanned["blocks"]:
            scanned["kind"] = "sharedPrefab"
            shared.append(scanned)

    csharp = scan_csharp(guid_index)
    guid_hits = remaining_guid_refs(guid_index, fungus_guids)

    all_blocks = []
    all_commands = []
    all_vars = []
    edges = []
    unclassified_commands = 0
    for scene in scenes:
        scene_name = Path(scene["assetPath"]).stem
        scene["buildEnabled"] = scene["assetPath"] in build_paths
        scene["inBuildSettings"] = scene["assetPath"] in build_paths
        for block in scene["blocks"]:
            row = dict(block)
            row["assetPath"] = scene["assetPath"]
            all_blocks.append(row)
        for cmd in scene["commands"]:
            row = dict(cmd)
            row["assetPath"] = scene["assetPath"]
            all_commands.append(row)
            if not cmd.get("disposition"):
                unclassified_commands += 1
        for var in scene["variables"]:
            row = dict(var)
            row["assetPath"] = scene["assetPath"]
            all_vars.append(row)
        for target in scene["loadTargets"]:
            edges.append(
                {
                    "from": scene_name,
                    "to": target,
                    "via": "fungus-or-blockoutcome",
                    "fromPath": scene["assetPath"],
                    "state": "planned",
                }
            )

    for edge in csharp["csharpLoadEdges"]:
        edges.append(
            {
                "from": Path(edge["fromFile"]).stem,
                "to": edge["sceneName"],
                "via": "csharp",
                "fromPath": edge["fromFile"],
                "state": "planned",
            }
        )

    command_types = Counter(c["className"] for c in all_commands)
    input_mode_counts = Counter()
    for scene in scenes:
        for mode in scene["inputModes"]:
            input_mode_counts[mode] += 1

    keep_scenes = [s for s in scenes if s["disposition"] == "keep"]
    summary = {
        "startedUtc": started,
        "finishedUtc": datetime.now(timezone.utc).isoformat(),
        "revisionNote": "working-tree scan on feature/fungus-deletion-framework",
        "sceneCount": len(scenes),
        "buildSceneCount": len(build),
        "scenesNotInBuild": [
            s["assetPath"] for s in scenes if s["assetPath"] not in build_paths
        ],
        "buildScenesMissingFile": [
            row["path"] for row in build if not (REPO / "disputatio" / row["path"]).exists()
        ],
        "flowchartCount": sum(s["flowcharts"] for s in scenes),
        "blockCount": len(all_blocks),
        "commandCount": len(all_commands),
        "variableCount": len(all_vars),
        "sharedPrefabWithFungus": len(shared),
        "csharpFungusFileCount": len(csharp["csharpFungusRefs"]),
        "asmdefFungusCount": len(csharp["asmdefsWithFungus"]),
        "remainingFungusGuidFileCount": len(guid_hits),
        "unclassifiedCommands": unclassified_commands,
        "unclassifiedBlocks": sum(1 for b in all_blocks if not b.get("disposition")),
        "classifiedBlocks": sum(1 for b in all_blocks if b.get("disposition")),
        "classifiedCommands": sum(1 for c in all_commands if c.get("disposition")),
        "commandTypes": command_types.most_common(),
        "inputModeCounts": dict(input_mode_counts),
        "edgeCount": len(edges),
        "fungusVendorGuidCount": len(fungus_guids),
    }

    ledger = {
        "summary": summary,
        "buildSettings": build,
        "scenes": [
            {k: v for k, v in s.items() if k not in {"commands"}} | {
                "commandCount": len(s["commands"]),
                "blockNames": [b["blockName"] for b in s["blocks"]],
                "variableKeys": sorted({v["key"] for v in s["variables"]}),
            }
            for s in scenes
        ],
        "sharedPrefabs": shared,
        "blocks": all_blocks,
        "commands": all_commands,
        "variables": all_vars,
        "edges": edges,
        "csharp": csharp,
        "remainingFungusGuidRefs": guid_hits,
    }

    json_path = OUT_DIR / "migration-inventory.json"
    json_path.write_text(json.dumps(ledger, ensure_ascii=False, indent=2), encoding="utf-8")

    csv_path = OUT_DIR / "migration-inventory.csv"
    with csv_path.open("w", encoding="utf-8", newline="") as fh:
        writer = csv.DictWriter(
            fh,
            fieldnames=[
                "assetPath",
                "kind",
                "buildEnabled",
                "flowcharts",
                "blockCount",
                "commandCount",
                "variableKeys",
                "loadTargets",
                "inputModes",
                "disposition",
                "state",
                "fungusComponentCount",
            ],
        )
        writer.writeheader()
        for s in scenes:
            writer.writerow(
                {
                    "assetPath": s["assetPath"],
                    "kind": s["kind"],
                    "buildEnabled": s.get("buildEnabled"),
                    "flowcharts": s["flowcharts"],
                    "blockCount": len(s["blocks"]),
                    "commandCount": len(s["commands"]),
                    "variableKeys": "|".join(sorted({v["key"] for v in s["variables"]})),
                    "loadTargets": "|".join(s["loadTargets"]),
                    "inputModes": "|".join(s["inputModes"]),
                    "disposition": s["disposition"],
                    "state": s["state"],
                    "fungusComponentCount": s["fungusComponentCount"],
                }
            )
        for s in shared:
            writer.writerow(
                {
                    "assetPath": s["assetPath"],
                    "kind": s["kind"],
                    "buildEnabled": "",
                    "flowcharts": s["flowcharts"],
                    "blockCount": len(s["blocks"]),
                    "commandCount": len(s["commands"]),
                    "variableKeys": "|".join(sorted({v["key"] for v in s["variables"]})),
                    "loadTargets": "|".join(s["loadTargets"]),
                    "inputModes": "|".join(s["inputModes"]),
                    "disposition": s["disposition"],
                    "state": s["state"],
                    "fungusComponentCount": s["fungusComponentCount"],
                }
            )

    print(json.dumps(summary, ensure_ascii=False, indent=2))
    print(f"wrote {json_path}")
    print(f"wrote {csv_path}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
