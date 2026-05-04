# newCapstone AI 백엔드 서버 프로젝트

## 프로젝트 개요

- **목적**: Unity 게임 `newCapstone`의 AI 챗봇용 HTTP API.
- **AI 엔진**: Groq(우선), Gemini(폴백).
- **프레임워크**: FastAPI, uvicorn.

## 새 PC / 클론 직후 필수 설정 (API 키)

API 키는 **Git에 올리지 않습니다**. 다른 컴퓨터에서는 반드시 아래를 한 번 실행하세요.

```bash
cd backend_ai
copy .env.example .env
   # macOS/Linux: cp .env.example .env
```

`.env` 파일을 열어 `GROQ_API_KEY`, `GOOGLE_API_KEY` 를 입력합니다.  
(레거시로 `capstone` 이름만 쓰는 경우도 `.env.example` 주석 참고.)

서버 실행:

```bash
pip install -r requirements.txt
uvicorn main:app --host 0.0.0.0 --port 8000
```

`config`는 **`backend_ai` 폴더의 `.env`** 를 자동으로 읽습니다 (작업 디렉터리와 무관).

## 운영·Docker·EC2

자세한 내용은 [DEPLOY.md](DEPLOY.md) 를 참고하세요.

## 환경 변수

| 변수 | 설명 |
|------|------|
| `GROQ_API_KEY` | Groq API 키 (권장) |
| `GOOGLE_API_KEY` | Google AI Studio (Gemini) 폴백 |
| `capstone` | 예전 Groq 키 변수명 (`GROQ_API_KEY` 가 비었을 때만 사용) |

## API

- **헬스**: `GET /`
- **채팅**: `POST /chat` — JSON `{ "prompt": "...", "system": "...", "use_tools": true }`
- **스트리밍**: `POST /chat/stream`
- **튜터 채점(LLM 없음)**: `POST /tutor/grade` — JSON `{ "question_id": "Q002", "user_answer": "골리앗", "correct_count_before": 1, "quiz_target": 5 }` → `is_correct`, `reference_snippet`, `quiz_complete_after`, `unknown_question`

### 튜터 룸 (RAG·문제 은행)

요청 JSON에 `rag_profile` 이 `"tutor"` 이면 서버가 참고 자료를 시스템 프롬프트에 합칩니다.

| 필드 | 설명 |
|------|------|
| `rag_profile` | `"tutor"` 일 때만 RAG·은행 블록 주입 |
| `rag_query` | 생략 시 `prompt` 로 검색 쿼리 생성 |
| `current_question_id` | `quiz_bank.csv` 의 `question_id` — 있으면 해당 행 질문·참고를 주입하고, CSV 기준으로 정답이면 `update_quiz.is_correct` 를 보정 |
| `rag_top_k` | 선택, 기본은 설정값 |

환경 변수·설정(`config.py`): `tutor_rag_*`, `tutor_quiz_csv_path`, `tutor_grade_fuzzy_*` 등.

### 기획자용: 문제 넣기

1. **`data/tutor_quiz/quiz_bank.csv`** 를 엑셀/구글 시트에서 UTF-8 CSV로 편집합니다.  
   필수 열: `question_id`, `question_ko`, `acceptable_answers`, `reference_snippet`  
   `acceptable_answers` 는 `|` 로 여러 정답 표현을 구분합니다.
2. 검증: `python scripts/validate_quiz_bank.py`
3. 긴 해설·원문은 **`data/tutor_rag/*.md`** (또는 `.txt`)에 추가합니다.
4. 임베딩 인덱스 재생성 (**`GOOGLE_API_KEY` 필요**):

   ```bash
   cd backend_ai
   python scripts/build_tutor_rag_index.py
   ```

   산출물: `data/tutor_rag_index.json` (커밋하거나 배포 시 생성)

Unity `Resources/TutorQuestionOrder.txt` 에 출제 순서대로 `question_id` 를 한 줄에 하나씩 적어 두면, 클라이언트가 `current_question_id` 를 자동으로 붙입니다.

Unity 클라이언트의 `BaseChatbot.localServerUrl` 은 배포한 서버 주소로 맞춥니다.

## 파일 구조

- `main.py` — FastAPI 앱
- `config.py` — 설정·`.env` 로드
- `Dockerfile` — 컨테이너 빌드
- `.env.example` — 키 이름 템플릿 (커밋됨)
- `.env` — 실제 키 (커밋 금지, `.gitignore`)
- `data/tutor_rag/` — RAG용 텍스트 코퍼스
- `data/tutor_quiz/quiz_bank.csv` — 비개발자 편집용 문제 은행
- `data/tutor_rag_index.json` — 임베딩 인덱스 (`build_tutor_rag_index.py`로 생성)
- `scripts/build_tutor_rag_index.py`, `scripts/validate_quiz_bank.py`
- `services/tutor_rag_service.py`, `services/quiz_bank.py`, `services/answer_grader.py`, `services/quiz_validation.py`

<!-- Touched to validate backend-build workflow path filters in CI. -->
