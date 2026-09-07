# newCapstone 아키텍처 문서

> **목적**: Cursor/AI가 새 기능을 추가할 때 따를 **코드베이스 기준 문서**입니다.  
> **원칙**: 이 문서는 저장소를 직접 조사한 내용만 기록합니다. 추측·일반론은 §8(미확인 사항)으로 분리합니다.

---

## 1. 프로젝트 개요

### 이 앱이 무엇을 하는지

**newCapstone**은 2D 미스터리·스릴러 **Unity 게임**과, 게임 내 AI 챗봇(체셔 앵무·튜터 등)을 위한 **HTTP 백엔드**로 구성된 모노레포입니다.

| 구성요소 | 역할 |
|----------|------|
| `disputatio/` | Unity 6 클라이언트. 저택 탐색, Fungus 대화/시퀀스, 퍼즐, 인벤토리, AI 챗 UI |
| `backend_ai/` | FastAPI AI 서버. `/chat`, `/chat/stream`, `/tutor/grade` 제공 |
| `scripts/` | CI용 C# 구문 검사기, 오류 수집 스크립트 등 |
| `deploy/` | 운영 Docker Compose, Caddy, 배포 후 헬스체크 스크립트 |

게임 제품명(Inspector): **The Unholy of Mention** (`disputatio/ProjectSettings/ProjectSettings.asset`).  
README에는 **민원 번호 33**으로도 표기되어 있습니다.

플레이어는 씬(방·복도) 단위로 이동하며, **Fungus Flowchart**로 대사·연출을 진행하고, **C# 상호작용 레이어**(`Godlotto.Interaction`)가 클릭·씬 전환·패널을 조율합니다. **체셔 대사**는 Unity `UnityWebRequest` → `POST /chat/stream`(실패 시 `POST /chat`)로 처리되며 **텍스트만** 반환한다. 게임 툴(`give_hint`, `emote`, `update_quiz`)은 **튜터 `rag_profile=tutor` 경로에만** 주입되고, 퀴즈 정오는 `/tutor/grade` CSV 채점이 담당한다.

### 주요 기술 스택

| 영역 | 스택 (코드·설정에서 확인) |
|------|---------------------------|
| 게임 엔진 | Unity **6000.0.36f1** (Unity 6), URP (`com.unity.render-pipelines.universal` 17.0.3) |
| 대화·시퀀싱 | **Fungus** (`disputatio/Assets/Fungus/`) |
| 입력 | Input System (`com.unity.inputsystem`) |
| JSON | Newtonsoft (`com.unity.nuget.newtonsoft-json`) |
| AI 클라이언트 | `BaseChatbot` → `ChatHttpClient` (`disputatio/Assets/mokotan/mokotan/script/AI/`) |
| AI 서버 | **FastAPI** + uvicorn, **Groq**(우선) / **Gemini**(폴백); `AI_PROVIDER=local`이면 LiteRT-LM Gemma 4 E2B |
| 클라이언트 영속화 | **PlayerPrefs** (체크포인트 JSON, 설정), Fungus 변수(`Variablemanager` Flowchart) |
| 서버 데이터 | CSV 문제은행, RAG JSON 인덱스, (선택) **Redis** rate limit |
| CI | GitHub Actions: Python ruff/py_compile, C# syntax checker, backend pytest + Docker |
| 배포 | GHCR 이미지 → EC2 SSH + `docker compose` (`deploy/docker-compose.prod.yml`) |

---

## 2. 디렉터리 구조

### 저장소 루트

```
newCapstone/
├── disputatio/          # Unity 프로젝트 (게임 본체)
├── backend_ai/          # FastAPI AI 백엔드
├── scripts/             # CI·로컬 보조 도구 (CSharpSyntaxChecker, install_local_ai.ps1, qa/autorun 등)
├── installer/           # 로컬 AI 라이선스 NOTICE·첫 실행 체크리스트
├── deploy/              # 운영 compose, Caddy, postdeploy 스크립트
├── docs/                # 기획·마이그레이션·본 아키텍처 문서
├── .github/workflows/   # CI/CD
└── README.md
```

### Unity (`disputatio/Assets/`) — 코드를 넣을 위치

| 경로 | 책임 | 새 코드 추가 시 |
|------|------|-----------------|
| `Assets/godlotto/Script/` | **팀 핵심 게임 로직**: 인벤토리, 체크포인트, 설정, 씬 네비, Fungus 커스텀 커맨드 | 대부분의 게임play·UI·세이브 기능 |
| `Assets/godlotto/Script/Interaction/` | **씬 상호작용 프레임워크** (`Godlotto.Interaction`) | 방/복도 클릭, Fungus 블록 실행, 씬 전환 outcome |
| `Assets/godlotto/Script/Checkpoint/` | PlayerPrefs 체크포인트 저장·복원 | 이어하기, 방 해금 스냅샷 |
| `Assets/godlotto/Script/Constants/` | `SceneNames`, `FungusVariableKeys` | 씬·변수 이름 상수 (매직 스트링 금지) |
| `Assets/godlotto/Script/Quest/` | `QuestTrackerState`, `TutorialQuestProgressAdapter`, `TutorialQuestGameBridge` | 튜토리얼 퀘스트 HUD·월드 이벤트 브리지 |
| `Assets/godlotto/Script/Core/` | `SingletonMonoBehaviour`, `GameLog` | 씬 간 유지 싱글톤, dev 로그 |
| `Assets/godlotto/Script/Config/` | `ServerConfig` ScriptableObject | AI 서버 URL 기본값 |
| `Assets/godlotto/Script/Editor/` | 씬 마이그레이션·에디터 도구 | Fungus→C# 마이그레이션, 씬 일괄 수정 |
| `Assets/godlotto/KTH/` | 점프스care, 이펙트, 실험적 씬 스크립트 | 공포 연출·특수 씬 |
| `Assets/mokotan/mokotan/script/AI/` | **AI 챗봇** (`BaseChatbot` 파생) | 방별 챗봇, HTTP, 휴리스틱 |
| `Assets/mokotan/mokotan/script/AI/Heuristics/` | 힌트 난이도·재방문 추적 | AI 컨텍스트 신호 |
| `Assets/mokotan/MapSP/scr/` | 미니맵·씬 이동 UI | 맵에서 방 이동 |
| `Assets/Scenes/` | **모든 플레이 씬** | 씬 에셋·Flowchart 배치 (로직은 Script에) |
| `Assets/Editor/Tests/EditMode/` | **EditMode 단위 테스트** | 순수 C# 로직 테스트 |
| `Assets/Fungus/` | 서드파티 Fungus (수정 최소화) | Fungus 코어 변경 지양 |
| `Assets/Resources/` | `ServerConfig`, `CheshirePrompts/{ko,ja,en}/`, `QA/Scenarios/*.json` | 런타임 `Resources.Load` 대상; DeveloperQa 시나리오 JSON |
| `Assets/mokotan/.../script/QA/Developer/` | `DeveloperQaService`, scenario runner (`scenario.run\|resume\|cancel\|status`) | Editor/dev-only Developer Mode QA 계약 |
| `Assets/mokotan/.../AI/Localization/` | `CheshireLocaleResolver`, `CheshirePromptCatalog`, fragment helpers | Fungus 언어 → `ko`\|`ja`\|`en`, 프롬프트 카탈로그 |

