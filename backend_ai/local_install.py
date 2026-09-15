from __future__ import annotations

import argparse
import hashlib
import json
import os
import shutil
import subprocess
import sys
from dataclasses import dataclass
from enum import Enum
from pathlib import Path
from typing import Any

_MANIFEST_PATH = Path(__file__).resolve().parent / "data" / "local_ai_manifest.json"


class InstallAction(str, Enum):
    INSTALL = "install"
    SKIP = "skip"
    REFUSE = "refuse"
    REMOVE = "remove"


@dataclass(frozen=True)
class LocalAiManifest:
    runtime_package: str
    runtime_version: str
    model_id: str
    huggingface_repo: str
    artifact_filename: str
    artifact_sha256: str
    approx_download_bytes: int
    min_free_disk_bytes: int
    min_ram_bytes: int
    loopback_host: str
    litert_port: int
    fastapi_port: int


@dataclass(frozen=True)
class InstallPlan:
    action: InstallAction
    message: str
    needs_download: bool
    remove_model_files: bool


def load_manifest(path: Path | None = None) -> LocalAiManifest:
    target = path or _MANIFEST_PATH
    payload = json.loads(target.read_text(encoding="utf-8"))
    if not isinstance(payload, dict):
        raise TypeError("local AI manifest must be a JSON object")
    return LocalAiManifest(
        runtime_package=str(payload["runtime_package"]),
        runtime_version=str(payload["runtime_version"]),
        model_id=str(payload["model_id"]),
        huggingface_repo=str(payload["huggingface_repo"]),
        artifact_filename=str(payload["artifact_filename"]),
        artifact_sha256=str(payload["artifact_sha256"]).upper(),
        approx_download_bytes=int(payload["approx_download_bytes"]),
        min_free_disk_bytes=int(payload["min_free_disk_bytes"]),
        min_ram_bytes=int(payload["min_ram_bytes"]),
        loopback_host=str(payload["loopback_host"]),
        litert_port=int(payload["litert_port"]),
        fastapi_port=int(payload["fastapi_port"]),
    )


def default_litert_home() -> Path:
    override = os.environ.get("LITERT_LM_HOME", "").strip()
    if override:
        return Path(override)
    return Path.home() / ".litert-lm"


def default_model_path(manifest: LocalAiManifest) -> Path:
    return default_litert_home() / "models" / manifest.model_id / "model.litertlm"


def format_gib(num_bytes: int) -> str:
    return f"{num_bytes / (1024 ** 3):.1f} GB"


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        while True:
            chunk = handle.read(1024 * 1024)
            if not chunk:
                break
            digest.update(chunk)
    return digest.hexdigest().upper()


def build_import_command(manifest: LocalAiManifest) -> list[str]:
    return [
        "uvx",
        "--from",
        f"{manifest.runtime_package}=={manifest.runtime_version}",
        "litert-lm",
        "import",
        f"--from-huggingface-repo={manifest.huggingface_repo}",
        manifest.artifact_filename,
        manifest.model_id,
    ]


def build_serve_command(manifest: LocalAiManifest) -> list[str]:
    return [
        "uvx",
        "--from",
        f"{manifest.runtime_package}=={manifest.runtime_version}",
        "litert-lm",
        "serve",
        "--host",
        manifest.loopback_host,
        "--port",
        str(manifest.litert_port),
    ]


def build_fastapi_command(manifest: LocalAiManifest) -> list[str]:
    return [
        sys.executable,
        "-m",
        "uvicorn",
        "main:app",
        "--host",
        manifest.loopback_host,
        "--port",
        str(manifest.fastapi_port),
    ]


def plan_install(
    manifest: LocalAiManifest,
    *,
    consent: bool,
    offline: bool,
    remove_model: bool,
    platform: str,
    free_disk_bytes: int,
    total_ram_bytes: int,
    model_path: Path | None,
    model_sha256: str | None,
    runtime_version: str | None,
) -> InstallPlan:
    if remove_model:
        return InstallPlan(
            action=InstallAction.REMOVE,
            message="Model files will be deleted only because --remove-model was passed.",
            needs_download=False,
            remove_model_files=True,
        )

    if not _is_windows(platform):
        return InstallPlan(
            action=InstallAction.REFUSE,
            message="Desktop local AI install is Windows-only in this release.",
            needs_download=False,
            remove_model_files=False,
        )

    installed = _checksum_matches(manifest, model_path, model_sha256)
    if installed:
        return InstallPlan(
            action=InstallAction.SKIP,
            message="Pinned Gemma 4 E2B artifact is already installed.",
            needs_download=False,
            remove_model_files=False,
        )

    interrupted = model_path is not None and model_sha256 not in (None, "")
    if interrupted:
        return InstallPlan(
            action=InstallAction.REFUSE,
            message="Installed model checksum does not match the pin. Delete the partial file and retry.",
            needs_download=True,
            remove_model_files=False,
        )

    if total_ram_bytes < manifest.min_ram_bytes:
        return InstallPlan(
            action=InstallAction.REFUSE,
            message=(
                f"Need at least {format_gib(manifest.min_ram_bytes)} RAM "
                f"(detected {format_gib(total_ram_bytes)})."
            ),
            needs_download=True,
            remove_model_files=False,
        )

    if free_disk_bytes < manifest.min_free_disk_bytes:
        return InstallPlan(
            action=InstallAction.REFUSE,
            message=(
                f"Need at least {format_gib(manifest.min_free_disk_bytes)} free disk "
                f"before downloading ~{format_gib(manifest.approx_download_bytes)}."
            ),
            needs_download=True,
            remove_model_files=False,
        )

    if offline:
        return InstallPlan(
            action=InstallAction.REFUSE,
            message="Offline launch cannot download the model. Connect once or copy the installed artifact.",
            needs_download=True,
            remove_model_files=False,
        )

    if not consent:
        return InstallPlan(
            action=InstallAction.REFUSE,
            message=(
                "Consent is required before downloading Gemma 4 E2B "
                f"(~{format_gib(manifest.approx_download_bytes)}). "
                "See installer/licenses/NOTICE.md."
            ),
            needs_download=True,
            remove_model_files=False,
        )

    runtime_note = ""
    if runtime_version and runtime_version != manifest.runtime_version:
        runtime_note = f" Runtime {runtime_version} will be replaced by {manifest.runtime_version}."

    return InstallPlan(
        action=InstallAction.INSTALL,
        message=f"Import pinned {manifest.model_id} via LiteRT-LM {manifest.runtime_version}.{runtime_note}",
        needs_download=True,
        remove_model_files=False,
    )


