"""Pytest path bootstrap for scripts/qa autorun package."""

from __future__ import annotations

import sys
import uuid
from pathlib import Path

import pytest

_QA_ROOT = Path(__file__).resolve().parents[1]
_REPO_ROOT = Path(__file__).resolve().parents[3]
for _path in (_QA_ROOT, _REPO_ROOT):
    if str(_path) not in sys.path:
        sys.path.insert(0, str(_path))

# Windows agents sometimes cannot create %TEMP%/pytest-of-*; keep temps in-repo.
_BASETEMP = Path(__file__).resolve().parents[3] / ".tmp_pytest" / "qa-autorun"


@pytest.fixture
def tmp_path() -> Path:
    _BASETEMP.mkdir(parents=True, exist_ok=True)
    path = _BASETEMP / f"case-{uuid.uuid4().hex}"
    path.mkdir(parents=True, exist_ok=False)
    return path