### 백엔드 (`backend_ai/`)

| 경로 | 책임 |
|------|------|
| `main.py` | FastAPI 앱, 라우트, lifespan(rate limiter) |
| `config.py` | `.env` 로드, 한도·튜터 RAG 설정 |
| `models/` | `ChatRequest`, `ChatResponse`, `TutorGradeRequest` |
| `providers/` | Groq / Gemini / LiteRT(로컬 Gemma 4 E2B) 어댑터 |
| `services/` | `chat_service`, `dialogue_guard`, `sse_format`, RAG, quiz bank, `locale_support`, rate limit |
| `local_runtime.py` | `AI_PROVIDER=local`일 때 LiteRT 프로바이더 선택·루프백 헬스 |
| `local_install.py` | 동의 후 Gemma 4 E2B import·체크섬 검증 (설치 플래너) |
| `scripts/` | `validate_quiz_bank.py`, `validate_cheshire_prompts.py`, `build_tutor_rag_index.py` |
| `tools/` | LLM function schema (`game_tools.py`) |
| `llm_defense/` | 입력 sanitize, message builder |
| `data/tutor_quiz/` | `quiz_bank.csv` (KO/JA/EN 질문·정답·스니펫; 빈 셀은 KO 폴백) |
| `data/tutor_rag/` | RAG 코퍼스 md/txt |
| `tests/` | pytest |
| `tests/evals/` | 체셔 대화 JSONL 스위트·스코어러·로컬 전용 러너 (`run_cheshire_eval.py`) |

### 네임스페이스 규칙 (실제 코드)

- **`Godlotto.Interaction`**: `RoomInteractionController`, `SceneTransitionService`, `SceneInteractionController` 등
- **`Godlotto.FungusIntegration`**: `GuardedClickable2D` 등
- **그 외 godlotto/mokotan 대부분**: 전역 네임스페이스 (클래스명만으로 참조)

---

## 3. 실행 흐름

### 앱 시작점

1. **빌드 첫 씬**: `Assets/Scenes/godlotto/MainMenuScene.unity`  
   (`disputatio/ProjectSettings/EditorBuildSettings.asset` index 0)
2. **`MainMenu`** (`disputatio/Assets/godlotto/Script/MainMenu.cs`):
   - **새 게임**: `PlayDataPrefsCleaner.ClearProgressPreserveAudioVideoSettings()` — 진행만 초기화, BGM/SFX/해상도 PlayerPrefs 유지
   - **이어하기**: `CheckpointLoadCoordinator.LoadLatestOrFallback(SceneNames.MainScene)`
   - 실제 **새 게임 씬 전환**은 Inspector에서 버튼→Fungus 블록 연결로 처리 (`MainMenu.OnStartButton`은 PlayerPrefs 정리만 수행)
3. **씬 로드 시 공통**:
   - `VariablemanagerSingleton` — `DontDestroyOnLoad`로 전역 Flowchart 오브젝트 유지
   - `SceneNameSetter` — Fungus `SceneName`, `SavePointKey` 갱신
   - `InventoryManager` — `PersistAcrossScenes == true` 싱글톤

### 씬/“라우트” 흐름 (웹 라우트 없음 → Unity SceneManager)

게임에는 URL 라우팅이 없습니다. **씬 이름 = 라우트**입니다. 상수는 `SceneNames`에 정의합니다.

```csharp
// disputatio/Assets/godlotto/Script/Constants/SceneNames.cs
public const string MainMenu = "MainMenuScene";
public const string Kitchen = "Kitchen";
public const string StudyRoom = "StudyRoom";
// ...
```

**대표 플로우 (빌드 설정·코드 기준)**

```mermaid
flowchart TD
    MM[MainMenuScene] -->|새 게임 Fungus| Intro[IntroScene / Opening_*]
    MM -->|이어하기| CP[CheckpointLoadCoordinator]
    CP --> Resume[resumeSceneName 씬]
    Intro --> Game[Mokotan 1F/2F/Basement 씬들]
    Game -->|RoomInteractionController outcome| ST[SceneTransitionService.LoadSceneSafely]
    Game -->|BackNavigator| Back[고정 복귀 또는 PrevScene 변수]
    ST --> Game
    Back --> Game
```

**씬 전환 진입점 (우선순위)**

| 메커니즘 | 파일 | 용도 |
|----------|------|------|
| `SceneTransitionService.LoadSceneSafely` | `godlotto/Script/Interaction/SceneTransitionService.cs` | 중복 LoadScene 방지 (권장) |
| `RoomInteractionController` BlockOutcome | `godlotto/Script/Interaction/RoomInteractionController.cs` | Fungus 블록 종료 후 `openPanel` / `resetIsClicked` / loadScene / goBack |
| `BackNavigator.GoBack` | `godlotto/Script/BackNavigator.cs` | 고정 복귀 테이블 또는 `PrevScene` Fungus 변수 |
| `SceneManager.LoadScene` (직접) | 여러 레거시·맵 UI | 점진적으로 Interaction 레이어로 이전 중 |

