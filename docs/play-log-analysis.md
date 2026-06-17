# Play log analysis (Python)

Unity 클라이언트가 기록한 플레이 로그 CSV(`disputatio/docs/play-log-csv-columns.md` 스키마)를 읽어 **세션 요약**과 **퍼즐 난이도** CSV를 생성한다.

## 설치

```bash
pip install -r tools/requirements-analyze-play-logs.txt
```

## 실행

```bash
python tools/analyze_play_logs.py --input logs --output outputs
```

**분석 + Google Sheets 업로드 한 번에** (운영/CI):

```bash
python tools/run_play_log_pipeline.py --input logs --output outputs --spreadsheet-id YOUR_ID
```

상세: `docs/play-log-pipeline.md`

단일 파일:

```bash
python tools/analyze_play_logs.py --input logs/play_log_sess-a.csv --output outputs
```

Fixture로 스모크 테스트:

```bash
python tools/analyze_play_logs.py --input tools/fixtures/play_logs --output outputs
```

## 출력

| 파일 | 설명 |
|------|------|
| `outputs/session_summary.csv` | 세션 × 씬 × 퍼즐 단위 지표 |
| `outputs/puzzle_difficulty.csv` | 퍼즐별 집계 난이도 |
| `outputs/player_context_summary.json` | 챗봇 맥락용 세션 요약 (원문 대화 미포함) |

### player_context_summary.json

CSV 전체를 프롬프트에 넣지 않고, **세션별 요약만** JSON으로 제공합니다.  
`backend_ai`가 `session_id`로 조회해 시스템 프롬프트에 짧게 붙이는 용도입니다.

```json
{
  "version": 1,
  "generated_at": "2026-06-16T12:00:00+00:00",
  "sessions": [
    {
      "session_id": "sess-a",
      "player_id": "anon-player-1",
      "current_scene": "Kitchen",
      "current_puzzle": "Kitchen",
      "stuck_score": 44,
      "hint_count": 1,
      "wrong_action_count": 1,
      "repeated_question_count": 2,
      "recommended_hint_policy": "light_hint",
      "solved": true
    }
  ]
}
```

`current_scene` / `current_puzzle`은 해당 세션의 **가장 최근 이벤트** 기준입니다.

#### recommended_hint_policy

| stuck_score | 정책 |
|-------------|------|
| 0–39 | `normal` |
| 40–69 | `light_hint` |
| 70–100 | `direct_hint` |

#### backend_ai 연동 예 (향후)

```python
import json
from pathlib import Path

payload = json.loads(Path("outputs/player_context_summary.json").read_text(encoding="utf-8"))
ctx = next(s for s in payload["sessions"] if s["session_id"] == request.session_id)
# system prompt에 tools/player_context_summary.format_prompt_snippet(ctx) 추가
```

`user_message` / `bot_response` 원문은 **포함하지 않습니다**.

생성 모듈: `tools/player_context_summary.py`

### session_summary.csv

| 컬럼 | 설명 |
|------|------|
| `session_id` | 플레이 세션 |
| `player_id` | `anonymous_player_id` |
| `scene_name` | 씬 |
| `puzzle_id` | 퍼즐 ID |
| `clear_time_seconds` | 클리어 시 `puzzle_solved`의 `time_since_scene_start`, 미클리어 시 씬 체류 최대 시간 |
| `hint_count` | `give_hint` 이벤트 수 |
| `wrong_action_count` | 씬 내 `wrong_action_count` 최대값 |
| `repeated_question_count` | 씬 내 `repeated_question_count` 최대값 |
| `solved` | 클리어 여부 |
| `stuck_score` | 0–100 막힘 점수 |

### puzzle_difficulty.csv

| 컬럼 | 설명 |
|------|------|
| `session_count` | 해당 퍼즐을 플레이한 세션 수 |
| `clear_rate` | 클리어 비율 |
| `median_clear_time` | 클리어 세션의 클리어 시간 중앙값(초) |
| `avg_hint_count` | 평균 힌트 사용 |
| `avg_wrong_action_count` | 평균 잘못된 행동 |
| `repeat_question_rate` | `repeated_question_count >= 2` 세션 비율 |
| `abandon_rate` | `1 - clear_rate` |
| `difficulty_score` | 0–100 난이도 점수 |

## 점수 공식

### stuck_score (세션)

```
stuck_score =
  0.35 * no_progress_time_score
+ 0.25 * repeated_question_score
+ 0.20 * hint_dependency_score
+ 0.20 * wrong_attempt_score
```

부분 점수는 각각 `clear_time_seconds`, `repeated_question_count`, `hint_count`, `wrong_action_count`를 cap 기준으로 0–100 정규화 후 clamp.

기본 cap:

| 지표 | cap |
|------|-----|
| 시간 | 600초 |
| 반복 질문 | 5회 |
| 힌트 | 5회 |
| 잘못된 행동 | 10회 |

### difficulty_score (퍼즐)

```
difficulty_score =
  0.30 * time_score
+ 0.25 * hint_score
+ 0.20 * fail_score
+ 0.15 * repeat_score
+ 0.10 * abandon_score
```

- `fail_score = (1 - clear_rate) * 100`
- `repeat_score = repeat_question_rate * 100`
- `abandon_score = abandon_rate * 100`
- 클리어 0건 퍼즐의 `time_score`는 100으로 처리

모든 score는 0–100 clamp.

## 필수 입력 컬럼

다음 컬럼이 없으면 **어느 파일에서 무엇이 빠졌는지** 에러 메시지로 종료한다.

```
session_id, anonymous_player_id, scene_name, puzzle_id, event_time,
event_type, time_since_scene_start, attempt_count, wrong_action_count,
repeated_question_count, solved
```

## 테스트

```bash
pip install -r tools/requirements-analyze-play-logs.txt
pytest tools/tests/test_analyze_play_logs.py -q
```

## 관련 문서

- 입력 스키마: `disputatio/docs/play-log-csv-columns.md`
- 분석 스크립트: `tools/analyze_play_logs.py`
- **Sheets 업로드**: `docs/play-log-sheets-upload.md`
- **분석+업로드 파이프라인**: `docs/play-log-pipeline.md`
- 샘플 입력: `tools/fixtures/play_logs/sample_sessions.csv`
