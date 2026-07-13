from __future__ import annotations

from xml.sax.saxutils import escape

from llm_defense.injection_meta import INJECTION_META_KO
from llm_defense.input_sanitize import sanitize_llm_text


def _escape_envelope_body(text: str) -> str:
    """Escape so angle brackets inside untrusted bodies cannot break XML-like markers."""
    return escape(text, entities={"\"": "&quot;", "'": "&apos;"})


def build_llm_messages(
    *,
    client_system_raw: str,
    user_prompt_raw: str,
    external_documents: list[tuple[str, str]],
    server_tool_instruction: str | None,
    max_prompt_chars: int,
    max_client_system_chars: int,
    max_external_doc_chars: int,
    server_response_language_instruction: str | None = None,
) -> list[dict[str, str]]:
    """
    Build chat messages with a single trusted system message and enveloped untrusted content.

    - Trusted system: injection meta (+ optional response-language / tool rules).
    - Client system / persona: <scene_config> in the user envelope (never a second raw system).
    - RAG / bank text: <external_document>.
    """
    trusted_system_parts: list[str] = [INJECTION_META_KO]
    if server_response_language_instruction:
        trusted_system_parts.append(server_response_language_instruction.strip())
    if server_tool_instruction:
        trusted_system_parts.append(server_tool_instruction.strip())
    trusted_system = "\n\n".join(p for p in trusted_system_parts if p)

    client_system_sanitized = sanitize_llm_text(client_system_raw, max_chars=max_client_system_chars)
    user_sanitized = sanitize_llm_text(user_prompt_raw, max_chars=max_prompt_chars)

    user_blocks: list[str] = []

    user_blocks.append(
        "<scene_config trust=\"untrusted\" source=\"client\">\n"
        f"{_escape_envelope_body(client_system_sanitized)}\n"
        "</scene_config>"
    )

    for source_id, body in external_documents:
        sid = sanitize_llm_text(source_id, max_chars=128)
        if not sid:
            sid = "unknown"
        doc = sanitize_llm_text(body, max_chars=max_external_doc_chars)
        if not doc.strip():
            continue
        user_blocks.append(
            f"<external_document trust=\"untrusted\" source=\"{_escape_envelope_body(sid)}\">\n"
            f"{_escape_envelope_body(doc)}\n"
            "</external_document>"
        )

    user_blocks.append(
        f"<user_input>\n{_escape_envelope_body(user_sanitized)}\n</user_input>"
    )

    user_content = "\n\n".join(user_blocks)

    return [
        {"role": "system", "content": trusted_system},
        {"role": "user", "content": user_content},
    ]
