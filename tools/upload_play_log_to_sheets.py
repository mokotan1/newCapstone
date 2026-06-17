#!/usr/bin/env python3
"""Upload play-log analysis CSVs to Google Sheets (developer/CI tool only)."""

from __future__ import annotations

import argparse
import logging
import os
from dataclasses import dataclass
from pathlib import Path

import gspread
import pandas as pd
from google.oauth2.service_account import Credentials
from gspread.utils import rowcol_to_a1

from analyze_play_logs import load_play_logs

logger = logging.getLogger(__name__)

SHEET_RAW_LOGS = "RawLogs"
SHEET_SESSION_SUMMARY = "SessionSummary"
SHEET_PUZZLE_DIFFICULTY = "PuzzleDifficulty"
SHEET_CHART_DATA = "ChartData"
SHEET_DASHBOARD = "Dashboard"

SESSION_SUMMARY_FILE = "session_summary.csv"
PUZZLE_DIFFICULTY_FILE = "puzzle_difficulty.csv"

CHART_TOP_N = 10
CHART_COLUMN_GAP = 1

CHAT_TEXT_COLUMNS: tuple[str, ...] = ("user_message", "bot_response")

SCOPES: tuple[str, ...] = (
    "https://www.googleapis.com/auth/spreadsheets",
    "https://www.googleapis.com/auth/drive.file",
)


@dataclass(frozen=True)
class ChartSectionSpec:
    title: str
    dataframe: pd.DataFrame
    percent_columns: tuple[str, ...] = ()


@dataclass(frozen=True)
class ChartSectionLayout:
    title: str
    start_col: int
    end_col: int
    title_row: int
    header_row: int
    data_start_row: int
    data_end_row: int
    column_names: tuple[str, ...]

    def col_index(self, column_name: str) -> int:
        return self.start_col + self.column_names.index(column_name)

    def column_source_range(self, sheet_id: int, column_name: str) -> dict[str, object]:
        column = self.col_index(column_name)
        return {
            "sheetId": sheet_id,
            "startRowIndex": self.header_row - 1,
            "endRowIndex": self.data_end_row,
            "startColumnIndex": column - 1,
            "endColumnIndex": column,
        }


class UploadStepError(RuntimeError):
    """Raised when a named upload step fails."""

    def __init__(self, step: str, message: str) -> None:
        super().__init__(f"[{step}] {message}")
        self.step = step


def resolve_spreadsheet_id(cli_value: str | None) -> str:
    spreadsheet_id = (cli_value or os.environ.get("GOOGLE_SHEET_ID") or "").strip()
    if not spreadsheet_id:
        raise UploadStepError(
            "resolve_spreadsheet_id",
            "Spreadsheet ID is required. Pass --spreadsheet-id or set GOOGLE_SHEET_ID.",
        )
    return spreadsheet_id


def resolve_credentials_path() -> Path:
    raw_path = (os.environ.get("GOOGLE_APPLICATION_CREDENTIALS") or "").strip()
    if not raw_path:
        raise UploadStepError(
            "resolve_credentials",
            "GOOGLE_APPLICATION_CREDENTIALS is not set.",
        )

    credentials_path = Path(raw_path)
    if not credentials_path.is_file():
        raise UploadStepError(
            "resolve_credentials",
            f"Service account JSON not found: {credentials_path}",
        )
    return credentials_path


def authorize_client(credentials_path: Path) -> gspread.Client:
    try:
        credentials = Credentials.from_service_account_file(
            str(credentials_path),
            scopes=list(SCOPES),
        )
        return gspread.authorize(credentials)
    except Exception as exc:  # noqa: BLE001 - surface auth failures clearly
        raise UploadStepError("authorize_client", str(exc)) from exc


def open_spreadsheet(client: gspread.Client, spreadsheet_id: str) -> gspread.Spreadsheet:
    try:
        return client.open_by_key(spreadsheet_id)
    except Exception as exc:  # noqa: BLE001
        raise UploadStepError("open_spreadsheet", str(exc)) from exc


