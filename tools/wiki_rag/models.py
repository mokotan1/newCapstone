"""Typed records shared by the project knowledge pipeline."""

from __future__ import annotations

from dataclasses import asdict, dataclass
from typing import Any


@dataclass(frozen=True)
class SourceRecord:
    """Immutable manifest record for one original knowledge artifact."""

    source_id: str
    source_path: str
    source_sha256: str
    source_type: str
    category: str
    title: str
    transcript_path: str
    status: str
    rag_eligible: bool
    canonical_group: str

    def to_dict(self) -> dict[str, Any]:
        """Return a serialization-ready mapping in dataclass field order."""

        return asdict(self)
