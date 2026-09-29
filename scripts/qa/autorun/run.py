"""Command line entry point for executing the versioned QA scenario catalog."""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

if __package__ in {None, ""}:
    sys.path.insert(0, str(Path(__file__).resolve().parents[3]))

from scripts.qa.tool.live import UnityCliGateway
from scripts.qa.tool.live_vertical import utc_run_root

try:
    from .scenario_runner import run_scenarios
except ImportError:  # direct `python scripts/qa/autorun/run.py` invocation
    from scripts.qa.autorun.scenario_runner import run_scenarios


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Run QA scenario JSON through Unity's QA gateway")
    parser.add_argument(
        "--scenario-root",
        default="disputatio/Assets/Resources/QA/Scenarios",
        help="directory containing schema-version-1 executable scenarios",
    )
    parser.add_argument("--scenario-id", action="append", dest="scenario_ids", help="exact ID; repeatable")
    parser.add_argument("--run-root", help="evidence directory; defaults to docs/qa/runs/<UTC>-scenario-autorun")
    args = parser.parse_args(argv)
    run_root = Path(args.run_root) if args.run_root else utc_run_root("scenario-autorun")
    summary = run_scenarios(
        UnityCliGateway(),
        Path(args.scenario_root),
        run_root,
        args.scenario_ids,
    )
    print(f"run={run_root} verdict={summary['verdict']} scenarios={len(summary['scenario_ids'])}")
    return 0 if summary["verdict"] == "PASS" else 1


if __name__ == "__main__":
    raise SystemExit(main())
