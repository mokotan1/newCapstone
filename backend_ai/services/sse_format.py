from __future__ import annotations

from models.responses import SSEEvent


def format_sse_event(event: SSEEvent) -> str:
    """Serialize one SSEEvent as a Unity-compatible SSE frame.

    Unity's download handler can split TCP reads mid-line, so the client
    reassembles on ``\\n``. Frames must therefore be complete
    ``data: {json}\\n\\n`` records, never a bare JSON object.
    """
    return f"data: {event.model_dump_json()}\n\n"