def filter_chat_columns(df: pd.DataFrame, include_chat_text: bool) -> pd.DataFrame:
    if include_chat_text:
        return df
    drop_cols = [col for col in CHAT_TEXT_COLUMNS if col in df.columns]
    if not drop_cols:
        return df
    return df.drop(columns=drop_cols)


def load_raw_logs_dataframe(raw_path: Path, include_chat_text: bool) -> pd.DataFrame:
    if not raw_path.exists():
        raise UploadStepError("load_raw_logs", f"Raw log path does not exist: {raw_path}")

    try:
        frame = load_play_logs(raw_path)
    except Exception as exc:  # noqa: BLE001
        raise UploadStepError("load_raw_logs", str(exc)) from exc

    if "_source_file" in frame.columns:
        frame = frame.drop(columns=["_source_file"])
    if "solved_bool" in frame.columns:
        frame = frame.drop(columns=["solved_bool"])

    return filter_chat_columns(frame, include_chat_text)


def load_summary_csv(summary_dir: Path, filename: str) -> pd.DataFrame:
    csv_path = summary_dir / filename
    if not csv_path.is_file():
        raise UploadStepError(
            "load_summary_csv",
            f"Missing summary file: {csv_path}. Run tools/analyze_play_logs.py first.",
        )
    try:
        return pd.read_csv(csv_path, encoding="utf-8-sig")
    except Exception as exc:  # noqa: BLE001
        raise UploadStepError("load_summary_csv", f"{csv_path}: {exc}") from exc


def dataframe_to_values(df: pd.DataFrame) -> list[list[str]]:
    cleaned = df.fillna("")
    header = [str(col) for col in cleaned.columns.tolist()]
    rows = cleaned.astype(str).values.tolist()
    return [header, *rows]


def apply_basic_formatting(worksheet: gspread.Worksheet, column_count: int) -> None:
    if column_count <= 0:
        return

    header_end = rowcol_to_a1(1, column_count)
    worksheet.freeze(rows=1)
    worksheet.format(
        f"A1:{header_end}",
        {
            "textFormat": {"bold": True},
            "backgroundColor": {"red": 0.92, "green": 0.92, "blue": 0.92},
            "horizontalAlignment": "CENTER",
        },
    )


def get_or_create_worksheet(
    spreadsheet: gspread.Spreadsheet,
    title: str,
    *,
    min_rows: int,
    min_cols: int,
) -> gspread.Worksheet:
    try:
        worksheet = spreadsheet.worksheet(title)
        logger.info("Found existing worksheet: %s", title)
        return worksheet
    except gspread.WorksheetNotFound:
        logger.info("Creating worksheet: %s", title)
        return spreadsheet.add_worksheet(
            title=title,
            rows=max(min_rows, 100),
            cols=max(min_cols, 26),
        )


def upload_dataframe_to_worksheet(
    spreadsheet: gspread.Spreadsheet,
    title: str,
    df: pd.DataFrame,
    *,
    step: str,
) -> None:
    if df.empty:
        logger.warning("[%s] %s is empty; uploading header row only.", step, title)

    values = dataframe_to_values(df)
    row_count = max(len(values), 2)
    col_count = max(len(values[0]), 1) if values else 1

    try:
        worksheet = get_or_create_worksheet(
            spreadsheet,
            title,
            min_rows=row_count,
            min_cols=col_count,
        )
        worksheet.clear()
        worksheet.resize(rows=row_count, cols=col_count)
        worksheet.update(values, value_input_option="RAW")
        apply_basic_formatting(worksheet, col_count)
        logger.info("[%s] Uploaded %s rows to %s", step, max(len(values) - 1, 0), title)
    except Exception as exc:  # noqa: BLE001
        raise UploadStepError(step, str(exc)) from exc


