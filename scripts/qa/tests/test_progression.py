"""Progression graph tests (design §4)."""

from __future__ import annotations

from scripts.qa.rooms.progression import has_path, load_progression_edges, neighbors


def test_hall_reaches_kitchen_via_first_floor_halls() -> None:
    edges = load_progression_edges()
    assert "hall.left" in neighbors(edges, "hall") or "hall.right" in neighbors(edges, "hall")
    assert has_path(edges, "hall", "kitchen")


def test_kitchen_is_reachable_from_hall_left() -> None:
    edges = load_progression_edges()
    assert "kitchen" in neighbors(edges, "hall.left")


def test_basement_research_reachable_from_basement_entry() -> None:
    edges = load_progression_edges()
    assert has_path(edges, "basement.entry", "basement.research")


def test_second_floor_rooms_hang_off_second_floor_hall() -> None:
    edges = load_progression_edges()
    hall2 = neighbors(edges, "second-floor.hall")
    assert "tutor-room" in hall2
    assert "child-room" in hall2
    assert "wife-room" in hall2
    assert "bed-room" in hall2
