from __future__ import annotations

from pathlib import Path

import pytest
from local_install import (
    InstallAction,
    LocalAiManifest,
    build_import_command,
    build_serve_command,
    default_model_path,
    load_manifest,
    plan_install,
)

_MANIFEST = LocalAiManifest(
    runtime_package="litert-lm",
    runtime_version="0.16.1",
    model_id="gemma4-e2b",
    huggingface_repo="litert-community/gemma-4-E2B-it-litert-lm",
    artifact_filename="gemma-4-E2B-it.litertlm",
    artifact_sha256="abc123",
    approx_download_bytes=2_588_147_712,
    min_free_disk_bytes=15 * 1024 * 1024 * 1024,
    min_ram_bytes=8 * 1024 * 1024 * 1024,
    loopback_host="127.0.0.1",
    litert_port=9379,
    fastapi_port=8000,
)


def test_plan_install_refuses_without_consent_when_download_needed() -> None:
    plan = plan_install(
        _MANIFEST,
        consent=False,
        offline=False,
        remove_model=False,
        platform="win32",
        free_disk_bytes=_MANIFEST.min_free_disk_bytes,
        total_ram_bytes=_MANIFEST.min_ram_bytes,
        model_path=None,
        model_sha256=None,
        runtime_version=None,
    )

    assert plan.action == InstallAction.REFUSE
    assert plan.needs_download is True
    assert "2.4" in plan.message or "2588" in plan.message or "GB" in plan.message


def test_plan_install_skips_when_model_checksum_matches() -> None:
    plan = plan_install(
        _MANIFEST,
        consent=False,
        offline=True,
        remove_model=False,
        platform="win32",
        free_disk_bytes=0,
        total_ram_bytes=_MANIFEST.min_ram_bytes,
        model_path=Path("C:/models/gemma4-e2b/model.litertlm"),
        model_sha256="abc123",
        runtime_version="0.16.1",
    )

    assert plan.action == InstallAction.SKIP
    assert plan.needs_download is False
    assert plan.remove_model_files is False


def test_plan_install_refuses_insufficient_disk() -> None:
    plan = plan_install(
        _MANIFEST,
        consent=True,
        offline=False,
        remove_model=False,
        platform="win32",
        free_disk_bytes=1024,
        total_ram_bytes=_MANIFEST.min_ram_bytes,
        model_path=None,
        model_sha256=None,
        runtime_version=None,
    )

    assert plan.action == InstallAction.REFUSE
    assert "disk" in plan.message.lower() or "디스크" in plan.message


def test_plan_install_refuses_offline_without_model() -> None:
    plan = plan_install(
        _MANIFEST,
        consent=True,
        offline=True,
        remove_model=False,
        platform="win32",
        free_disk_bytes=_MANIFEST.min_free_disk_bytes,
        total_ram_bytes=_MANIFEST.min_ram_bytes,
        model_path=None,
        model_sha256=None,
        runtime_version=None,
    )

    assert plan.action == InstallAction.REFUSE
    assert "offline" in plan.message.lower() or "오프라인" in plan.message


def test_plan_install_refuses_interrupted_checksum_mismatch() -> None:
    plan = plan_install(
        _MANIFEST,
        consent=True,
        offline=False,
        remove_model=False,
        platform="win32",
        free_disk_bytes=_MANIFEST.min_free_disk_bytes,
        total_ram_bytes=_MANIFEST.min_ram_bytes,
        model_path=Path("C:/models/gemma4-e2b/model.litertlm"),
        model_sha256="deadbeef",
        runtime_version="0.16.1",
    )

    assert plan.action == InstallAction.REFUSE
    assert "retry" in plan.message.lower() or "재시도" in plan.message or "checksum" in plan.message.lower()


def test_plan_install_removes_model_only_when_explicit() -> None:
    plan = plan_install(
        _MANIFEST,
        consent=False,
        offline=True,
        remove_model=True,
        platform="win32",
        free_disk_bytes=0,
        total_ram_bytes=0,
        model_path=Path("C:/models/gemma4-e2b/model.litertlm"),
        model_sha256="abc123",
        runtime_version="0.16.1",
    )

    assert plan.action == InstallAction.REMOVE
    assert plan.remove_model_files is True


def test_plan_install_refuses_non_windows() -> None:
    plan = plan_install(
        _MANIFEST,
        consent=True,
        offline=False,
        remove_model=False,
        platform="linux",
        free_disk_bytes=_MANIFEST.min_free_disk_bytes,
        total_ram_bytes=_MANIFEST.min_ram_bytes,
        model_path=None,
        model_sha256=None,
        runtime_version=None,
    )

    assert plan.action == InstallAction.REFUSE


def test_plan_install_accepts_consent_and_builds_pinned_import() -> None:
    plan = plan_install(
        _MANIFEST,
        consent=True,
        offline=False,
        remove_model=False,
        platform="win32",
        free_disk_bytes=_MANIFEST.min_free_disk_bytes,
        total_ram_bytes=_MANIFEST.min_ram_bytes,
        model_path=None,
        model_sha256=None,
        runtime_version=None,
    )

    assert plan.action == InstallAction.INSTALL
    command = build_import_command(_MANIFEST)
    assert command[:3] == ["uvx", "--from", "litert-lm==0.16.1"]
    assert "--from-huggingface-repo=litert-community/gemma-4-E2B-it-litert-lm" in command
    assert "gemma4-e2b" in command
    serve = build_serve_command(_MANIFEST)
    assert "--host" in serve and "127.0.0.1" in serve
    assert "--port" in serve and "9379" in serve


def test_default_model_path_uses_litert_home(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv("LITERT_LM_HOME", str(tmp_path))
    path = default_model_path(_MANIFEST)
    assert path == tmp_path / "models" / "gemma4-e2b" / "model.litertlm"


def test_plan_install_refuses_low_ram_when_download_needed() -> None:
    plan = plan_install(
        _MANIFEST,
        consent=True,
        offline=False,
        remove_model=False,
        platform="win32",
        free_disk_bytes=_MANIFEST.min_free_disk_bytes,
        total_ram_bytes=1024,
        model_path=None,
        model_sha256=None,
        runtime_version=None,
    )

    assert plan.action == InstallAction.REFUSE
    assert "RAM" in plan.message


def test_manifest_pins_runtime_checksum_and_loopback() -> None:
    manifest = load_manifest()
    assert manifest.runtime_package == "litert-lm"
    assert manifest.runtime_version == "0.16.1"
    assert manifest.model_id == "gemma4-e2b"
    assert manifest.artifact_sha256 == "181938105E0EEFD105961417E8DA75903EACDA102C4FCE9CE90F50B97139A63C"
    assert manifest.loopback_host == "127.0.0.1"
    assert manifest.litert_port == 9379
    assert manifest.fastapi_port == 8000
