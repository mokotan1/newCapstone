"""Parse Unity test output and map QA actions (design AC3, AC4)."""

from __future__ import annotations

from scripts.unity_harness.result_contract import (
    classify_test_counts,
    map_qa_action,
    parse_nunit_xml,
    parse_test_stdout,
)


def test_nunit_xml_failed_run_is_test_failed_not_zero_matched() -> None:
    xml = """
    <test-run id="2" testcasecount="3" result="Failed" total="3"
              passed="2" failed="1" skipped="0" inconclusive="0" />
    """
    counts = parse_nunit_xml(xml)
    result = classify_test_counts(**counts)
    assert counts == {
        "matched": 3,
        "executed": 3,
        "passed": 2,
        "failed": 1,
        "skipped": 0,
    }
    assert result["resultKind"] == "test-failed"
    assert result["verificationStatus"] == "failed"


def test_nunit_xml_empty_run_is_zero_matched() -> None:
    xml = """
    <test-run id="2" testcasecount="0" result="Passed" total="0"
              passed="0" failed="0" skipped="0" inconclusive="0" />
    """
    counts = parse_nunit_xml(xml)
    result = classify_test_counts(**counts)
    assert counts["matched"] == 0
    assert result["resultKind"] == "zero-matched"


def test_stdout_passed_failed_skipped_line_is_parsed() -> None:
    stdout = "Some log\nPassed: 4  Failed: 0  Skipped: 1\nDone"
    counts = parse_test_stdout(stdout)
    result = classify_test_counts(**counts)
    assert counts["matched"] == 5
    assert counts["passed"] == 4
    assert counts["skipped"] == 1
    assert result["resultKind"] == "ok"


def test_qa_actions_map_to_existing_gateway_tools() -> None:
    assert map_qa_action("status") == "qa_status"
    assert map_qa_action("list") == "qa_list"
    assert map_qa_action("run") == "qa_run"
    assert map_qa_action("start") == "qa_run"
    assert map_qa_action("cancel") == "qa_cancel"
    assert map_qa_action("capture") == "qa_capture"
    assert map_qa_action("recover") == "qa_recover"
