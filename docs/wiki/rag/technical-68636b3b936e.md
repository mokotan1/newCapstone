---
source_id: technical:68636b3b936e
source_path: docs/play-log-pipeline.md
source_sha256: 68636b3b936e63b4ca1aed003ec398904180194ef35c3d22200fb8fc2115bf58
source_type: md
category: technical
title: play-log-pipeline
status: extracted
rag_eligible: true
---

# Play log → Google Sheets pipeline

분석(`analyze_play_logs.py`)과 Google Sheets 업로드(`upload_play_log_to_sheets.py`)를 **한 번에** 실행하는 운영/CI용 스크립트입니다.

## 설치

```bash
pip install -r tools/requirements-play-log-pipeline.txt
```

## 환경 변수

| 변수 | 필수 | 설명 |
|------|------|------|
| `GOOGLE_APPLICATION_CREDENTIALS` | 업로드 시 | 서비스 계정 JSON **파일 경로** |
| `GOOGLE_SHEET_ID` | 업로드 시 (CLI 생략 가능) | 대상 Spreadsheet ID |
| `INCLUDE_CHAT_TEXT` | 아니오 | `true`/`1`/`yes`/`on`이면 RawLogs에 채팅 본문 포함 (기본 `false`) |

템플릿:

- `tools/.env.example` — 변수 이름·설명
- `tools/secrets/google-service-account.example.json` — JSON 구조 예시 (**커밋 가능**)

실제 키 파일(`tools/secrets/google-service-account.json` 등)은 `.gitignore`로 **커밋 금지**입니다.

### PowerShell 예시

```powershell
$env:GOOGLE_APPLICATION_CREDENTIALS = "C:\secrets\play-log-sheets-sa.json"
$env:GOOGLE_SHEET_ID = "YOUR_SPREADSHEET_ID"
$env:INCLUDE_CHAT_TEXT = "false"

python tools/run_play_log_pipeline.py --input logs --output outputs --spreadsheet-id YOUR_ID
```

`GOOGLE_SHEET_ID`가 설정돼 있으면 `--spreadsheet-id` 생략 가능:

```powershell
python tools/run_play_log_pipeline.py --input logs --output outputs
```

## 실행

```bash
python tools/run_play_log_pipeline.py --input logs --output outputs --spreadsheet-id YOUR_ID
```

Fixture 스모크 (업로드는 mock 테스트로 검증):

```bash
python tools/run_play_log_pipeline.py --input tools/fixtures/play_logs --output outputs
```

## 파이프라인 단계

1. **analyze** — `logs/*.csv` → `outputs/session_summary.csv`, `outputs/puzzle_difficulty.csv`
2. **upload** — RawLogs, SessionSummary, PuzzleDifficulty, ChartData, Dashboard(optional)

## 완료 요약 출력

성공 시 stdout에 다음을 출력합니다.

- 처리한 raw log **파일 수**
- **session** 수
- **puzzle** 수
- `difficulty_score` **상위 5개**
- 생성된 CSV 경로, Spreadsheet ID

## 종료 코드

| 코드 | 의미 |
|------|------|
| `0` | 성공 |
| `1` | **analyze** 단계 실패 (입력 CSV 없음, 컬럼 누락 등) |
| `2` | **upload** 단계 실패 (인증, Spreadsheet 접근, Sheets API 등) |

실패 시 stderr:

```
ERROR: stage=upload exit_code=2 message=[authorize_client] ...
```

## 옵션

| 옵션 | 설명 |
|------|------|
| `--input`, `-i` | raw play-log CSV 디렉터리 또는 단일 파일 |
| `--output`, `-o` | 분석 CSV 출력 디렉터리 |
| `--spreadsheet-id` | Spreadsheet ID (`GOOGLE_SHEET_ID` 대체) |
| `--include-chat-text` | CLI로 채팅 본문 업로드 강제 (env보다 우선) |

## 테스트

```bash
pytest tools/tests/test_run_play_log_pipeline.py -q
```

## 관련 문서

- 분석: `docs/play-log-analysis.md`
- Sheets 업로드 상세: `docs/play-log-sheets-upload.md`
- 입력 스키마: `disputatio/docs/play-log-csv-columns.md`
