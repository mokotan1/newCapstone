# newCapstone

캡스톤용 저장소입니다. **2D Unity 게임 클라이언트**와 **AI 대화용 HTTP 백엔드**로 구성되어 있습니다.

## Project wiki

- **[docs/wiki/Home.md](docs/wiki/Home.md)** — curated project knowledge index
- **[docs/wiki/OPERATIONS.md](docs/wiki/OPERATIONS.md)** — inventory, validation, RAG, and CI runbook

## 구성

```
newCapstone/
├── disputatio/      # Unity 6 프로젝트 (게임: 민원 번호 33)
├── backend_ai/      # FastAPI AI 챗봇 서버 (Groq / Gemini)
└── scripts/         # 보조 스크립트 (예: C# 구문 검사, 오류 수집)
```

## Unity 클라이언트 (`disputatio/`)

| 항목 | 내용 |
|------|------|
| **Unity 버전** | **6000.0.36f1** (Unity 6) |
| **제품명** | 민원 번호 33 |
| **렌더 파이프라인** | URP (Universal Render Pipeline) |

주요 패키지 예시:

- **Fungus** — 대화·시퀀싱
- **Cinemachine** — 카메라
- **Input System** — 입력
- **AI Navigation**, **NavMeshPlus** — 2D 내비게이션
- **Post Processing**, **Timeline**, **Newtonsoft.Json** (UPM)

### 실행 방법

1. [Unity Hub](https://unity.com/download)에서 에디터 **6000.0.36f1**을 설치합니다.
2. Hub에서 **Add**로 `disputatio` 폴더를 프로젝트로 추가한 뒤 엽니다.
3. `Assets/Scenes` 아래 씬을 열고 **Play**로 실행합니다.

### AI 서버 연동

챗봇은 `BaseChatbot`의 `localServerUrl`로 백엔드 `POST /chat` 엔드포인트를 호출합니다. 로컬 개발 시에는 `backend_ai`를 띄운 뒤 Inspector에서 URL을 `http://localhost:8000/chat` 등으로 맞춥니다.

## AI 백엔드 (`backend_ai/`)

Unity용 챗봇 API(FastAPI, Groq 우선·Gemini 폴백)입니다. 설치, 환경 변수, API 명세, Docker·배포는 아래 문서를 따릅니다.

- **[backend_ai/README.md](backend_ai/README.md)** — 로컬 실행·API 키·엔드포인트
- **[backend_ai/DEPLOY.md](backend_ai/DEPLOY.md)** — 운영·Docker·EC2

## 기타 (`scripts/`)

CI용 C# 구문 검사기 등 저장소 보조 도구가 있습니다. 각 폴더의 소스와 프로젝트 파일을 참고하세요.

## 라이선스·에셋

서드파티 에셋(Fungus, 아이콘·사운드 팩 등)은 각 `Assets` 하위의 원본 라이선스를 따릅니다.
