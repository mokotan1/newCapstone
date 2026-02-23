# newCapstone AI 백엔드 서버 프로젝트

## 🚀 프로젝트 개요
- **목적**: 유니티(Unity) 게임 `newCapstone`의 AI 챗봇 기능을 위한 외부 접속 가능 서버.
- **배포 플랫폼**: Render.com (Docker 기반)
- **AI 엔진**: 
    - **Primary**: Groq (Llama 3 70B) - `capstone` 환경변수 사용
    - **Fallback**: Gemini (1.5 Flash) - `GOOGLE_API_KEY` 환경변수 사용

## 🛠️ 기술 스택
- **Framework**: FastAPI (Python)
- **Deployment**: Docker, Render.com
- **Libraries**: `groq`, `google-generativeai`, `uvicorn`, `pydantic`

## 🔐 환경변수 설정 (Render.com Dashboard)
- `capstone`: Groq API 키 (스크린샷 기반 반영 완료)
- `GOOGLE_API_KEY`: Google AI Studio API 키

## 📁 파일 구조 (`projects/newCapstone/backend_ai/`)
- `main.py`: 서버 메인 로직 (하이브리드 엔진 전환 및 API 엔드포인트)
- `Dockerfile`: Render 배포용 이미지 설정
- `requirements.txt`: 의존성 라이브러리 목록
- `render.yaml`: Render 자동 배포 청사진

## 🔗 접속 정보
- **Endpoint**: `POST /chat`
- **Payload**: `{"prompt": "유저 입력", "system": "시스템 프롬프트"}`
- **Response**: `{"response": "AI 답변"}`

---
*최종 업데이트: 2026-02-23 (Slave)*
