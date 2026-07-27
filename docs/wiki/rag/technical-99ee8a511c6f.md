---
source_id: technical:99ee8a511c6f
source_path: docs/security/llm-defense-play-test-guide.md
source_sha256: 99ee8a511c6f240a0115ea15331b69938b05e4e8fd31680da43385634be694e4
source_type: md
category: technical
title: llm-defense-play-test-guide
status: extracted
rag_eligible: true
---

# LLM 방어 기능 — 플레이 테스트 가이드

본 문서는 [llm-abuse-defense-plan.md](llm-abuse-defense-plan.md) Phase 1 이후 **실제 플레이(Unity + backend_ai)** 로 무엇을 어떻게 확인할지 정리합니다. 자동 테스트(`backend_ai/tests`)와 역할이 겹치는 부분은 “스모크” 수준만 다룹니다.

---

## 1. 무엇을 검증하는가

| 구분 | 플레이에서 바로 보이는지 | 플레이로 확인할 것 |
|------|--------------------------|---------------------|
| 채널 분리(system vs 봉투) | 대부분 안 보임 | 대사·툴이 정상, 튜터 RAG 없는 환각이 줄었는지 |
| 입력 길이·sanitize | 거의 안 보임 | 극단 입력 시 크래시/빈 응답 없음 |
| `max_tokens` 하드 캡 | 일부 경우 응답 잘림 | 긴 출력 요구 시 적절히 잘리거나 재시도 |
| IP / user rate limit | 429 또는 “한도” 메시지 | 연속 채팅 스팸 시 제한 동작 |

“프롬프트 인젝션에 완전히 안전하다”는 플레이 테스트만으로는 증명할 수 없습니다. 목표는 **회귀(기능 깨짐 없음)** + **남용 시 완충 동작 확인**입니다.

---

## 2. 사전 준비

1. **API 키**
   - `backend_ai/.env`에 `GROQ_API_KEY`, `GOOGLE_API_KEY` 설정 ([backend_ai/README.md](../../backend_ai/README.md) 참고).

2. **백엔드 실행** (저장소 루트가 아니라 `backend_ai` 기준으로 실행하는 것을 권장)

   ```bash
   cd backend_ai
   pip install -r requirements.txt
   uvicorn main:app --host 0.0.0.0 --port 8000
   ```

3. **헬스 확인**

   ```bash
   curl -s http://127.0.0.1:8000/
   ```

   `"status":"online"` 등이 나오면 OK.

4. **Unity 서버 URL**
   - `BaseChatbot` 등에 `local Server Url` 필드가 있으면 우선 적용되고, 비어 있으면 `ServerConfig`(에셋 `ServerConfig.cs` 기본값)의 `ChatUrl`이 사용됩니다.
   - **로컬 PC에서 에디터 플레이** 시 예시: `http://127.0.0.1:8000/chat`
     (반드시 `/chat`까지 포함해야 합니다.)

5. **(선택) Redis**
   - `REDIS_URL`을 두면 레이트 리밋이 프로세스 간 공유됩니다. 없으면 **단일 uvicorn 프로세스** 에서만 인메모리 제한이 동작합니다. 멀티 인스턴스·도커 스케일을 쓸 때는 Redis를 두고 재검증하세요.

6. **(선택) Android 에뮬레이터**
   호스트의 `localhost` 대신 에뮬레이터에서는 보통 `http://10.0.2.2:8000/chat` 을 사용합니다.

---

## 3. 플레이 테스트 시나리오 (권장 순서)

### 3.1 스모크 — 일반 채팅(Chester 등)

1. 게임 시작 후 채팅 가능한 장면까지 진행합니다.
2. 짧은 한 줄 입력 → 대사가 재생되고, 오류 문자열이 뜨지 않는지 확인합니다.
3. **힌트/이모션 툴**이 쓰이는 장면이라면 객체 하이라이트·감정 연출 등이 과거 빌드와 동일하게 오는지 봅니다.

**실패 시**: Unity 콘솔 네트워크 오류, 백엔드 터미널 스택 트레이스, `ServerConfig` / `localServerUrl` 오타를 우선 확인합니다.

### 3.2 스모크 — 스트리밍

클라이언트는 `POST /chat/stream` 을 사용합니다. 스트리밍 중:

- 문장이 조각나서 들어와도 최종 대사가 자연스럽게 이어지는지
- 중간에 끊기면 플레이어에게 보이는 한국어 메시지가 무엇인지(“연결 오류”, “한도” 등)

### 3.3 튜터 룸(RAG + quiz_bank)

1. 튜터 씬에서 질문이 나오는지, `current_question_id`·RAG가 붙는 흐름이 기존과 같은지 확인합니다.
2. **정답/오답** 각각 한 번씩: `update_quiz`에 따른 Fungus·UI 진행이 맞는지 봅니다.
3. `POST /tutor/grade`만 쓰는 경로(별도 UI)가 있다면, LLM 없이도 채점이 되는지 backend_ai README의 설명대로 확인합니다.

### 3.4 입력·남용(수동)

다음은 **한 세션당 1~2번**이면 충분합니다. 기록만 남기면 됩니다.

| 시도 | 기대 |
|------|------|
| 빈 입력 / 공백만 | 전송되지 않거나 “입력해 주세요”류 |
| 매우 긴 붙여넣기 (수천 자) | 크래시 없음; 응답 지연 또는 잘린 입력으로 처리 |
| 동일 문장 반복 전송 | 정상 플레이면 OK; 과도하면 429 또는 “한도” 안내 |

### 3.5 레이트 리밋(옵션, curl)

Unity 없이 빠르게 보려면 (같은 IP로 연속 호출):

```bash
for i in $(seq 1 70); do
  curl -s -o /dev/null -w "%{http_code}\n" \
    -X POST http://127.0.0.1:8000/chat \
    -H "Content-Type: application/json" \
    -d '{"prompt":"ping","system":"test","use_tools":false}'
done
```

일정 횟수 이후 **HTTP 429**와 `Retry-After` 헤더가 나오면 [config의 IP/유저 분당 한도](../../backend_ai/config.py)가 동작하는 것입니다.
`user_id`가 JSON에 들어가면 추가 버킷이 적용됩니다(해시 키).

---

## 4. 결과 기록 템플릿 (복사해서 사용)

```text
날짜:
빌드/커밋:
백엔드: 로컬 uvicorn / Docker / 스테이징 URL
Redis: 있음( URL 일부만 ) / 없음

[ ] GET / 헬스
[ ] 일반 채팅 1회
[ ] 스트리밍 대사 끊김 없음
[ ] 튜터 정답·오답 각 1회
[ ] 긴 입력 붙여넣기 — 크래시 없음
[ ] (선택) curl 연속 호출 — 429 확인

이슈:
```

---

## 5. 알려진 한계

- 인메모리 레이트 리밋은 **워커가 여러 개**이면 인스턴스마다 따로 잡힙니다. 운영형 검증은 `REDIS_URL` 기준으로 다시 하세요.
- 시스템 프롬프트·RAG가 `user` 측 마크업으로 이동했기 때문에, **모델·프로바이더 버전**에 따라 말투가 미세하게 달라질 수 있습니다. 기획 승인이 필요하면 동일 시드·동일 질문으로 Groq/Gemini 스냅샷을 비교하세요.

---

## 6. 관련 문서·코드

- 방어 설계 전체: [llm-abuse-defense-plan.md](llm-abuse-defense-plan.md)
- 백엔드 실행·API: [backend_ai/README.md](../../backend_ai/README.md)
- 자동 회귀: `backend_ai/tests/`, `backend_ai/tests/security/injection_corpus.yaml`
