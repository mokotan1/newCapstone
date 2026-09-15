# SettingScene 체셔 AI 탭 미리보기 채팅 연동

작성일: 2026-09-14. 상태: 사용자 승인(`ㄱ`). CPU/GPU/AUTO 장치 전환은 범위 밖.

## 목표

체셔 AI 탭의 미리보기 질문이 인게임과 같은 `ChatHttpClient` `/chat/stream`으로 답을 받는다. 히스토리는 미리보기 전용이며 게임 진행·방 프롬프트·툴을 쓰지 않는다. 루프백이면 `GET /` `local_runtime.model_available`이 참일 때만 전송한다.

## 동작

- UI는 HTTP를 직접 치지 않는다. `ChatHttpClient.FetchRootStatus` / `GetGPTResponseStreaming`.
- 시스템 프롬프트: `BaseSystem` + 보이스 공통 규칙. `use_tools=false`.
- 질문마다 `ChatHistoryManager.Initialize()`.
- 상태 키: 비활성 `LocalAiDisabled`, 대기 `AiSettingsConnecting`, 준비 `SettingsPreviewReady`, 전송 거부 `LocalAiNotReady`.
- 장치 버튼은 비활성 유지. 스트립 상태 문구만 미리보기 폴링과 맞춘다.
- 미구현 지연시간 메트릭(금색 `—`)은 숨긴다.

## 비범위

CPU/GPU/AUTO 적용 API, 답변 지연 ms, 인게임 SayDialog.
