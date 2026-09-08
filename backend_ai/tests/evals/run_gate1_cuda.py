"""Run the pinned CUDA candidate on its own port, then stop only our process."""
from __future__ import annotations

import argparse
import asyncio
import json
import statistics
import time
from dataclasses import asdict
from pathlib import Path

from config import Settings
from models.requests import ChatRequest
from services.chat_service import ChatService
from services.local_ai.gate1 import evaluate_gate1
from services.local_ai.hardware import probe_nvidia
from services.local_ai.process_host import ProcessEngineHost
from services.local_ai.telemetry import read_gpu_usage
from tests.evals.cheshire_eval import evaluate_replies, load_dialogue_cases
from tests.evals.run_cheshire_eval import _DEFAULT_SYSTEM
from tools.registry import ToolRegistry


async def run(root: Path, output: Path, port: int) -> None:
    settings = Settings(ai_provider="local", groq_api_key="", google_api_key="",
                        local_ai_cuda_dir=str(root), local_ai_base_url=f"http://127.0.0.1:{port}")
    host = ProcessEngineHost(settings)
    engine = host.start("cuda", port)
    report: dict[str, object] = {"runtime": "llama.cpp-b10852-cuda-12.4", "port": port,
                                "gate_passed": False, "unity_frame_gate": "not_measured"}
    try:
        warmup = await asyncio.wait_for(host.warmup(engine), timeout=120)
        report["warmup"] = asdict(warmup)
        print(json.dumps(report, ensure_ascii=False), flush=True)
        if not warmup.ok or warmup.effective_backend != "gpu":
            return
        service = ChatService(primary=engine.provider, fallback=None, registry=ToolRegistry(),
                              temperature=settings.default_temperature, max_tokens=settings.max_tokens,
                              app_settings=settings)
        cases = load_dialogue_cases(Path(__file__).with_name("cheshire_dialogue_cases.jsonl"))
        replies: list[str] = []
        measurements: list[dict] = []
        for case in cases:
            request = ChatRequest(prompt=case.prompt, system=case.system or _DEFAULT_SYSTEM,
                                  use_tools=False, locale=case.locale,
                                  character_facts=case.character_facts or None,
                                  dialogue_context=case.dialogue_context or None)
            started = time.perf_counter()
            first = None
            reply = ""
            error = False
            async for event in service.stream_chat(request):
                if event.type == "text_delta" and event.content and first is None:
                    first = (time.perf_counter() - started) * 1000
                if event.type == "done":
                    reply = event.full_text or ""
                if event.type in {"error", "function_call"}:
                    error = True
            total = (time.perf_counter() - started) * 1000
            replies.append(reply)
            measurements.append({"id": case.id, "ttft_ms": first, "total_ms": total,
                                 "reply": reply, "error": error,
                                 "gpu": await asyncio.to_thread(read_gpu_usage)})
            print(f"{case.id}: {total:.0f}ms", flush=True)
        quality = evaluate_replies(cases, replies)
        report["quality"] = {"case_count": quality.case_count, "valid_count": quality.valid_count,
                             "passes_release_gate": quality.passes_release_gate,
                             "tool_leak_count": quality.tool_leak_count,
                             "invented_fact_count": quality.invented_fact_count,
                             "used_fallback_count": quality.used_fallback_count}
        report["total_ms_p50"] = statistics.median(m["total_ms"] for m in measurements)
        ttfts = [m["ttft_ms"] for m in measurements if m["ttft_ms"] is not None]
        report["ttft_ms_p50"] = statistics.median(ttfts) if ttfts else None
        report["samples"] = measurements
        verdict = evaluate_gate1(
            probe_nvidia(),
            cuda_offload=warmup.effective_backend == "gpu",
            quality_ok=quality.passes_release_gate,
            had_error=any(m["error"] for m in measurements),
            complete_ms_p50=float(report["total_ms_p50"]),
        )
        report["gate1"] = asdict(verdict)
        report["backend_gate_passed"] = verdict.passed
        report["gate_passed"] = verdict.passed
    finally:
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
        host.stop(engine)
        await engine.provider.aclose()


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--runtime-dir", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--port", type=int, default=19380)
    args = parser.parse_args()
    asyncio.run(run(args.runtime_dir, args.output, args.port))
