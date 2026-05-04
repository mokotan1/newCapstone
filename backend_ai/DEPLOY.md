# API 키와 배포 (팀·다른 PC·서버)

`.env` 는 **Git에 커밋하지 않습니다** (`.gitignore`). 그래서 저장소만 클론하면 **이 PC에만 있는 키**는 다른 환경에 자동으로 따라가지 않습니다. 아래 중 하나로 키를 넣으면 됩니다.

## 1. 로컬 / 다른 개발 PC

1. `backend_ai` 폴더로 이동
2. `.env.example` 을 복사해 `.env` 생성  
   - Windows: `copy .env.example .env`  
   - macOS/Linux: `cp .env.example .env`
3. `GROQ_API_KEY`, `GOOGLE_API_KEY` 등 필요한 값을 채움
4. 서버 실행: `uvicorn main:app --host 0.0.0.0 --port 8000` (또는 `python main.py`)

`config.py`는 **`backend_ai` 폴더 안의 `.env`** 를 실행 위치와 관계없이 읽습니다.

## 2. Docker

- **파일로 주입:** 위와 같이 `backend_ai/.env` 를 만든 뒤  
  `docker compose up --build -d`  
  (같은 폴더의 `docker-compose.yml` 사용)
- **환경 변수만:** 이미지에 키를 넣지 말고 실행 시 전달  
  `docker run -e GROQ_API_KEY=... -e GOOGLE_API_KEY=... -p 8000:8000 <이미지>`

## 3. EC2·클라우드 (운영)

- 인스턴스/컨테이너의 **환경 변수**에 `GROQ_API_KEY`, `GOOGLE_API_KEY` 설정  
  (AWS Systems Manager Parameter Store, Secrets Manager, 호스팅 패널의 Env 설정 등)
- `.env` 파일을 서버에만 두고 권한 제한 (선택)

## 환경 변수 이름 (요약)

| 이름 | 용도 |
|------|------|
| `GROQ_API_KEY` | Groq (우선) |
| `GOOGLE_API_KEY` | Gemini 폴백 |
| `capstone` | 레거시 Groq 키 (`GROQ_API_KEY` 가 비었을 때만) |

키는 **절대** 저장소에 넣지 말고, 위 경로로만 배포하세요.

## 4. GitHub Container Registry 에서 이미지 받기

CI 가 main 에 머지될 때마다 다음 두 태그로 이미지를 푸시합니다:

- `ghcr.io/<owner>/newcapstone-ai:<commit-sha>` — 특정 커밋의 재현 가능한 빌드
- `ghcr.io/<owner>/newcapstone-ai:latest` — main 의 최신 이미지

사용 예:

```bash
docker pull ghcr.io/<owner>/newcapstone-ai:latest
docker run -e GROQ_API_KEY=... -e GOOGLE_API_KEY=... -p 8000:8000 \
  ghcr.io/<owner>/newcapstone-ai:latest
```

`<owner>` 는 이 저장소 owner 의 GitHub 사용자/조직명입니다.