def build_top_difficult_puzzles(puzzle_difficulty: pd.DataFrame) -> pd.DataFrame:
    columns = (
        "scene_name",
        "puzzle_id",
        "difficulty_score",
        "clear_rate",
        "median_clear_time",
    )
    missing = [col for col in columns if col not in puzzle_difficulty.columns]
    if missing:
        raise UploadStepError(
            "build_chart_data",
            f"PuzzleDifficulty missing columns: {', '.join(missing)}",
        )

    frame = puzzle_difficulty.loc[:, list(columns)].copy()
    frame = frame.sort_values("difficulty_score", ascending=False).head(CHART_TOP_N)
    return frame.reset_index(drop=True)


def build_top_stuck_puzzles(session_summary: pd.DataFrame) -> pd.DataFrame:
    required = ("scene_name", "puzzle_id", "stuck_score", "session_id")
    missing = [col for col in required if col not in session_summary.columns]
    if missing:
        raise UploadStepError(
            "build_chart_data",
            f"SessionSummary missing columns: {', '.join(missing)}",
        )

    grouped = (
        session_summary.groupby(["scene_name", "puzzle_id"], as_index=False)
        .agg(
            avg_stuck_score=("stuck_score", "mean"),
            session_count=("session_id", "count"),
        )
        .sort_values("avg_stuck_score", ascending=False)
        .head(CHART_TOP_N)
    )
    return grouped.reset_index(drop=True)


def build_hint_usage_by_puzzle(puzzle_difficulty: pd.DataFrame) -> pd.DataFrame:
    columns = ("scene_name", "puzzle_id", "avg_hint_count")
    missing = [col for col in columns if col not in puzzle_difficulty.columns]
    if missing:
        raise UploadStepError(
            "build_chart_data",
            f"PuzzleDifficulty missing columns: {', '.join(missing)}",
        )

    frame = puzzle_difficulty.loc[:, list(columns)].copy()
    return frame.sort_values(["scene_name", "puzzle_id"]).reset_index(drop=True)


def build_clear_rate_by_puzzle(puzzle_difficulty: pd.DataFrame) -> pd.DataFrame:
    columns = ("scene_name", "puzzle_id", "clear_rate")
    missing = [col for col in columns if col not in puzzle_difficulty.columns]
    if missing:
        raise UploadStepError(
            "build_chart_data",
            f"PuzzleDifficulty missing columns: {', '.join(missing)}",
        )

    frame = puzzle_difficulty.loc[:, list(columns)].copy()
    return frame.sort_values("clear_rate", ascending=True).reset_index(drop=True)


def build_chart_data_sections(
    session_summary: pd.DataFrame,
    puzzle_difficulty: pd.DataFrame,
) -> list[ChartSectionSpec]:
    return [
        ChartSectionSpec(
            title="TopDifficultPuzzles",
            dataframe=build_top_difficult_puzzles(puzzle_difficulty),
            percent_columns=("clear_rate",),
        ),
        ChartSectionSpec(
            title="TopStuckPuzzles",
            dataframe=build_top_stuck_puzzles(session_summary),
        ),
        ChartSectionSpec(
            title="HintUsageByPuzzle",
            dataframe=build_hint_usage_by_puzzle(puzzle_difficulty),
        ),
        ChartSectionSpec(
            title="ClearRateByPuzzle",
            dataframe=build_clear_rate_by_puzzle(puzzle_difficulty),
            percent_columns=("clear_rate",),
        ),
    ]


def _normalize_grid_cell(value: object) -> object:
    if value is None:
        return ""
    if isinstance(value, float) and pd.isna(value):
        return ""
    return value