def plan_to_dict(plan: InstallPlan) -> dict[str, Any]:
    return {
        "action": plan.action.value,
        "message": plan.message,
        "needs_download": plan.needs_download,
        "remove_model_files": plan.remove_model_files,
    }


def detect_free_disk_bytes(path: Path) -> int:
    usage = shutil.disk_usage(path)
    return int(usage.free)


def detect_total_ram_bytes() -> int:
    if hasattr(os, "sysconf"):
        try:
            pages = int(os.sysconf("SC_PHYS_PAGES"))
            page_size = int(os.sysconf("SC_PAGE_SIZE"))
            return pages * page_size
        except (ValueError, OSError):
            pass
    if sys.platform == "win32":
        return _windows_total_ram_bytes()
    return 0


def execute_plan(
    manifest: LocalAiManifest,
    plan: InstallPlan,
    *,
    model_path: Path,
) -> int:
    if plan.action == InstallAction.REMOVE:
        if model_path.is_file():
            model_path.unlink()
        return 0
    if plan.action == InstallAction.SKIP:
        return 0
    if plan.action != InstallAction.INSTALL:
        return 2
    completed = subprocess.run(build_import_command(manifest), check=False)
    return int(completed.returncode)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Plan or run Windows local Gemma 4 E2B install.")
    parser.add_argument("--consent", action="store_true")
    parser.add_argument("--offline", action="store_true")
    parser.add_argument("--remove-model", action="store_true")
    parser.add_argument("--execute", action="store_true")
    args = parser.parse_args(argv)

    manifest = load_manifest()
    model_path = default_model_path(manifest)
    model_sha: str | None = None
    if model_path.is_file():
        model_sha = sha256_file(model_path)

    plan = plan_install(
        manifest,
        consent=args.consent,
        offline=args.offline,
        remove_model=args.remove_model,
        platform=sys.platform,
        free_disk_bytes=detect_free_disk_bytes(model_path.parent if model_path.parent.exists() else Path.home()),
        total_ram_bytes=detect_total_ram_bytes(),
        model_path=model_path if model_path.is_file() else None,
        model_sha256=model_sha,
        runtime_version=manifest.runtime_version,
    )
    print(json.dumps(plan_to_dict(plan), ensure_ascii=False))
    if args.execute:
        return execute_plan(manifest, plan, model_path=model_path)
    return 0 if plan.action in {InstallAction.INSTALL, InstallAction.SKIP, InstallAction.REMOVE} else 2


def _is_windows(platform: str) -> bool:
    name = platform.lower()
    return name.startswith("win")


def _checksum_matches(
    manifest: LocalAiManifest,
    model_path: Path | None,
    model_sha256: str | None,
) -> bool:
    if model_path is None or not model_sha256:
        return False
    return model_sha256.upper() == manifest.artifact_sha256.upper()


def _windows_total_ram_bytes() -> int:
    try:
        import ctypes

        class _MemoryStatusEx(ctypes.Structure):
            _fields_ = [
                ("dwLength", ctypes.c_ulong),
                ("dwMemoryLoad", ctypes.c_ulong),
                ("ullTotalPhys", ctypes.c_ulonglong),
                ("ullAvailPhys", ctypes.c_ulonglong),
                ("ullTotalPageFile", ctypes.c_ulonglong),
                ("ullAvailPageFile", ctypes.c_ulonglong),
                ("ullTotalVirtual", ctypes.c_ulonglong),
                ("ullAvailVirtual", ctypes.c_ulonglong),
                ("ullAvailExtendedVirtual", ctypes.c_ulonglong),
            ]

        status = _MemoryStatusEx()
        status.dwLength = ctypes.sizeof(_MemoryStatusEx)
        if ctypes.windll.kernel32.GlobalMemoryStatusEx(ctypes.byref(status)) == 0:
            return 0
        return int(status.ullTotalPhys)
    except (AttributeError, OSError, ValueError):
        return 0


if __name__ == "__main__":
    raise SystemExit(main())