**복귀 고정 테이블 예** (`BackNavigator.TryResolveFixedReturnScene`):

- `StudyRoom` / `MaidRoom` → `Hallway_Right`
- `BedRoom` / `WifeRoom` → `2floorHallway_Right`
- `TutorRoom` / `ChildRoom` → `2floorHallway_Left`
- `2floorMainHall` → `Hall_playerble`

### 클라이언트 ↔ 서버 관계

```mermaid
sequenceDiagram
    participant UI as BaseChatbot / TMP Input
    participant CH as ChatHttpClient
    participant SC as ServerConfig
    participant API as backend_ai FastAPI
    participant LLM as LiteRT Gemma 4 E2B / Groq / Gemini

    UI->>CH: GetGPTResponseStreaming (체셔 전송)
    CH->>SC: Resolve ChatUrl (또는 Inspector override)
    CH->>API: POST /chat/stream JSON LocalLlamaPayload
    API->>LLM: chat (체셔는 tools off)
    LLM-->>API: SSEEvent text_delta / done
    API-->>CH: SSE data JSON frames
    Note over CH: ChatSseStreamParser 줄 버퍼. 스트림 실패 시 POST /chat 폴백
    CH->>UI: HandleChatbotResponse
    UI->>UI: SayDialog
```

- **URL 해석**: `BaseChatbot.Start()` → Inspector `localServerUrl`이 비어 있으면 `ServerConfig.GetOrCreate().ChatUrl`.  
  `ServerConfig`는 **이중 모드**다. `UseLocalLoopback`(기본 true)이면 항상 `http://127.0.0.1:8000/chat`. 끄면 직렬화된 클라우드 URL(`DefaultCloudChatUrl`, `http://54.156.51.119:8000/chat`)을 쓴다. 챗봇 Inspector URL이 있으면 ServerConfig를 우회한다. 저장소에는 `Resources/ServerConfig.asset`이 없어 `GetOrCreate` 폴백은 런타임 기본값(루프백 켜짐)이다.
- **튜터 채점**: `TutorQuizGrader`가 `/chat` URL에서 `/tutor/grade`로 치환해 `POST` (LLM 없이 CSV 채점). 본문에 Fungus 언어에서 해석한 `locale`(`ko`|`ja`|`en`)을 포함.
- **플레이어 locale**: `CheshireLocaleResolver`가 Fungus 언어 설정을 `ko`|`ja`|`en`으로 정규화. `ChatHttpClient`가 `/chat`·`/chat/stream` payload에 `locale`을 실어 보내고, 서버 `ChatRequest.locale` / `TutorGradeRequest.locale`이 동일 규칙으로 정규화한다. 동일 authority가 **시나리오 standing dialogue CSV**(`PlayScenarioBlockCommand` → `ScenarioLocalizationTable`)와 **Cheshire UI 문자열 CSV**(`Resources/Scenario/cheshire_ui_strings.csv`, `CheshireUiStrings`)에도 적용된다 (header `id|line_id|string_id,ko,en,ja`, 빈 셀은 KO 폴백).

---

## 4. 데이터 흐름

### 영속화 계층 (DB 없음 — 클라이언트)

| 저장소 | 키/형식 | 담당 코드 | 내용 |
|--------|---------|-----------|------|
| PlayerPrefs | `Checkpoint.Latest.v1` | `CheckpointRepository` | JSON `CheckpointSaveData` |
| PlayerPrefs | `BGMVolume`, `SFXVolume`, `Fullscreen`, `ResolutionIndex` | `SettingPlayerPrefsKeys` | 설정 (진행 초기화 시 **유지**) |
| Fungus Flowchart | `Variablemanager` GameObject | `FlowchartLocator`, `ProgressSnapshotCollector` | bool/int/string 게임 상태 |
| Fungus SaveManager | `persistentDataPath/FungusSaves/` | Fungus 내장 | 에디터에서 `PlayDataPrefsCleaner`가 삭제 가능 |
| ScriptableObject | `Assets/godlotto/Item/*.asset` | `Item` | 인벤토리 아이템 정의 |

**체크포인트 CRUD**

```mermaid
flowchart LR
    Trigger[RoomUnlockCheckpointTrigger.SaveCheckpoint] --> Svc[RoomUnlockCheckpointService]
    Svc --> Col[ProgressSnapshotCollector.Populate]
    Col --> Inv[InventoryManager.Instance]
    Col --> FC[FlowchartLocator.Find]
    Svc --> Repo[CheckpointRepository.Save PlayerPrefs]
    Load[MainMenu OnLoad / Jumpscare retry] --> Coord[CheckpointLoadCoordinator]
    Coord --> Repo
    Coord --> Apply[ProgressSnapshotApplier.Apply]
```

**`CheckpointSaveData` 필드** (`godlotto/Script/Checkpoint/CheckpointSaveData.cs`):

- `resumeSceneName`, `checkpointId`, `checkpointType`, `unlockedRoomKey`
- `itemIds[]`, `fungusBooleans[]`, `fungusIntegers[]`, `fungusStrings[]`

**방 해금 체크포인트 정의** (`RoomCheckpointDefinition.cs`):  
`ElectricOn`→Kitchen, `UsedStudyKey`→StudyRoom, … `UsedBedKey`→BedRoom (Order 10~70)

### 런타임 상태 (세션)

| 상태 | 위치 | 비고 |
|------|------|------|
| 대화·플래그 | Fungus `Variablemanager` | `FungusVariableKeys.*` 상수로 접근 |
| 인벤토리 슬롯 | `InventoryManager` | `DontDestroyOnLoad` |
| AI 대화 기록 | `ChatHistoryManager` | `BaseChatbot` 인스턴스별 |
| 상호작용 차단 | `InteractionInputGate`, `SceneInteractionController` | 대사 중·씬 전환 중 클릭 차단 |
| 씬 전환 플래그 | `SceneTransitionService` static | `IsTransitionPending` |