def layout_chart_data_horizontally(
    sections: list[ChartSectionSpec],
) -> tuple[list[list[object]], list[ChartSectionLayout]]:
    if not sections:
        return [[""]], []

    title_row = 1
    header_row = 2
    data_start_row = 3
    max_data_rows = max(
        (max(len(section.dataframe), 1) for section in sections),
        default=1,
    )
    max_data_rows = min(max_data_rows, CHART_TOP_N)
    sheet_height = header_row + max_data_rows

    placements: list[tuple[ChartSectionSpec, int, int]] = []
    current_col = 1
    max_col = 1
    for section in sections:
        width = max(len(section.dataframe.columns), 1)
        placements.append((section, current_col, width))
        max_col = max(max_col, current_col + width - 1)
        current_col += width + CHART_COLUMN_GAP

    grid: list[list[object]] = [["" for _ in range(max_col)] for _ in range(sheet_height)]
    layouts: list[ChartSectionLayout] = []

    for section, start_col, width in placements:
        frame = section.dataframe
        column_names = tuple(frame.columns.astype(str).tolist()) if not frame.empty else ("",)
        data_end_row = data_start_row + max(len(frame), 1) - 1

        grid[title_row - 1][start_col - 1] = section.title
        for index, header in enumerate(column_names):
            grid[header_row - 1][start_col - 1 + index] = header

        for row_offset, row in enumerate(frame.itertuples(index=False, name=None)):
            for col_offset, value in enumerate(row):
                grid[data_start_row - 1 + row_offset][start_col - 1 + col_offset] = (
                    _normalize_grid_cell(value)
                )

        layouts.append(
            ChartSectionLayout(
                title=section.title,
                start_col=start_col,
                end_col=start_col + width - 1,
                title_row=title_row,
                header_row=header_row,
                data_start_row=data_start_row,
                data_end_row=data_end_row,
                column_names=column_names,
            )
        )

    return grid, layouts


def apply_chart_data_formatting(
    worksheet: gspread.Worksheet,
    layouts: list[ChartSectionLayout],
    percent_columns_by_title: dict[str, tuple[str, ...]],
) -> None:
    header_format = {
        "textFormat": {"bold": True},
        "backgroundColor": {"red": 0.92, "green": 0.92, "blue": 0.92},
        "horizontalAlignment": "CENTER",
    }
    title_format = {
        "textFormat": {"bold": True, "fontSize": 11},
        "backgroundColor": {"red": 0.85, "green": 0.89, "blue": 0.95},
    }
    percent_format = {"numberFormat": {"type": "PERCENT", "pattern": "0.0%"}}

    for layout in layouts:
        title_cell = rowcol_to_a1(layout.title_row, layout.start_col)
        worksheet.format(title_cell, title_format)

        header_start = rowcol_to_a1(layout.header_row, layout.start_col)
        header_end = rowcol_to_a1(layout.header_row, layout.end_col)
        worksheet.format(f"{header_start}:{header_end}", header_format)

        for column_name in percent_columns_by_title.get(layout.title, ()):
            if column_name not in layout.column_names:
                continue
            column_index = layout.col_index(column_name)
            if layout.data_end_row < layout.data_start_row:
                continue
            range_start = rowcol_to_a1(layout.data_start_row, column_index)
            range_end = rowcol_to_a1(layout.data_end_row, column_index)
            worksheet.format(f"{range_start}:{range_end}", percent_format)


def upload_chart_data_worksheet(
    spreadsheet: gspread.Spreadsheet,
    session_summary: pd.DataFrame,
    puzzle_difficulty: pd.DataFrame,
) -> list[ChartSectionLayout]:
    logger.info("Step: build ChartData sections")
    sections = build_chart_data_sections(session_summary, puzzle_difficulty)
    grid, layouts = layout_chart_data_horizontally(sections)

    row_count = max(len(grid), 2)
    col_count = max(len(grid[0]), 1) if grid else 1
    percent_columns_by_title = {
        section.title: section.percent_columns for section in sections
    }

    try:
        worksheet = get_or_create_worksheet(
            spreadsheet,
            SHEET_CHART_DATA,
            min_rows=row_count,
            min_cols=col_count,
        )
        worksheet.clear()
        worksheet.resize(rows=row_count, cols=col_count)
        worksheet.update(grid, value_input_option="USER_ENTERED")
        apply_chart_data_formatting(worksheet, layouts, percent_columns_by_title)
        logger.info(
            "[upload_chart_data] Uploaded %s chart tables to %s",
            len(sections),
            SHEET_CHART_DATA,
        )
        return layouts
    except Exception as exc:  # noqa: BLE001
        raise UploadStepError("upload_chart_data", str(exc)) from exc


