"""Product defect packet (design AC20). QA must not patch product code to pass."""

from __future__ import annotations

from collections.abc import Mapping
from typing import Any


def build_defect_packet(payload: Mapping[str, Any]) -> dict[str, Any]:
    """제품 결함 핸드오프 패킷을 만든다. 제품 트리가 바뀌었으면 accepted=False다."""
    # TODO(AC20): 라이브 결함 수집은 이 함수가 아니라 수직 실행 쪽에서 패킷을 채운다.
    packet = {
        "requirementId": payload.get("requirementId"),
        "environment": dict(payload.get("environment") or {}),
        "reproduction": list(payload.get("reproduction") or []),
        "expected": payload.get("expected"),
        "actual": payload.get("actual"),
        "evidenceIds": list(payload.get("evidenceIds") or []),
        "accepted": True,
        "reasonCodes": [],
    }
    if payload.get("productTreeBefore") != payload.get("productTreeAfter"):
        packet["accepted"] = False
        packet["reasonCodes"] = ["product-modified"]
    return packet
