"""Canonical mansion progression graph (design §4)."""

from __future__ import annotations

from collections import deque
from typing import Mapping

# Directed adjacency list: source region -> reachable next regions.
PROGRESSION_EDGES: dict[str, list[str]] = {
    "hall": ["hall.left", "hall.right", "second-floor.hall"],
    "hall.left": ["utility-room", "kitchen"],
    "hall.right": ["maid-room", "study-room", "prison"],
    "study-room": ["study-bookcases"],
    "second-floor.hall": ["tutor-room", "child-room", "wife-room", "bed-room"],
    "bed-room": ["basement.entry"],
    "basement.entry": ["basement.hall"],
    "basement.hall": [
        "basement.extraction",
        "basement.observation",
        "basement.brick",
        "basement.research",
    ],
}


def load_progression_edges() -> dict[str, list[str]]:
    """Return a shallow copy of the progression adjacency list."""
    return {key: list(values) for key, values in PROGRESSION_EDGES.items()}


def neighbors(edges: Mapping[str, list[str]], region_id: str) -> list[str]:
    return list(edges.get(region_id, []))


def has_path(edges: Mapping[str, list[str]], source: str, destination: str) -> bool:
    """BFS reachability over the directed progression graph."""
    if source == destination:
        return True
    seen: set[str] = {source}
    queue: deque[str] = deque([source])
    while queue:
        current = queue.popleft()
        for nxt in edges.get(current, []):
            if nxt == destination:
                return True
            if nxt not in seen:
                seen.add(nxt)
                queue.append(nxt)
    return False