### API 호출 구조 (백엔드)

| 메서드 | 경로 | 요청 모델 | 응답 |
|--------|------|-----------|------|
| GET | `/` | — | `{ status, message }` (`AI_PROVIDER=local`이면 `local_runtime` 상태 포함) |
| POST | `/chat` | `ChatRequest` | `ChatResponse` (text + function_calls) |
| POST | `/chat/stream` | `ChatRequest` | SSE |
| POST | `/tutor/grade` | `TutorGradeRequest` | `TutorGradeResponse` (LLM 없음) |

**Unity → `/chat` JSON** (`ChatHttpClient.LocalLlamaPayload`):

- `prompt`, `system`, `use_tools`, `message`, `user_id`, `locale` (`ko`|`ja`|`en`)
- 선택: `character_facts`, `dialogue_context` (체셔 대화 전용 사실/장면 컨텍스트)
- 튜터: `rag_profile`, `rag_query`, `current_question_id`

**체셔 대화 계약**: `rag_profile != "tutor"`이면 서버가 `use_tools`를 무시하고 게임 툴 레지스트리(`give_hint`/`emote`/`update_quiz`)를 주입하지 않는다. Unity `ChatHttpClient.ResolveUseTools`도 동일 규칙을 적용한다. 퀴즈 정오는 `/tutor/grade` CSV 채점이 담당한다.

**체셔 샘플링·가드**: 대화 전용 요청은 `dialogue_temperature=0.8`, `dialogue_max_tokens=64`, LiteRT `top_p=0.95` / `top_k=64`, `local_ai_num_ctx=2048`(지연 예산; 설계 초안 4096보다 짧음). `text_delta`는 생성되는 대로 SSE로 흘린다. `done.full_text`만 가드가 빈/JSON/3문장 초과를 로케일 폴백으로 치환하고 function_call은 버린다. 완료 목표 약 3초(로컬 E2B). 튜터 경로는 가드하지 않는다.

**서버 function tools** (`backend_ai/tools/game_tools.py`, 튜터 전용):

- `give_hint` — Unity에서 오브젝트 강조 등
- `emote` — 앵무 감정
- `update_quiz` — TutorRoom 퀴즈 진행

**인증**: 클라이언트→서버 **사용자 로그인/토큰 없음**. 서버는 API 키(`GROQ_API_KEY`, `GOOGLE_API_KEY`)를 `.env`에서만 읽고, `rate_guard`로 IP/`user_id` 해시 rate limit (429).

---

## 5. 주요 모듈 설명

### Interaction 레이어 (`Godlotto.Interaction`)

**역할**: Fungus는 **대사·Say·Menu**만; 클릭 라우팅·씬 load·`isClicked` 정리는 C#.

| 클래스 | 역할 |
|--------|------|
| `SceneInteractionController` | `TryInteract(id)` — 연타·대사 중·전환 중 차단 |
| `RoomInteractionController` | `interactionId` → Fungus block; `BlockOutcome` → 씬/load/back |
| `CorridorEntranceController` | 복도·입구 씬용 `RoomInteractionController` 파생 |
| `FungusDialogueBridge` | Flowchart 블록 안전 실행 |
| `SceneTransitionService` | LoadScene 중복 방지 |
| `InteractionInputGate` | 시퀀스 중 입력 전역 차단 |
| `ClickInteractionCleanup` | `isClicked` / UI 경계 후 정리 |

**의존**: Fungus `Flowchart`, `BlockSignals` ← C# controller ← UI/월드 Collider2D

### AI 챗 (`mokotan/.../AI/`)

| 클래스 | 역할 |
|--------|------|
| `BaseChatbot` | SayDialog, 입력, HTTP coroutine 진입; locale별 system prompt 조립. 체셔 플레이어 전송은 `GetGPTResponseStreaming`. `text_delta`는 `CheshireLiveStreamDisplay`로 SayDialog `StoryText`에 바로 붙임 |
| `ChatHttpClient` | `/chat`, `/chat/stream` transport; payload `locale`; `ChatSseStreamParser`로 chunk-safe SSE; 델타마다 `OnStreamTextDelta`; 스트림 재시도 소진 시 `/chat` 폴백; loopback이면 `GET /` 헬스 폴링 |
| `LocalAiReadiness` | `127.0.0.1`/`localhost` 채팅 URL만 로컬 모델 준비 여부를 강제. PlayerPrefs `LocalAi.ChatDisabled` 로 대화 AI만 끄기 |
| `ChatSseStreamParser` | Unity download-buffer가 JSON을 쪼개도 `data:` 줄이 완성된 뒤에만 파싱 |
| `ChatHistoryManager` | system prompt·히스토리; `CheshirePromptCatalog`로 BaseSystem/ChesterVoiceCommon 로드 |
| `CheshireLocaleResolver` | Fungus 언어 → canonical `ko`\|`ja`\|`en` (alias/`en-US` 정규화); 시나리오 CSV·UI CSV·AI prompt의 단일 locale authority |
| `CheshireUiStrings` | `Resources/Scenario/cheshire_ui_strings.csv`를 `ScenarioLocalizationTable`로 로드 (하드코딩 KO/JA/EN은 CSV 부재 시 폴백) |
| `ScenarioLocalizationTable` | Scenario/dialogue·speaker·UI CSV 공통 파서 (`id` 컬럼 + locale 컬럼, KO 폴백) |
| `PlayScenarioBlockCommand` | standing dialogue 재생; 기본 언어는 `CheshireLocaleResolver` (Inspector override 선택) |
| `CheshirePromptCatalog` | `Resources/CheshirePrompts/{locale}/{key}` 로드; 없으면 `ko` 폴백 |
| `CheshireDynamicPromptFragments` | Kitchen/Study 등 동적 fragment (`Fragment_*` 카탈로그 우선) |
| `GlobalChatbot` | `give_hint`, `emote` 공통 처리 |
| `TutorChatbot` | RAG 프로필, 퀴즈, `/tutor/grade` |
| `*RoomChatbot` | 방별 system prompt·휴리스틱 (`StudyRoomChatbot`, `WifeRoomChatbot`, …) |
| `ParretPanelChatbotBinder` | 씬별 챗봇 타입 바인딩 |

