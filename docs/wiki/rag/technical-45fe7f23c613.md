---
source_id: technical:45fe7f23c613
source_path: docs/play-log-sheets-upload.md
source_sha256: 45fe7f23c6136e723a05ad4a06aa2af8162a1bf5750f411290a7f96f932bd34c
source_type: md
category: technical
title: play-log-sheets-upload
status: extracted
rag_eligible: true
---

# Play log → Google Sheets upload

분석 결과 CSV와 원본 로그를 Google Sheets에 업로드하는 **개발자/CI 전용** 도구입니다.
Unity 클라이언트에는 Google 인증 정보를 넣지 않습니다.

## 설치

```bash
pip install -r tools/requirements-analyze-play-logs.txt
pip install -r tools/requirements-upload-play-logs.txt
```

## 사전 준비

1. Google Cloud에서 **서비스 계정** 생성 및 JSON 키 다운로드
2. 대상 Google Spreadsheet를 서비스 계정 이메일과 **편집자**로 공유
3. 환경 변수 설정

```bash
# Windows PowerShell
$env:GOOGLE_APPLICATION_CREDENTIALS = "C:\secrets\play-log-sheets-sa.json"
$env:GOOGLE_SHEET_ID = "YOUR_SPREADSHEET_ID"
```

Spreadsheet ID는 URL 중 `/d/{ID}/` 구간입니다.

## 워크플로

```bash
# 1) CSV 분석
python tools/analyze_play_logs.py --input logs --output outputs

# 2) Sheets 업로드
python tools/upload_play_log_to_sheets.py --summary outputs --raw logs --spreadsheet-id YOUR_ID
```

**한 번에 실행** (권장):

```bash
python tools/run_play_log_pipeline.py --input logs --output outputs --spreadsheet-id YOUR_ID
```

환경 변수·종료 코드: `docs/play-log-pipeline.md`

`GOOGLE_SHEET_ID`가 설정돼 있으면 `--spreadsheet-id` 생략 가능:

```bash
python tools/upload_play_log_to_sheets.py --summary outputs --raw logs
```

## 갱신되는 시트

| 시트 | 소스 |
|------|------|
| `RawLogs` | `--raw` 디렉터리의 `*.csv` (병합) |
| `SessionSummary` | `outputs/session_summary.csv` |
| `PuzzleDifficulty` | `outputs/puzzle_difficulty.csv` |
| `ChartData` | `SessionSummary` + `PuzzleDifficulty`에서 생성한 차트용 표 4개 |
| `Dashboard` | `ChartData` 기반 차트 자동 생성 (best effort, 실패해도 업로드는 성공) |

- 시트가 있으면 **clear 후 전체 갱신**
- 없으면 **새로 생성**
- 1행 헤더 **freeze** + 헤더 bold/배경색 basic formatting (`ChartData`는 표별 제목·헤더 서식)

## ChartData 시트

`ChartData`는 Google Sheets에서 차트 범위를 잡기 쉽게 **가로로 분리**된 4개 표를 만듭니다.

| 블록 | 컬럼 | 설명 |
|------|------|------|
| `TopDifficultPuzzles` | A~ | `difficulty_score` 내림차순 Top 10 |
| `TopStuckPuzzles` | G~ | `avg_stuck_score` 내림차순 Top 10 |
| `HintUsageByPuzzle` | L~ | 퍼즐별 평균 힌트 사용 |
| `ClearRateByPuzzle` | P~ | 퍼즐별 클리어율 |

각 블록 구조:

1. 1행: 표 제목 (예: `TopDifficultPuzzles`)
2. 2행: 헤더
3. 3행~: 데이터 (최대 10행)

`clear_rate`는 **0.0% ~ 100.0%** 퍼센트 서식으로 표시됩니다 (원본 CSV의 0.0~1.0 비율 값).

차트 범위 예시 (헤더 포함):

- Top Difficult: `ChartData!A2:E12`
- Top Stuck: `ChartData!G2:J12`
- Hint Usage: `ChartData!L2:N{마지막행}`
- Clear Rate: `ChartData!P2:R{마지막행}`

`Dashboard` 시트에는 다음 차트를 **자동 생성 시도**합니다.

- Top Difficult Puzzles (막대)
- Clear Rate by Puzzle (가로 막대)

Sheets Charts API 제한·권한 문제로 실패하면 경고 로그만 남기고, `ChartData` 업로드는 그대로 성공합니다.

## 개인정보 옵션

기본적으로 `RawLogs`에서 `user_message`, `bot_response` 컬럼은 **업로드하지 않습니다**.

채팅 본문을 포함하려면:

```bash
python tools/upload_play_log_to_sheets.py --summary outputs --raw logs --spreadsheet-id YOUR_ID --include-chat-text
```

## 실패 로그

단계별로 `INFO`/`ERROR` 로그가 출력됩니다. 실패 시 `Upload failed at step '<단계명>'` 형태로 어느 단계에서 멈췄는지 확인할 수 있습니다.

예시 단계:

- `resolve_spreadsheet_id`
- `resolve_credentials`
- `authorize_client`
- `open_spreadsheet`
- `load_raw_logs`
- `load_summary_csv`
- `upload_raw_logs`
- `upload_session_summary`
- `upload_puzzle_difficulty`
- `upload_chart_data`
- `create_dashboard_charts` (best effort — 실패해도 exit code 0)

## 테스트 (mock, 자격 증명 불필요)

```bash
pip install -r tools/requirements-analyze-play-logs.txt
pip install -r tools/requirements-upload-play-logs.txt
pytest tools/tests/test_upload_play_log_to_sheets.py -q
```

## 관련 파일

- 업로드 스크립트: `tools/upload_play_log_to_sheets.py`
- 분석 스크립트: `tools/analyze_play_logs.py`
- 입력 스키마: `disputatio/docs/play-log-csv-columns.md`
