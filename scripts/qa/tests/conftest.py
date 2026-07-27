"""Pytest path bootstrap for scripts/qa autorun package."""

from __future__ import annotations

import sys
import uuid
from pathlib import Path

import pytest

_QA_ROOT = Path(__file__).resolve().parents[1]
if str(_QA_ROOT) not in sys.path:
    sys.path.insert(0, str(_QA_ROOT))

# Windows agents sometimes cannot create %TEMP%/pytest-of-*; keep temps in-repo.
_BASETEMP = Path(__file__).resolve().parents[3] / ".tmp_pytest" / "qa-autorun"


@pytest.fixture
def tmp_path() -> Path:
    _BASETEMP.mkdir(parents=True, exist_ok=True)
    path = _BASETEMP / f"case-{uuid.uuid4().hex}"
    path.mkdir(parents=True, exist_ok=False)
    return path