def try_create_dashboard_charts(
    spreadsheet: gspread.Spreadsheet,
    chart_worksheet: gspread.Worksheet,
    layouts: list[ChartSectionLayout],
) -> None:
    logger.info("Step: create Dashboard charts (best effort)")
    try:
        dashboard = get_or_create_worksheet(
            spreadsheet,
            SHEET_DASHBOARD,
            min_rows=60,
            min_cols=24,
        )
        dashboard.clear()

        difficult_layout = next(
            (layout for layout in layouts if layout.title == "TopDifficultPuzzles"),
            None,
        )
        clear_rate_layout = next(
            (layout for layout in layouts if layout.title == "ClearRateByPuzzle"),
            None,
        )
        if difficult_layout is None or clear_rate_layout is None:
            logger.warning(
                "Dashboard chart creation skipped: required ChartData sections missing."
            )
            return

        chart_sheet_id = chart_worksheet.id
        dashboard_sheet_id = dashboard.id

        requests: list[dict[str, object]] = [
            {
                "addChart": {
                    "chart": {
                        "spec": {
                            "title": "Top Difficult Puzzles",
                            "basicChart": {
                                "chartType": "COLUMN",
                                "legendPosition": "NO_LEGEND",
                                "axis": [
                                    {"position": "BOTTOM_AXIS", "title": "Puzzle"},
                                    {"position": "LEFT_AXIS", "title": "Difficulty Score"},
                                ],
                                "domains": [
                                    {
                                        "domain": {
                                            "sourceRange": {
                                                "sources": [
                                                    difficult_layout.column_source_range(
                                                        chart_sheet_id,
                                                        "puzzle_id",
                                                    )
                                                ]
                                            }
                                        }
                                    }
                                ],
                                "series": [
                                    {
                                        "series": {
                                            "sourceRange": {
                                                "sources": [
                                                    difficult_layout.column_source_range(
                                                        chart_sheet_id,
                                                        "difficulty_score",
                                                    )
                                                ]
                                            }
                                        },
                                        "targetAxis": "LEFT_AXIS",
                                    }
                                ],
                            },
                        },
                        "position": {
                            "overlayPosition": {
                                "anchorCell": {
                                    "sheetId": dashboard_sheet_id,
                                    "rowIndex": 0,
                                    "columnIndex": 0,
                                },
                                "widthPixels": 640,
                                "heightPixels": 380,
                            }
                        },
                    }
                }
            },
            {
                "addChart": {
                    "chart": {
                        "spec": {
                            "title": "Clear Rate by Puzzle",
                            "basicChart": {
                                "chartType": "BAR",
                                "legendPosition": "NO_LEGEND",
                                "axis": [
                                    {"position": "BOTTOM_AXIS", "title": "Clear Rate"},
                                    {"position": "LEFT_AXIS", "title": "Puzzle"},
                                ],
                                "domains": [
                                    {
                                        "domain": {
                                            "sourceRange": {
                                                "sources": [
                                                    clear_rate_layout.column_source_range(
                                                        chart_sheet_id,
                                                        "puzzle_id",
                                                    )
                                                ]
                                            }
                                        }
                                    }
                                ],
                                "series": [
                                    {
                                        "series": {
                                            "sourceRange": {
                                                "sources": [
                                                    clear_rate_layout.column_source_range(
                                                        chart_sheet_id,
                                                        "clear_rate",
                                                    )
                                                ]
                                            }
                                        },
                                        "targetAxis": "BOTTOM_AXIS",
                                    }
                                ],
                            },
                        },
                        "position": {
                            "overlayPosition": {
                                "anchorCell": {
                                    "sheetId": dashboard_sheet_id,
                                    "rowIndex": 0,
                                    "columnIndex": 8,
                                },
                                "widthPixels": 640,
                                "heightPixels": 380,
                            }
                        },
                    }
                }
            },
        ]

        spreadsheet.batch_update({"requests": requests})
        logger.info("Dashboard charts created on %s", SHEET_DASHBOARD)
    except Exception as exc:  # noqa: BLE001 - dashboard is optional
        logger.warning(
            "Dashboard chart creation failed (ChartData upload still succeeded): %s",
            exc,
        )


