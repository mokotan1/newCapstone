# local-llm-autostart

- Feature: 로컬 LLM 자동 실행 (스펙 `docs/superpowers/specs/2026-09-15-local-llm-autostart-design.md`)
- 한 줄 목표: Editor·Player가 공통 Supervisor로 전용 FastAPI를 켜고, 원격 URL·Groq/Gemini로 돌아가지 않는다.
- AC1 (관찰 가능): 세션 파일이 protocol/instance/session/parent 신원과 맞을 때만 재연결한다. 포트만·PID만 일치하면 거부한다.
- AC2 (실패/경계): Inspector/Resources 원격 URL은 세션이 없어도 cloud로 가지 않고 loopback을 쓴다. batchmode는 자동 시작하지 않는다. 클라우드 키가 있어도 LiteRT만 고른다.
- 제외(아직 미달): Python 없는 깨끗한 VM Player 패키지(AC05/AC14), 망차단(AC02), Gate 0 동결·품질 eval(AC16), Play/Stop×10(AC04), Job 강제종료 실측(AC07), qa-playtester(AC17), 독립 리뷰
- 허용 파일: `backend_ai/services/local_ai/**`, `backend_ai/services/local_embedding.py`, `backend_ai/services/tutor_rag_service.py`, `backend_ai/local_runtime.py`, `backend_ai/config.py`, `backend_ai/providers/**`, Unity Resolver/Bootstrap/`ServerConfig`/`ChatHttpClient`/`TutorQuizGrader`, 테스트, `docs/architecture.md`, 본 패킷
- 공유 자원: `ServerConfig.ChatUrl`, `BaseChatbot` URL 선택, architecture.md, CI wiki-rag/backend-build
- 선행: 이 checkout에 LiteRT/CUDA `services/local_ai`가 이미 있음. Gate 0 동결 파일: `docs/superpowers/specs/2026-09-15-local-llm-gate0-decision.md` (AC16 불가 기록)
- 검증 명령: `python -m pytest tests -q` (`backend_ai/`, 2026-09-15: 290 passed); Unity EditMode 필터는 QA report 참고
- Cursor 도구: 부모
- 위험도: R2
- independentReview: false
- 상태: blocked-on-live (코드 계약은 구현, 스펙 전량 pass 아님)
- 증거: `docs/qa/runs/20260915T014800Z-run-local-llm-autostart-ac/report.md`