**프롬프트 Resources** (`Assets/Resources/CheshirePrompts/{ko,ja,en}/`):

- 필수 키: `BaseSystem`, `ChesterVoiceCommon`, `introPrompt`, `KitchenPrompt`, `MainBedroomPrompt`, `SonRoomPrompt`, `StudyRoomPrompt`, `TutorRoomPrompt`, `WifeRoomPrompt`, `ParrotPrompt`
- 선택 키: `HintPolicy_{Novice,Intermediate,Expert}`, `Fragment_*` (세 locale 모두 비어 있지 않은 UTF-8)
- 검증: `backend_ai/scripts/validate_cheshire_prompts.py` · EditMode `CheshirePromptCatalogTests`

### 게임 코어 (`godlotto/Script/`)

| 클래스 | 역할 |
|--------|------|
| `InventoryManager` | 아이템 CRUD UI, Tab 토글, Fungus `pressTab` |
| `Item` / `ItemPickup` | ScriptableObject 아이템, `itemId` 1~30 |
| `FlowchartLocator` | `"Variablemanager"` Flowchart 탐색 |
| `VariablemanagerSingleton` | 전역 Flowchart GO `DontDestroyOnLoad` |
| `DontDestroyGameplayCleanup` | 메인메뉴 복귀 시 DDOL 게임플레이 루트(Fungus 전역 변수·퀘스트 트래커) 정리 정책의 단일 소유자. `GlobalSettingManager`(BGM/SFX/전체화면/해상도)와 호출자 자신만 보존. `InGameSettingsPanel`·`EndSceneManager`·`IntegratedSettingUI`·`SettingPanelButtonActions`의 모든 "메인메뉴로" 진입점이 공유 |
| `AudioController` | BGM 등 (`SingletonMonoBehaviour`) |
| `OpeningMentionController` | 오프닝 씬 Bell/Fence (Interaction 패턴 예시) |
| `WifeRoomPuzzleController` | WifeRoom 클릭·패널·복귀 (RoomInteractionController 확장) |
| `MaidRoomPuzzleController` | MaidRoom 클릭 진입 (Phase R3-A, RoomInteractionController 확장) |
| `StudyRoomPuzzleController` | StudyRoom UI·월드 클릭 (R4-A CardStack/Diary, R4-B Bible/BookCase + LoadScene outcome) |
| `StudyRoomDiaryMirrorPuzzleController` | BookmarkMirror 드롭 후 위치·각도·반사 판정 → `StudyRoomMirrorPuzzleSuccessRouter`. QA seam: `TrySnapToConfiguredSolutionAndEvaluateForQa` (플레이어 입력 경로 불변; ForceSolve 아님). Adapter: `StudyRoomQaAdapter` (`preset.before-placement`, `place-bookmark` via real `FilterCardBookDropZone.OnDrop`) |
| `ChildRoomPuzzleController` | ChildRoom 클릭(R5-A), 인장 드롭(R5-B), allSealsComplete(R5-C) |
| `KitchenInteractionController` | Kitchen 월드(R6-A)·UI(R6-B)·드롭(R6-C) 클릭·패널(R6-D) 조율 |
| `KitchenPanelRegistry` | Kitchen 버너/프라이팬/앵무 패널 SetActive → Call Method (R6-D) |
| `QuizInputHandler` | TutorRoom 퀴즈 TMP 패널 활성화·제출. QA seam: `ActivateQuizInputField` via `TutorRoomQaAdapter` (`tutorroom.quiz-input`) |

### 백엔드

| 모듈 | 역할 |
|------|------|
| `ChatService` | provider fallback, 대화 전용 온도·가드, tool 주입(locale별 `_TOOL_INSTRUCTIONS`, 튜터만), tutor RAG; `response_language_instruction(locale)` |
| `dialogue_guard` | 체셔 1–2문장 대사 sanitize (빈/JSON/장문 → 로케일 폴백) |
| `sse_format` | `data: {JSON}\\n\\n` SSE 프레임 |
| `local_runtime` | LiteRT primary (`AI_PROVIDER=local`), 루프백 `GET /v1/models` |
| `locale_support` | `normalize_locale`, 플레이어 대면 오류·API 키/엔진 실패 문구·응답 언어 지시 (Unity resolver와 동일 규칙) |
| `TutorRAGService` | `tutor_rag_index.json` 검색; chunk `locale` 메타가 있으면 필터, 없으면 전체·없으면 KO 폴백; 컨텍스트 헤더 chrome locale별 |
| `QuizBank` | CSV 로드; multi-locale 컬럼(`question_*`, `acceptable_answers_*`, `reference_snippet_*`; 빈 셀 → KO); `format_bank_context_block` chrome locale별 |
| `answer_grader` / `tutor_grade` | `/tutor/grade` (`TutorGradeRequest.locale`) |
| `rate_limit` + `rate_guard` | Redis 또는 in-process window |

### 의존성 다이agram (요약)

```mermaid
graph TB
    subgraph UnityClient
        RIC[RoomInteractionController]
        SIC[SceneInteractionController]
        STS[SceneTransitionService]
        FC[Fungus Flowchart]
        VM[Variablemanager]
        INV[InventoryManager]
        BC[BaseChatbot]
        CHC[ChatHttpClient]
    end
    subgraph Backend
        MAIN[main.py]
        CS[ChatService]
        TOOLS[ToolRegistry]
    end
    RIC --> SIC
    RIC --> STS
    RIC --> FC
    BC --> CHC
    CHC --> MAIN
    MAIN --> CS
    CS --> TOOLS
    Checkpoint[CheckpointRepository] --> INV
    Checkpoint --> VM
```

