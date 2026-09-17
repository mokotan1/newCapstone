"""Transport failure guard for the live gateway.

Play Mode에서 unity-cli HTTP listener가 죽으면 qa_dev_exec 한 건이 90초씩 걸리고,
runner가 다음 hop을 계속 보내면 실패가 N배로 늘어난다. 이 모듈은 첫 장애에서
게이트웨이를 잠그고(tripped) 이후 호출을 CLI 없이 즉시 거절한다.
"""

from __future__ import annotations

from collections.abc import Callable, Mapping
from typing import Any

TRANSPORT_DOWN_CODE = "TransportDown"
CLI_TIMEOUT_RETURNCODE = 124

_REASON_HEALTH_DOWN = "health-down"
_REASON_CLI_TIMEOUT = "cli-timeout"
_REASON_CLI_NO_JSON = "cli-no-json"
_REASON_ALREADY_TRIPPED = "already-tripped"


def is_transport_failure(payload: Mapping[str, Any]) -> bool:
    """응답이 게임플레이 실패가 아니라 전송 계층 실패인지 판별한다.

    전송 실패 = TransportDown 코드, 또는 CLI가 JSON을 돌려주지 못해
    ``_coerce_payload``가 ``returncode``를 남긴 EnvironmentBlocked.
    DeveloperQa가 정상 응답한 AssertionFailed/EnvironmentBlocked(data 있음)는 전송 실패가 아니다.
    """
    code = str(payload.get("code") or "")
    if code == TRANSPORT_DOWN_CODE:
        return True
    return (
        code == "EnvironmentBlocked"
        and "returncode" in payload
        and payload.get("returncode") != 0
    )


def _classify_reason(payload: Mapping[str, Any]) -> str:
    """전송 실패 응답을 timeout/no-json으로 나눈다. trip 사유 기록용이다."""
    if payload.get("returncode") == CLI_TIMEOUT_RETURNCODE:
        return _REASON_CLI_TIMEOUT
    return _REASON_CLI_NO_JSON


class TransportGuard:
    """라이브 게이트웨이를 감싸 첫 전송 실패 이후의 모든 호출을 거절한다.

    입력: ``inner`` (VerticalGateway 계약), ``health_probe`` (True면 listener 살아있음).
    출력: 정상이면 inner 응답 그대로. 장애면 ``{"ok": False, "code": "TransportDown", ...}``.
    부수 효과: ``tripped``/``trip_reason``/``trip_target``이 첫 장애로 고정된다.
    """

    def __init__(
        self,
        inner: Any,
        *,
        health_probe: Callable[[], bool],
    ) -> None:
        """inner의 kind를 그대로 노출한다(stub-forbidden 판정이 뚫리지 않게)."""
        self._inner = inner
        self._health_probe = health_probe
        self.kind = str(getattr(inner, "kind", "stub"))
        self.tripped = False
        self.trip_reason: str | None = None
        self.trip_target: str | None = None

    def _trip(self, reason: str, target: str) -> None:
        """첫 장애만 기록한다. 이미 tripped면 사유를 덮어쓰지 않는다."""
        if self.tripped:
            return
        self.tripped = True
        self.trip_reason = reason
        self.trip_target = target

    def _down(self, *, refused: bool, target: str, reason: str) -> dict[str, Any]:
        """TransportDown 응답을 만든다. refused=True는 CLI를 띄우지 않고 거절한 호출이다."""
        return {
            "ok": False,
            "code": TRANSPORT_DOWN_CODE,
            "message": f"transport down ({reason}); no further Unity commands are sent",
            "refused": refused,
            "reason": reason,
            "target": target,
            "tripTarget": self.trip_target,
        }

    def invoke(self, family: str, name: str, target: str) -> dict[str, Any]:
        """tripped면 거절, health가 죽었으면 CLI 없이 trip, 아니면 inner 호출 후 응답을 검사한다."""
        if self.tripped:
            return self._down(refused=True, target=target, reason=_REASON_ALREADY_TRIPPED)
        if not self._health_probe():
            self._trip(_REASON_HEALTH_DOWN, target)
            return self._down(refused=True, target=target, reason=_REASON_HEALTH_DOWN)
        payload = self._inner.invoke(family, name, target)
        if is_transport_failure(payload):
            reason = _classify_reason(payload)
            self._trip(reason, target)
            down = self._down(refused=False, target=target, reason=reason)
            down["inner"] = dict(payload)
            return down
        return payload
