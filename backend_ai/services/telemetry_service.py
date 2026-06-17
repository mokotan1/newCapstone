from __future__ import annotations

import csv
import logging
from pathlib import Path

from models.requests import TelemetryEvent

logger = logging.getLogger(__name__)

#: 서버 CSV 컬럼 순서. Unity ``PlayLogCsvColumns.Ordered``와 반드시 동일하게 유지한다.
#: (disputatio/Assets/godlotto/Script/DialogueLog/PlayLogCsvColumns.cs)
TELEMETRY_COLUMNS: tuple[str, ...] = (
    "session_id",
    "anonymous_player_id",
    "scene_name",
    "puzzle_id",
    "event_time",
    "event_type",
    "user_message",
    "bot_response",
    "hint_level",
    "progress_state",
    "time_since_scene_start",
    "attempt_count",
    "wrong_action_count",
    "repeated_question_count",
    "solved",
)

#: 스프레드시트 수식 인젝션을 유발할 수 있는 선두 문자.
_FORMULA_INJECTION_PREFIXES = ("=", "+", "-", "@", "\t", "\r")

_DEFAULT_CSV_FILENAME = "play_logs.csv"


class TelemetryService:
    """플레이 로그 이벤트를 CSV에 append한다.

    파일 I/O 책임만 가진다(SRP). 로그 디렉터리를 주입받아 테스트 시
    임시 경로를 쓸 수 있다(DIP·테스트 용이성).
    """

    def __init__(self, log_dir: Path | str, csv_filename: str = _DEFAULT_CSV_FILENAME) -> None:
        self._log_dir = Path(log_dir)
        self._csv_path = self._log_dir / csv_filename

    @property
    def csv_path(self) -> Path:
        return self._csv_path

    def append_events(self, events: list[TelemetryEvent]) -> int:
        """이벤트들을 CSV에 append하고 기록한 행 수를 반환한다.

        파일이 없거나 비어 있으면 헤더를 먼저 쓴다. 실패해도 예외를 밖으로
        던지지 않고 0을 반환한다(서버 다운 방지, fail-safe).
        """
        if not events:
            return 0

        try:
            self._log_dir.mkdir(parents=True, exist_ok=True)
            write_header = not self._csv_path.exists() or self._csv_path.stat().st_size == 0

            with self._csv_path.open("a", encoding="utf-8-sig", newline="") as fh:
                writer = csv.writer(fh, lineterminator="\n")
                if write_header:
                    writer.writerow(TELEMETRY_COLUMNS)
                for event in events:
                    writer.writerow(self._to_row(event))

            return len(events)
        except OSError:
            # 디스크/권한 문제로 수집이 막혀도 API는 살아 있어야 한다.
            logger.exception("telemetry append failed")
            return 0

    @classmethod
    def _to_row(cls, event: TelemetryEvent) -> list[str]:
        return [
            cls._sanitize(event.session_id),
            cls._sanitize(event.anonymous_player_id),
            cls._sanitize(event.scene_name),
            cls._sanitize(event.puzzle_id),
            cls._sanitize(event.event_time),
            cls._sanitize(event.event_type),
            cls._sanitize(event.user_message),
            cls._sanitize(event.bot_response),
            cls._sanitize(event.hint_level),
            cls._sanitize(event.progress_state),
            cls._format_float(event.time_since_scene_start),
            str(event.attempt_count),
            str(event.wrong_action_count),
            str(event.repeated_question_count),
            "true" if event.solved else "false",
        ]

    @staticmethod
    def _format_float(value: float) -> str:
        # Unity FormatFloat("0.###")와 동일하게 불필요한 0 제거.
        return f"{value:.3f}".rstrip("0").rstrip(".") or "0"

    @staticmethod
    def _sanitize(value: str) -> str:
        """CSV 수식 인젝션 방어. RFC4180 인용은 csv.writer가 처리한다."""
        text = value or ""
        if text and text[0] in _FORMULA_INJECTION_PREFIXES:
            return "'" + text
        return text