---

## 6. 아키텍처 규칙

### 새 기능 추가 시 따라야 할 규칙

1. **씬 이름**은 `SceneNames`에 상수 추가 후 사용 (`godlotto/Script/Constants/SceneNames.cs`).
2. **Fungus 변수 키**는 `FungusVariableKeys`에 추가 (`godlotto/Script/Constants/FungusVariableKeys.cs`).
3. **방/복도 클릭·씬 전환**은 새 Fungus `LoadScene` 커맨드 대신 **`RoomInteractionController` + BlockOutcome** 패턴을 따릅니다. 기존 마이그레이션 참고: `godlotto/Script/Editor/CorridorEntranceSceneMigrator.cs`, `docs/fungus-room-migration-plan.md`.
4. **씬 load**는 `SceneTransitionService.LoadSceneSafely` 사용.
5. **클릭 진입** 전 `SceneInteractionController.TryInteract(interactionId)` 호출.
6. **로그**는 릴리스에 남기지 않을 진단은 `GameLog.Log` (`Core/GameLog.cs`); 실제 버그는 `Debug.LogError` 유지.
7. **싱글톤 매니저**는 `SingletonMonoBehaviour<T>` + `PersistAcrossScenes` override (`Core/SingletonMonoBehaviour.cs`).
8. **AI URL**은 `ServerConfig.ChatUrl`(루프백 플래그 vs 클라우드 URL) 또는 chatbot Inspector `localServerUrl`. 로컬 Gemma 데스크톱은 루프백을 켠다. 클라우드 QA는 플래그를 끄거나 Inspector로 EC2 URL을 지정한다. `ServerConfigTests`와 불일치하는 하드코딩 금지.
9. **체크포인트에 넣을 Fungus 키**는 `ProgressSnapshotPolicy` / `ProgressSnapshotCollector`의 capture 목록과 맞출 것.
10. **테스트**: EditMode 순수 로직 → `Assets/Editor/Tests/EditMode/`; 백엔드 → `backend_ai/tests/`.

### 파일 위치·네이밍

| 추가 대상 | 위치 | 네이밍 예 |
|-----------|------|-----------|
| 방 상호작용 | `godlotto/Script/Interaction/` | `*InteractionController`, `*PuzzleController` |
| Fungus 커맨드 | `godlotto/Script/FungusCommands/` | 동사형 (`SetBloom`, `PlayRegisteredSfx`); 선택지는 `GlassMenu` (`FungusCommands/Menu/`) |
| Fungus Menu 마이그레이션 | `godlotto/Script/Editor/GlassMenuMigrator.cs` | `Tools/Godlotto/Migrate/Fungus Menu → Glass Menu` — 연속 `Fungus.Menu` → `GlassMenu.options`, `ClearMenu` 제거 |
| 방별 AI | `mokotan/mokotan/script/AI/` | `{Room}Chatbot : BaseChatbot` |
| Cheshire 프롬프트 | `Assets/Resources/CheshirePrompts/{ko,ja,en}/` | `{Key}.txt` + `.meta`; 검증 `validate_cheshire_prompts.py` |
| Locale 해석 | `mokotan/.../AI/Localization/` | `CheshireLocaleResolver`, `CheshirePromptCatalog`, `CheshireUiStrings` |
| Scenario CSV | `Assets/Resources/Scenario/` | `the_unholy_dialogue.csv`, `the_unholy_speakers.csv`, `cheshire_ui_strings.csv` (+ `ScenarioLocalizationTable`) |
| 상수 | `godlotto/Script/Constants/` | `*Keys`, `SceneNames` |
| 에디터 마이그레이션 | `godlotto/Script/Editor/` | `*SceneMigrator` |
| API·서비스 | `backend_ai/services/` | `*_service.py` |
| LLM tool schema | `backend_ai/tools/game_tools.py` + `registry` |

### 상태 관리 패턴

- **글로벌 진행**: Fungus bool/int/string on `Variablemanager` + 필요 시 `CheckpointSaveData` 스냅샷.
- **UI/세션**: MonoBehaviour 필드 + `InteractionInputGate`.
- **설정**: PlayerPrefs (`SettingPlayerPrefsKeys`만 — 키 문자열 변경 금지, 주석에 명시). 로컬 대화 AI 끄기는 별도 키 `LocalAi.ChatDisabled` (`LocalAiReadiness`).
- **AI 대화**: 인스턴스별 `ChatHistoryManager` (씬마다 chatbot 컴포넌트).

### API 호출 패턴

- Unity: **Coroutine** + `UnityWebRequest` — `ChatHttpClient`만 수정; UI에서 직접 HTTP 금지. 체셔 대사는 `/chat/stream` + `ChatSseStreamParser`; 튜터 채점 후 LLM은 기존 `/chat` 유지. loopback 빌드는 `ChatHttpClient.FetchRootStatus`로 `GET /` 를 폴링한 뒤 전송.

### 로컬 AI 첫 실행 (Windows)

1. 핀: `backend_ai/data/local_ai_manifest.json` (LiteRT-LM 0.16.1, `gemma4-e2b` SHA256)
2. 동의 후 설치: `scripts/install_local_ai.ps1` → `backend_ai/local_install.py` (`uvx litert-lm import …`). 동의 없으면 import 없음
3. 라이선스: `installer/licenses/NOTICE.md`. 모델은 게임 종료 후에도 유지, 삭제는 `-RemoveModel`
4. 서비스는 `127.0.0.1` only. Unity 채팅 URL이 loopback이면 `local_runtime.model_available` 전까지 입력 차단
5. 수동 체크리스트: `installer/CHECKLIST.md`

- Payload 확장: `BaseChatbot.AugmentChatPayload` override → `LocalLlamaPayload` 필드 추가 → backend `ChatRequest` 동기화.
- 새 tool: `backend_ai/tools/game_tools.py` 등록 → `GlobalChatbot.ProcessCommonFunctionCalls` 또는 방별 `HandleChatbotResponse`에서 dispatch.

