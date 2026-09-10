"""Classify Unity test stdout or NUnit XML into harness result JSON."""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

from scripts.unity_harness.result_contract import (
    classify_test_counts,
    parse_nunit_xml,
    parse_test_stdout,
)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--xml", type=Path)
    parser.add_argument("--native-exit", type=int, default=0)
    args = parser.parse_args()
    stdout = sys.stdin.read()
    if args.xml is not None and args.xml.is_file():
        counts = parse_nunit_xml(args.xml.read_text(encoding="utf-8"))
    else:
        counts = parse_test_stdout(stdout)
    result = classify_test_counts(**counts)
    result["nativeExitCode"] = args.native_exit
    json.dump(result, sys.stdout)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
