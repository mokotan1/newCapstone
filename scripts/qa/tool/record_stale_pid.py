"""Record AC22 stale-pid evidence against the previous hung Editor PID."""

from __future__ import annotations

import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from scripts.qa.tool.live_vertical import run_stale_pid_probe, utc_run_root


def main() -> None:
    """죽은 Editor PID 36340 vs 현재 PID를 stale-pid BLOCKED 보고서로 남긴다."""
    root = utc_run_root("run-hall-to-kitchen-stale-pid")
    result = run_stale_pid_probe(root, current_pid="16840", previous_pid="36340")
    summary = root / "ac22-stale-pid.txt"
    summary.write_text(
        f"reasonCode={result['reasonCode']}\n"
        f"runVerdict={result['runVerdict']}\n"
        f"featureVerified={result['featureVerified']}\n"
        "previousPid=36340\n"
        "currentPid=16840\n",
        encoding="utf-8",
    )
    print("wrote", str(root))
    print(result["reasonCode"], result["runVerdict"])


if __name__ == "__main__":
    main()