### 피해야 할 패턴

- Fungus 블록에 **LoadScene + isClicked 수동 정리** (Interaction 레이어와 이중·경쟁).
- `GameObject.Find` 남발 — Flowchart는 **`FlowchartLocator`**.
- `Debug.Log` 남발 — **`GameLog`** 사용.
- `SceneManager.LoadScene` 직접 호출로 **전환 중복** ( `SceneTransitionService` 우회).
- `PlayerPrefs.DeleteAll()` without preserving settings — **`PlayDataPrefsCleaner`** 패턴 사용.
- `Assets/Fungus/` 서드파티 **대규모 수정** (업스트림 merge 불가).
- API 키를 Unity/저장소에 **커밋**.

---

## 7. 확장 가이드

### 새로운 “페이지”(씬) 추가

1. **씬 에셋** 생성: `Assets/Scenes/Mokotan/.../MyRoom.unity`
2. **`EditorBuildSettings`에 등록**: `ProjectSettings/EditorBuildSettings.asset` (Unity Build Settings UI)
3. **`SceneNames`에 상수 추가**
4. **전역 Flowchart** 변수·블록 배치; `Variablemanager` 프리팹/씬 지속 확인
5. **상호작용**:
   - 단순 복도/방: `RoomInteractionController` 또는 `CorridorEntranceController` 컴포넌트 + Inspector `InteractionRoute[]`, `BlockOutcome[]`
   - 특수 퍼즐: `RoomInteractionController` 상속 (예: `WifeRoomPuzzleController.cs`)
6. **복귀 경로**: `BackNavigator.TryResolveFixedReturnScene`에 case 추가 또는 Fungus `PrevScene` 설정
7. **체크포인트(선택)**: `RoomCheckpointDefinition.Definitions` + `RoomUnlockCheckpointTrigger` on Fungus 이벤트
8. **EditMode 테스트** 추가: `Assets/Editor/Tests/EditMode/...`

### 튜토리얼 퀘스트 단계 연결

1. **단계 id**: `TutorialQuestIds` 상수만 사용 (매직 스트링 금지)
2. **순수 매핑**: `TutorialQuestProgressAdapter` — 씬·Fungus bool·Kitchen 블록 → step id
3. **런타임 부트스트랩**: `QuestTrackerHudBootstrap` — DDOL 시스템 생성 + 씬 Canvas 아래 HUD 부착 (`QuestTrackerHudHost`)
4. **런타임 브리지**: `TutorialQuestGameBridge` — 씬 로드·플래그 엣지·`BlockSignals.OnBlockEnd` 구독
5. **Fungus 수동 완료**: `CompleteTutorialQuestStep` 커맨드 (`Assets/godlotto/Script/FungusCommands/`)
6. **수동 QA**: `docs/quest-tracker-manual-verification.md`
7. 새 단계 신호 추가 시 adapter 상수/메서드 확장 → `TutorialQuestProgressAdapterTests` 추가

**DDOL 정책**: `QuestTrackerHudController`·`TutorialQuestGameBridge`만 `DontDestroyOnLoad` (퀘스트 상태·이벤트 구독 유지). HUD 위젯은 씬 Canvas 자식으로 매 씬 재생성 (`InventoryGuideController`와 동일한 scene-bound UI 패턴).

### 새로운 API 추가 (백엔드)

1. **`models/requests.py` / `responses.py`**에 Pydantic 모델
2. **`main.py`**에 라우트 등록
3. 로직은 **`services/`**에 분리 (`ChatService`와 동일 패턴)
4. **`tests/test_*.py`** 작성
5. Unity 연동 시: `ChatHttpClient` 또는 전용 클라이언트 클래스 + EditMode 테스트
6. **`backend_ai/README.md`** API 표 업데이트 (팀 문서; 선택)

**예: 기존 채점 API**

- 서버: `POST /tutor/grade` — `main.py`, `services/tutor_grade.py`
- 클라이언트: `TutorQuizGrader.cs` — URL은 chat endpoint에서 `/tutor/grade`로 치환

### 새로운 데이터 모델 추가

| 종류 | 절차 |
|------|------|
| **인벤토리 아이템** | `Item` ScriptableObject (`Assets/godlotto/Item/`), 고유 `itemId` 1~30, `ItemAcquisitionTracker` 연동 |
| **체크포인트 필드** | `CheckpointSaveData` 필드 추가 → Collector/Applier/Policy → `CheckpointRepositoryTests` |
| **Fungus 플래그** | `FungusVariableKeys` + Flowchart 변수 선언 + Collector boolean/int/string 배열 |
| **튜터 퀴즈** | `backend_ai/data/tutor_quiz/quiz_bank.csv` (KO/JA/EN 컬럼) + `validate_quiz_bank.py` |
| **Cheshire 프롬프트** | `disputatio/Assets/Resources/CheshirePrompts/{ko,ja,en}/` + `validate_cheshire_prompts.py` |
| **RAG 문서** | `backend_ai/data/tutor_rag/*.md` + `build_tutor_rag_index.py` (chunk에 `locale` 메타) |
| **LLM tool** | `game_tools.py` + Unity `HandleChatbotResponse` / `ProcessCommonFunctionCalls` |

---

## 8. 미확인 사항

코드만으로 **확실히 단정할 수 없거나 불일치**가 있는 항목입니다.

