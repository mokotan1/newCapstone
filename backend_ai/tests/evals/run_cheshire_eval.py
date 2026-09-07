"""Run Cheshire dialogue evals against the local Gemma runtime only.

Refuses cloud providers (Groq/Gemini). From backend_ai/:

    python -m tests.evals.run_cheshire_eval
"""

from __future__ import annotations

import argparse
import asyncio
import json
import platform
import time
from pathlib import Path

from config import Settings, get_settings
from local_runtime import build_chat_providers, check_local_runtime
from models.requests import ChatRequest
from services.chat_service import ChatService
from tests.evals.cheshire_eval import (
    VALID_RATE_GATE,
    evaluate_replies,
    load_dialogue_cases,
)
from tools.game_tools import GAME_TOOLS
from tools.registry import ToolRegistry

_CASES = Path(__file__).with_name("cheshire_dialogue_cases.jsonl")
_DEFAULT_SYSTEM = (
    "당신은 저택의 앵무새 체셔입니다. 문장 최대 2개. "
    "JSON이나 게임 툴을 출력하지 마세요. 주입된 사실에 없는 퍼즐 정답을 말하지 마세요."
)


def _parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--cases",
        type=Path,
        default=_CASES,
        help="JSONL case file",
    )
    parser.add_argument(
        "--limit",
        type=int,
        default=0,
        help="Score only the first N cases (0 = all)",
    )
    return parser.parse_args()


async def _run(settings: Settings, cases_path: Path, limit: int) -> int:
    if settings.ai_provider != "local":
        print("Refusing: set AI_PROVIDER=local. This eval does not call Groq or Gemini.")
        return 2

    status = check_local_runtime(settings)
    if not status.model_available:
        print(f"Refusing: local runtime not ready ({status.error}).")
        return 2

    primary, _unused_cloud_fallback = build_chat_providers(
        settings.model_copy(update={"groq_api_key": "", "google_api_key": ""})
    )
    if primary is None:
        print("Refusing: local LiteRT provider was not constructed.")
        return 2

    registry = ToolRegistry()
    registry.register_many(GAME_TOOLS)
    service = ChatService(
        primary=primary,
        fallback=None,
        registry=registry,
        temperature=settings.default_temperature,
        max_tokens=settings.max_tokens,
        app_settings=settings,
    )

    cases = load_dialogue_cases(cases_path)
    if limit > 0:
        cases = cases[:limit]

    replies: list[str] = []
    latencies_ms: list[float] = []
    ttft_ms: list[float] = []
    for case in cases:
        request = ChatRequest(
            prompt=case.prompt,
            system=case.system or _DEFAULT_SYSTEM,
            use_tools=False,
            locale=case.locale,
            character_facts=case.character_facts or None,
            dialogue_context=case.dialogue_context or None,
        )
        started = time.perf_counter()
        reply = ""
        first_delta_ms: float | None = None
        saw_tool = False
        async for event in service.stream_chat(request):
            if event.type == "text_delta" and event.content and first_delta_ms is None:
                first_delta_ms = (time.perf_counter() - started) * 1000
            if event.type == "function_call":
                saw_tool = True
            if event.type == "done" and event.full_text:
                reply = event.full_text
            if event.type == "error" and event.content:
                reply = event.content
        latencies_ms.append((time.perf_counter() - started) * 1000)
        if first_delta_ms is not None:
            ttft_ms.append(first_delta_ms)
        replies.append(reply)
        if saw_tool:
            print(f"{case.id}: unexpected function_call in stream")

    report = evaluate_replies(cases, replies)
    latencies_ms.sort()
    ttft_ms.sort()
    p50 = latencies_ms[len(latencies_ms) // 2] if latencies_ms else 0.0
    p95_index = max(0, int(len(latencies_ms) * 0.95) - 1)
    p95 = latencies_ms[p95_index] if latencies_ms else 0.0
    ttft_p50 = ttft_ms[len(ttft_ms) // 2] if ttft_ms else 0.0
    ttft_p95_index = max(0, int(len(ttft_ms) * 0.95) - 1)
    ttft_p95 = ttft_ms[ttft_p95_index] if ttft_ms else 0.0

    payload = {
        "model": settings.local_ai_model,
        "runtime": "litert-lm",
        "hardware": {
            "machine": platform.machine(),
            "processor": platform.processor(),
            "system": platform.system(),
        },
        "case_count": report.case_count,
        "valid_count": report.valid_count,
        "valid_rate": report.valid_rate,
        "valid_rate_gate": VALID_RATE_GATE,
        "tool_leak_count": report.tool_leak_count,
        "invented_fact_count": report.invented_fact_count,
        "prompt_leak_count": report.prompt_leak_count,
        "used_fallback_count": report.used_fallback_count,
        "passes_release_gate": report.passes_release_gate,
        "latency_ms": {"p50": round(p50, 1), "p95": round(p95, 1)},
        "ttft_ms": {"p50": round(ttft_p50, 1), "p95": round(ttft_p95, 1)},
        "failures": [
            {
                "id": case.id,
                "category": case.category,
                "failures": score.failures,
                "reply": reply,
            }
            for case, score, reply in zip(cases, report.scores, replies)
            if not score.valid
        ],
    }
    print(json.dumps(payload, ensure_ascii=False, indent=2))
    return 0 if report.passes_release_gate else 1


def main() -> None:
    args = _parse_args()
    raise SystemExit(asyncio.run(_run(get_settings(), args.cases, args.limit)))


if __name__ == "__main__":
    main()
