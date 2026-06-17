# Play log CSV columns

플레이·챗봇 분석용 CSV는 Unity 클라이언트가 런타임에 append-only로 기록한다.

## 파일 위치

| 항목 | 값 |
|------|-----|
| 디렉터리 | `{Application.persistentDataPath}/PlayLogs/` |
| 파일명 | `play_log_{session_id}.csv` (기본 패턴) |
| 인코딩 | UTF-8 BOM |
| 구현 | `PlayLogRecorder`, `PlayLogCsvLogic` |

Windows 예: `%USERPROFILE%\AppData\LocalLow\<Company>\<Product>\PlayLogs\play_log_<guid>.csv`

`session_id`는 앱 실행 세션마다 `PlayerPrefs`에 저장된 GUID다.  
`anonymous_player_id`는 `ChatHttpClient.ResolveChatClientUserId()` (`anon-{guid}`)와 동일하다.

## 컬럼 순서 (고정)

| # | 컬럼 | 설명 |
|---|------|------|
| 1 | `session_id` | 플레이 세션 GUID |
| 2 | `anonymous_player_id` | 익명 플레이어 ID (`anon-…`) |
| 3 | `scene_name` | Unity 활성 씬 이름 |
| 4 | `puzzle_id` | 퍼즐 식별자 (현재는 `scene_name`과 동일) |
| 5 | `event_time` | UTC ISO-8601 (`O` 형식) |
| 6 | `event_type` | 이벤트 종류 (아래 표) |
| 7 | `user_message` | 플레이어 질문 (옵션으로 비활성화 가능) |
| 8 | `bot_response` | 체셔 응답 (옵션으로 비활성화 가능) |
| 9 | `hint_level` | `give_hint` 툴의 hint_level |
| 10 | `progress_state` | 퀘스트·룸 스냅샷 (`quest=…;step=…` 등) |
| 11 | `time_since_scene_start` | 씬 진입 후 경과 초 (unscaled) |
| 12 | `attempt_count` | 해당 씬에서의 누적 시도(질문) 횟수 |
| 13 | `wrong_action_count` | 해당 씬에서의 잘못된 행동 누적 |
| 14 | `repeated_question_count` | 동일 정규화 질문의 누적 횟수(해당 씬) |
| 15 | `solved` | `true` / `false` — `PuzzleSolvedStateProvider` 기준 |

## event_type 값

| 값 | 발생 시점 |
|----|-----------|
| `scene_enter` | 씬 로드·세션 시작 |
| `cheshire_user_message` | 체셔에게 질문 전송 |
| `cheshire_bot_response` | 체셔 최종 응답 수신 |
| `give_hint` | `give_hint` 툴 호출 |
| `wrong_action` | `PlayLogRecorder.RecordWrongAction()` |
| `puzzle_solved` | 씬 로드 시 이미 solved이거나 `RecordPuzzleSolved()` |

## 개인정보·본문 옵션

`Resources/PlayLogSettings` ScriptableObject (없으면 기본값):

| 필드 | 기본 | 설명 |
|------|------|------|
| `enableCsvLogging` | `true` | CSV 기록 on/off |
| `includeMessageContent` | `true` | `false`면 `user_message`·`bot_response`를 빈 칸으로 기록 |

## UI 로그와의 관계

`CheshireLogEntry` / `DialogueLogPanel`은 세션 메모리 UI용이다.  
CSV는 `PlayLogRecorder`가 별도로 기록하며, UI 로그 구조를 변경하지 않는다.

## 샘플 헤더

```csv
session_id,anonymous_player_id,scene_name,puzzle_id,event_time,event_type,user_message,bot_response,hint_level,progress_state,time_since_scene_start,attempt_count,wrong_action_count,repeated_question_count,solved
```

## 관련 코드

- `disputatio/Assets/godlotto/Script/DialogueLog/PlayLogCsvColumns.cs`
- `disputatio/Assets/godlotto/Script/DialogueLog/PlayLogCsvLogic.cs`
- `disputatio/Assets/godlotto/Script/DialogueLog/PlayLogRecorder.cs`
- `disputatio/Assets/godlotto/Script/DialogueLog/PlayLogSettings.cs`
- `disputatio/Assets/Editor/Tests/EditMode/UI/PlayLogCsvTests.cs`