| 항목 | 관찰 | 추가 확인 방법 |
|------|------|----------------|
| **`SceneNames.MainScene` ("MainScene")** | `MainMenu` 이어하기 fallback, Jumpscare retry에 사용되나 **`MainScene.unity` 파일 없음**, `EditorBuildSettings`에도 없음 | 의도된 fallback 씬명(예: `Hall_playerble`) 확인; 상수·빌드 설정 정렬 |
| **새 게임 시작 씬** | `MainMenu.OnStartButton`은 PlayerPrefs만 지우고 **LoadScene 호출 없음** — 실제 전환은 Fungus/버튼 Inspector | `MainMenuScene.unity` Flowchart·Button onClick 추적 |
| **`IntroScene` vs `Opening_Office`** | 빌드 목록에 둘 다 존재; 정확한 오프닝 순서는 씬 내 Flowchart 의존 | 플레이through 또는 Fungus 블록 문서화 |
| **Fungus Save Point vs Checkpoint** | `SaveManager`/`SavePointKey`와 `CheckpointRepository` **병존**; 어떤 메뉴가 어느系를 쓰는지 코드만으로 단일 정책 불명 | 기획·`docs/superpowers/plans/2026-05-11-remove-custom-save-system.md`와 런타임 확인 |
| **`resumeSpawnId`** | `CheckpointSaveData`에 필드 있으나 **`ProgressSnapshotApplier`에서 spawn 적용 코드 미확인** | 스폰 시스템 존재 여부 씬 검색 |
| **운영 HTTPS URL** | `ServerConfig` 클라우드 필드·`deploy/Caddyfile` 도메인과 Unity 최종 URL이 코드만으로 불명. 저장소에 `Resources/ServerConfig.asset` 없음 | 배포 환경·로컬 빌드는 `UseLocalLoopback` |
| **체셔 50케이스 eval** | 스위트·스코어러·게이트 테스트 있음. 라이브 2026-09-03 재측정(`dialogue_max_tokens=64`, `num_ctx=2048`, 스트림 가드 통과): `gemma4-e2b` / LiteRT-LM, Windows AMD64 (Intel), 50/50 유효, 폴백 0, JSON/툴 누출 0, 날조 사실 0, 완료 p50 4.9s / p95 5.6s, 첫 `text_delta`(TTFT) p50 3.7s / p95 3.7s. Groq 미사용. 한 대 측정이며 최소 사양 조사는 아님. 말끝(깍/삐약/푸드덕)은 하드 게이트가 아님 | 재측정: `cd backend_ai` 후 `AI_PROVIDER=local python -m tests.evals.run_cheshire_eval`. 게이트: 유효 ≥ 90%, 누출 0, 날조 0 |
| **Windows 게임 설치본** | `scripts/install_local_ai.ps1`·`installer/CHECKLIST.md`는 플래너. 실제 게임+런타임 패키징 설치 프로그램은 없음 | 패키징 파이프라인 확정 |
| **Unity EditMode 하네스** | 이 클론(`D:\\Capstone\\newCapstone\\disputatio`)에 Unity 인스턴스가 없으면 unity-cli compile/test 불가 | 해당 프로젝트를 Editor에서 연 뒤 `ServerConfigTests` 실행 |
| **Redis in prod** | `REDIS_URL` 비면 in-process rate limit (멀티 replica 부적합) — 운영 `.env` 미포함 | 서버 `/opt/newcapstone/.env` |
| **WebGL 빌드** | `deploy/serve_webgl_brotli.py` 존재; 게임 WebGL 배포 파이프라인은 본 문서 범위에서 미검증 | 빌드 타겟·CI 확인 |
| **Tutor RAG 인덱스 비어 있음** | `backend_ai/data/tutor_rag_index.json`이 `chunks: []` (임베딩 미생성). locale 필터는 동작하나 검색 컨텍스트는 항상 빈 결과 | `build_tutor_rag_index.py`로 인덱스 재생성 후 커밋/배포 |
| **EN/JA 프롬프트의 KO 제어 태그** | `[진행]`, `[시스템: …]`, `[문제 은행]` 등 일부 대괄호 태그가 EN/JA 본문에 KO로 잔존 (의도적 클라이언트 주입 태그). 본문 서술은 EN/JA | Task 6 이후 주입 prefix 로컬라이즈 여부·태그 키 안정성 점검 |

---

## 부록: 자주 쓰는 코드 경로

| 작업 | 경로 |
|------|------|
| 메인 메뉴 | `disputatio/Assets/godlotto/Script/MainMenu.cs` |
| 빌드 씬 목록 | `disputatio/ProjectSettings/EditorBuildSettings.asset` |
| 씬 전환 | `disputatio/Assets/godlotto/Script/Interaction/SceneTransitionService.cs` |
| 방 클릭 | `disputatio/Assets/godlotto/Script/Interaction/RoomInteractionController.cs` |
| 체크포인트 저장 | `disputatio/Assets/godlotto/Script/Checkpoint/CheckpointRepository.cs` |
| AI HTTP | `disputatio/Assets/mokotan/mokotan/script/AI/ChatHttpClient.cs` |
| Cheshire locale/프롬프트 | `.../AI/Localization/CheshireLocaleResolver.cs`, `CheshirePromptCatalog.cs` |
| Cheshire 프롬프트 txt | `disputatio/Assets/Resources/CheshirePrompts/` |
| AI 서버 URL | `disputatio/Assets/godlotto/Script/Config/ServerConfig.cs` |
| 메인메뉴 복귀 시 DDOL 정리 | `disputatio/Assets/godlotto/Script/DontDestroyGameplayCleanup.cs` (모든 "메인메뉴로" 버튼이 공유) |
| FastAPI 진입 | `backend_ai/main.py` |
| LLM tools | `backend_ai/tools/game_tools.py` |
| CI (C#) | `.github/workflows/ci-check.yml` → `scripts/CSharpSyntaxChecker/` |
| QA autorun orchestrator | `scripts/qa/autorun/` (classify / checkpoint / git isolation / state machine) |
| QA autorun tests | `python -m pytest scripts/qa/tests -q` |
| CI (backend) | `.github/workflows/backend-build.yml` |
| 배포 | `.github/workflows/deploy-backend.yml`, `deploy/docker-compose.prod.yml` |
| 체셔 대화 eval | `backend_ai/tests/evals/` |
| Fungus 마이그레이션 계획 | `docs/fungus-room-migration-plan.md` |

---

*문서 버전: 저장소 조사 기준 2026-09-03 (체셔 로컬 Gemma 4 E2B·URL 이중 모드·dialogue_guard 반영). 변경 시 §8 불일치 항목부터 재검증하세요.*
