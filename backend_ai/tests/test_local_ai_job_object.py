from __future__ import annotations

from services.local_ai.job_object import (
    JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE,
    extended_limit_flags,
)


def test_extended_limit_includes_kill_on_job_close() -> None:
    flags = extended_limit_flags()
    assert JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE == 0x2000
    assert flags & JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
    assert flags & JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE == JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