def upload_play_logs_to_sheets(
    *,
    summary_dir: Path,
    raw_dir: Path,
    spreadsheet_id: str,
    include_chat_text: bool = False,
    client: gspread.Client | None = None,
) -> None:
    logger.info("Step: resolve credentials")
    credentials_path = resolve_credentials_path()

    if client is None:
        logger.info("Step: authorize Google Sheets client")
        client = authorize_client(credentials_path)

    logger.info("Step: open spreadsheet %s", spreadsheet_id)
    spreadsheet = open_spreadsheet(client, spreadsheet_id)

    logger.info("Step: load raw logs from %s", raw_dir)
    raw_logs = load_raw_logs_dataframe(raw_dir, include_chat_text)

    logger.info("Step: load summary CSVs from %s", summary_dir)
    session_summary = load_summary_csv(summary_dir, SESSION_SUMMARY_FILE)
    puzzle_difficulty = load_summary_csv(summary_dir, PUZZLE_DIFFICULTY_FILE)

    upload_dataframe_to_worksheet(
        spreadsheet,
        SHEET_RAW_LOGS,
        raw_logs,
        step="upload_raw_logs",
    )
    upload_dataframe_to_worksheet(
        spreadsheet,
        SHEET_SESSION_SUMMARY,
        session_summary,
        step="upload_session_summary",
    )
    upload_dataframe_to_worksheet(
        spreadsheet,
        SHEET_PUZZLE_DIFFICULTY,
        puzzle_difficulty,
        step="upload_puzzle_difficulty",
    )

    chart_layouts = upload_chart_data_worksheet(
        spreadsheet,
        session_summary,
        puzzle_difficulty,
    )
    chart_worksheet = spreadsheet.worksheet(SHEET_CHART_DATA)
    try_create_dashboard_charts(spreadsheet, chart_worksheet, chart_layouts)


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Upload play-log raw CSVs and analysis summaries to Google Sheets. "
            "Run only on developer machines or CI — never embed credentials in Unity."
        ),
    )
    parser.add_argument(
        "--summary",
        type=Path,
        required=True,
        help="Directory containing session_summary.csv and puzzle_difficulty.csv.",
    )
    parser.add_argument(
        "--raw",
        type=Path,
        required=True,
        help="Directory containing raw play-log CSV files (*.csv).",
    )
    parser.add_argument(
        "--spreadsheet-id",
        type=str,
        default=None,
        help="Target Google Spreadsheet ID (fallback: GOOGLE_SHEET_ID env var).",
    )
    parser.add_argument(
        "--include-chat-text",
        action="store_true",
        help="Include user_message and bot_response columns in RawLogs (default: excluded).",
    )
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    logging.basicConfig(level=logging.INFO, format="%(levelname)s %(message)s")
    args = parse_args(argv)

    try:
        spreadsheet_id = resolve_spreadsheet_id(args.spreadsheet_id)
        upload_play_logs_to_sheets(
            summary_dir=args.summary,
            raw_dir=args.raw,
            spreadsheet_id=spreadsheet_id,
            include_chat_text=args.include_chat_text,
        )
    except UploadStepError as exc:
        logger.error("Upload failed at step '%s': %s", exc.step, exc)
        return 1
    except Exception:
        logger.exception("Upload failed with unexpected error")
        return 1

    logger.info("Upload completed successfully.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
