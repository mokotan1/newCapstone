from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any

from providers.base import AIProvider
from services.local_ai.engine import HardwareInfo, OwnedEngine, WarmupResult
from services.local_ai.types import EffectiveBackend, EngineKind

__all__ = ["FakeEngineHost", "FakeProvider", "HardwareInfo"]


@dataclass
class EngineHostCalls:
    starts: list[EngineKind] = field(default_factory=list)
    stops: list[int] = field(default_factory=list)


class FakeProvider(AIProvider):
    def __init__(self, provider_name: str = "fake") -> None:
        self._name = provider_name

    @property
    def name(self) -> str:
        return self._name

    async def stream_chat(
        self,
        messages: list[dict],
        tools: list[dict] | None = None,
        temperature: float = 0.7,
        max_tokens: int = 512,
    ) -> Any:
        if False:  # pragma: no cover
            yield None
        return
        yield  # pragma: no cover


class FakeEngineHost:
    """In-memory engine host for manager tests. Does not spawn processes."""

    def __init__(self) -> None:
        self.port_open = False
        self.next_pid = 1000
        self.calls = EngineHostCalls()
        self.warmup_ok = True
        self.warmup_backend: EffectiveBackend = "cpu"
        self.start_should_fail = False
        self._warmup_override = None

    def is_port_open(self, port: int) -> bool:
        return self.port_open

    def make_provider(self, kind: EngineKind, port: int) -> FakeProvider:
        return FakeProvider(f"external-{kind}:{port}")

    def start(self, kind: EngineKind, port: int) -> OwnedEngine:
        self.calls.starts.append(kind)
        if self.start_should_fail:
            raise RuntimeError("engine_start_failed")
        self.next_pid += 1
        self.port_open = True
        return OwnedEngine(
            kind=kind,
            pid=self.next_pid,
            port=port,
            provider=FakeProvider(kind),
            started_by_manager=True,
        )

    def stop(self, engine: OwnedEngine) -> None:
        if not engine.started_by_manager:
            raise AssertionError("must not stop an engine the manager does not own")
        self.calls.stops.append(engine.pid)
        self.port_open = False

    async def warmup(self, engine: OwnedEngine) -> WarmupResult:
        if self._warmup_override is not None:
            return await self._warmup_override(engine)
        if not self.warmup_ok:
            return WarmupResult(ok=False, effective_backend="unknown", error="warmup_failed")
        return WarmupResult(ok=True, effective_backend=self.warmup_backend)
