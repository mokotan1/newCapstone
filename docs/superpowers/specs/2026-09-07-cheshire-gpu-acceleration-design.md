# 체셔 로컬 AI GPU 가속 설계

- 작성일: 2026-09-07
- 상태: 대화 합의 반영 초안. GPU 경로 실측 전. CPU 수치는 2026-09-03 eval만 확정.
- 기준 변경: [PR #326 — Cheshire local Gemma 4 E2B dialogue](https://github.com/mokotan1/newCapstone/pull/326)
- 기준 머지 커밋: `2978d02adcf41f815ba20f6487da0705f15a3c6a` (`develop`)
- 관련: `docs/superpowers/specs/2026-09-03-cheshire-local-gemma4-e2b-design.md`, `docs/체셔_Gemma4_E2B_로컬AI_설계_요약.md`, `docs/architecture.md` §체셔 샘플링·§8 eval

이 문서는 구현 설계다. CPU 지연만 실측이다. GPU 카드 목록·타임아웃·오프로드 단계의 배포값은 1절 목표를 기준으로 검증 단계에서 근거와 함께 고정한다.

## 1. 목표

로컬 체셔가 **클라우드 API급(첫 글자 1초 안쪽)을 CPU에서 따라잡는 것은 목표가 아니다.**  
목표는 장치를 나누고, NVIDIA CUDA가 있는 PC에서만 2초/1초 급으로 내리는 것이다.

| 경로 | 사양 | 완료 시간 목표 | 근거 |
|---|---|---|---|
| **CPU (LiteRT, GPU 없음)** | 현재 제품 최소. 8GB RAM, 4코어, 외장 GPU 불필요 | **약 5초**. 2초·API급 약속 금지 | 2026-09-03 50케이스, Gemma 4 E2B, `dialogue_max_tokens=64`, `num_ctx=2048`, Windows AMD64 Intel. 완료 p50 **4.9s** / p95 **5.6s**, TTFT p50/p95 **3.7s**. Groq 미사용. 한 대 측정 |
| **CUDA 최소** | NVIDIA, **VRAM 4GB** | 워밍업 후 완료 **약 2초** | 추정. 모델(~2.4GB)은 들어가지만 유니티와 VRAM을 나눠 1초는 불안 |
| **CUDA 권장** | NVIDIA, **VRAM 6GB** (여유 **8GB**, RTX 3060 12GB / 4060 8GB급) | 워밍업 후 완료 **약 1초** | 추정. 1초권의 실사용 바닥은 6GB, 안정은 8GB |

설정창에서 실행 장치(CPU / GPU / 자동)를 고른다. GPU 사용률(%) 슬라이더는 만들지 않는다. GPU가 실패하면 **로컬 CPU로만** 복구한다. 클라우드 Groq/Gemini를 GPU 실패 폴백으로 쓰지 않는다.

선택한 설정(`requested_mode`)과 실제 장치(`effective_backend`)를 나눠 표시한다. GPU를 골랐다는 이유만으로 GPU 사용 중이라고 쓰지 않는다.

평가 지표는 GPU 점유율이 아니라 **첫 글자 표시 시간, 전체 응답 시간, 게임 프레임 시간**이다.

## 2. 기존 구조와 제약

#326 대화 경로:

```text
Unity 체셔
  → FastAPI 127.0.0.1:8000 (/chat, /chat/stream)
  → LiteRT-LM 127.0.0.1:9379
  → Gemma 4 E2B (.litertlm)
```

확정된 제약:

- 런타임 핀: `litert-lm==0.16.1`, 모델 id `gemma4-e2b`, 아티팩트 ~2.4GB.
- Unity는 `/chat/stream` SSE만 본다. 게임 툴은 체셔에 주입하지 않는다. `/tutor/grade`는 유지.
- `LocalAiReadiness`는 루프백 모델 준비 여부만 본다. GPU 사용·추론 성공을 대신하지 않는다.
- `LocalAi.ChatDisabled`는 켜기/끄기만 있다. GPU 오프로드·VRAM 한도는 없다.
- 설정창 키는 볼륨·해상도·전체화면뿐이다. GPU 조절 UI는 없다.
- 설정창은 `Time.timeScale = 0`이다. 폴링·타임아웃은 실시간 기준이어야 한다.
- CPU 병목은 출력 길이가 아니라 **프리필(TTFT ~3.7s)** 이다. 생성은 완료 4.9s 중 약 1s다. `max_tokens`를 더 깎아도 API급이 되지 않는다.
- 실사용 프롬프트는 eval보다 길다(공통 말투 + 방 지침: 주방 ~1.1천 자, 서재 ~1.4천 자, 가정교사 ~2.6천 자). GPU에서도 첫 글자가 병목이다.
- LiteRT-LM은 온디바이스 런타임이다. Windows 0.16.1이 NVIDIA **CUDA**를 쓰는지 미확인이다. GPU를 쓰더라도 OpenCL 계열일 수 있다. LiteRT `backend: gpu`만으로 1절의 1초/2초를 약속하지 않는다.

## 3. 접근 방식

대화에서 합의한 전제: **1초/2초는 CUDA 가능한 별 런타임 경로**이고, 현재 LiteRT CPU를 CUDA로 “올리는 패치”가 아니다.

| 방식 | 장점 | 비용 및 판단 |
|---|---|---|
| A. LiteRT `config.json` `backend: gpu`만 | 모델·SSE 유지 | 0.16.1 Windows에서 CUDA 여부·지연 미검증. **1초/2초 SLO의 배달 수단으로 채택하지 않음** |
| B. FastAPI 유지 + NVIDIA일 때 CUDA 사이드카 (OpenAI 호환 서버) | Unity 계약 유지, `n_gpu_layers`로 거친 오프로드, CPU LiteRT는 그대로 ~5초 | **권장.** GGUF 등 별 아티팩트·동의 다운로드·품질 eval 재게이트 필요 |
| C. LiteRT를 CUDA 런타임으로 교체 | 단일 엔진 | CPU-only 플레이어와 #326 설치 경로를 깨뜨림. 이번 범위 제외 |

**채택: B.** FastAPI가 장치를 고른다.

```text
Unity 체셔  (변경 없음: /chat/stream)
     ↓
FastAPI 127.0.0.1:8000
     ├─ effective=cpu → LiteRT-LM 127.0.0.1:9379  (.litertlm, ~5s)
     └─ effective=gpu → CUDA 서버 127.0.0.1:9380  (핀한 OpenAI 호환, ~2s/1s 목표)
```

사이드카 후보(검증 후 하나 핀):

1. **llama.cpp `llama-server`** (권장 후보): CUDA, `n_gpu_layers`, 게임 임베드에 맞음.
2. Ollama: 설치는 쉽고 제어·용량이 큼.
3. vLLM: 데스크톱 게임 동봉에 과함. 제외.

구현 착수 전 **게이트 0**: 고정 `litert-lm==0.16.1` Windows에서 공식 GPU 설정이 실제로 NVIDIA GPU를 쓰는지, 워밍업 후 완료가 4GB에서 ~2s / 6–8GB에서 ~1s인지 측정한다.

- 게이트 통과 → 사이드카 없이 LiteRT GPU만으로 1절 SLO를 배달. B의 두 번째 아티팩트는 하지 않는다.
- 게이트 실패(CPU와 비슷, CUDA 아님, 또는 SLO 미달) → B로 진행. LiteRT는 CPU 경로로 유지.

AMD/인텔 내장 GPU는 CUDA 경로가 아니다. 그 PC는 LiteRT CPU(~5s)다.

두 모델을 VRAM에 동시에 올리지 않는다. 장치 전환 시 이전 엔진을 내린 뒤 다음 엔진을 올린다.

## 4. 지연·VRAM 계약

제품 문구와 설정 안내, 완료 기준은 이 표를 따른다.

| 등급 | 하드웨어 | 완료(워밍업 후) | 제품에서 말해도 되는 것 |
|---|---|---|---|
| 현재 최소 | GPU 없음, LiteRT CPU | ~5s (실측 p50 4.9s) | “오프라인 가능. 응답은 약 5초.” 2초라고 쓰지 않음 |
| GPU 최소 | NVIDIA **4GB** | ~2s (목표, 미실측) | “GPU 최소. 약 2초.” 1초 보장 아님 |
| GPU 1초 바닥 | NVIDIA **6GB** | ~1s (목표, 미실측) | 1초권 실사용 최소 |
| GPU 1초 여유 | NVIDIA **8GB+** (3060/4060급) | ~1s 안정 (목표, 미실측) | 권장 |

4GB는 “모델이 올라가는 최소”이지 “플레이 중 1초”가 아니다. 유니티 URP와 AI가 같은 GPU를 쓴다.

CPU에서 5초 아래를 약속하지 않는다. 프롬프트 축소·CPU 런타임 교체는 잘 돼도 3–4초 여지이고, 이번 범위에서 CPU를 2초로 내리는 작업은 제외한다.

콜드 스타트(VRAM 적재·컴파일)는 SLO에서 뺀다. SLO는 워밍업 이후 대화다.

## 5. 설정창 UX

기존 설정창에 ‘체셔 AI’ 영역을 추가한다. **GPU 사용률 % 슬라이더는 없다.** NVIDIA도 레이어 오프로드 단계만 제공한다.

| 항목 | 표시 및 동작 |
|---|---|
| 로컬 AI | 기존 켜기/끄기 (`LocalAi.ChatDisabled`) 유지 |
| 응답 장치 | 자동(권장), GPU 우선, CPU |
| GPU 부담 | GPU일 때만: 낮음 / 중간 / 높음. `n_gpu_layers`(또는 동등 설정)에 매핑. “GPU 40%”가 아님 |
| 현재 상태 | 준비 중, GPU 사용 중, CPU 사용 중, 사용 확인 중, 사용 불가 |
| 안내 | 예상 체감: CPU ~5초, GPU 최소 ~2초, GPU 권장 ~1초. 복구 사유. 미실측이면 목표라고 명시 |
| 적용 | 변경을 백엔드에 전달. 즉시 완료로 표시하지 않음 |
| 속도 확인 | 유휴 상태에서 짧은 샘플. 게임 대화 성능과 구분해 표시 |

### 모드 정책

- `auto`: NVIDIA CUDA가 보이고 VRAM이 4GB 이상이면 GPU 초기화와 짧은 추론을 시도한다. 실패하면 CPU(~5s)로 복구하고, 같은 런타임·모델 해시·GPU/드라이버에서는 실패를 기억해 반복 시도를 피한다. `auto`는 “벤치에서 가장 빠른 장치를 고른다”가 아니다. 사용 가능하면 GPU, 아니면 CPU다.
- `gpu`: GPU 우선. 실패 시 CPU 복구 + 사유. 사용자가 다시 적용하면 GPU를 재시도한다. VRAM 4GB 미만이면 시도 전 안내하고 CPU를 쓸 수 있다.
- `cpu`: CUDA/LiteRT GPU 초기화를 하지 않는다. LiteRT CPU만.

기본값: 지원 GPU 실측·복구 검증을 통과한 배포만 `auto`. 그 전 빌드는 `cpu`.

원격 `ChatUrl`(루프백 아님)이면 이 PC의 GPU 설정을 끄고, 로컬 AI 전용 옵션임을 안내한다.

GPU 실패 기록은 런타임 버전, 모델 해시, GPU·드라이버와 함께 저장한다. 식별 불가면 현재 세션만.

### 표시 원칙

- `requested_mode`와 `effective_backend`를 구분한다.
- `effective_backend = gpu`는 런타임 초기화 결과 또는 검증된 로그로 CUDA/GPU 실행이 확인된 경우만.
- 설정 파일에 gpu가 있거나 샘플 추론이 성공한 것만으로 GPU를 확정하지 않는다.
- 확인 불가면 `effective_backend = unknown`, UI는 ‘사용 확인 중’.
- 한국어·영어·일본어는 기존 체셔 UI 문자열 패턴. 키보드 탐색에 새 컨트롤을 넣는다.

## 6. 구성 요소와 책임

```text
Unity
├── InGameSettingsPanel          체셔 AI 영역만 연결. 프로세스 제어 없음
├── LocalAiSettingsController    상태 조회, 적용, 폴링, 안내 (신규)
└── LocalAiReadiness             모델 존재 + inference_ready

FastAPI
├── LiteRTProvider               CPU 경로 (기존)
├── CudaCompatProvider           GPU 경로. OpenAI 호환 스트림 → SSEEvent (신규, 게이트 0 실패 시)
└── LocalRuntimeManager          설정, 프로세스 소유, 전환 잠금, 워밍업, CPU 복구 (신규)
```

### Unity

- HTTP/SSE 계층은 기존 계약 유지.
- 프로세스 spawn/kill을 Unity에 넣지 않는다.

### 백엔드 `LocalRuntimeManager`

1. 사용자 선택 원자적 저장.
2. 게임이 소유한 추론 프로세스의 시작·종료·재시작 (LiteRT 및, 필요 시 CUDA 서버).
3. 초기화 결과와 짧은 워밍업 추론.
4. `requested_mode` / `effective_backend` / 오류 / 복구.
5. 채팅과 장치 전환의 동시성.

FastAPI는 로컬 워커 하나, 관리자 하나. 엔진 재시작 중에도 FastAPI는 살아 있어 상태 조회가 된다.

설치 스크립트는 설치와 FastAPI 기동만 한다. LiteRT/CUDA serve 책임은 관리자에 모은다. `local_install.py`와 PowerShell에 serve 명령을 중복하지 않는다. 백그라운드 창은 숨긴다.

외부에서 띄운 추론 프로세스는 죽이지 않는다. 포트가 외부 점유면 `externally_managed`로 표시하고 장치 전환을 거절한다. 대화 가능 여부는 따로 검사한다.

CUDA 아티팩트(GGUF 등)는 #326 동의와 같은 방식으로, **추가 용량을 보여 준 뒤** 받는다. 동의 없이 받지 않는다. 품질은 기존 50케이스 게이트(유효 ≥ 90%, JSON/툴 누출 0, 날조 0)를 다시 통과해야 한다. HuggingFace 리포·파일명·SHA는 검증 후 manifest에 핀한다. 이 문서에 없는 리포를 지어내지 않는다.

## 7. 설정 저장과 런타임 구성

사용자 선택과 엔진 설정은 게임 전용 디렉터리에 둔다. 기준은 백엔드 파일이다. Unity는 조회만 한다. PlayerPrefs에 장치 모드를 따로 두지 않는다.

```text
%LOCALAPPDATA%/Disputatio/local-ai/settings.json
%LOCALAPPDATA%/Disputatio/local-ai/runtime-config.json
```

`settings.json` 예시:

```json
{
  "mode": "auto",
  "gpu_offload": "medium"
}
```

`gpu_offload`는 `low` | `medium` | `high`다. 관리자가 CUDA 서버 인자(예: `n_gpu_layers`)로 바꾼다. 정확한 정수 매핑은 검증 PC에서 고정한다. 퍼센트 한도가 아니다.

- `auto`는 관리자 정책이다. 엔진에는 해석된 `cpu` 또는 `gpu`만 넘긴다.
- `%USERPROFILE%/.litert-lm/config.json`을 덮어쓰지 않는다.
- LiteRT 모델 경로·체크섬은 #326 manifest를 재사용한다. 장치 전환만으로 LiteRT 모델을 다시 받지 않는다.
- 샘플링·프롬프트·`dialogue_max_tokens=64`·`num_ctx=2048`은 CPU/GPU 공통으로 유지한다. 속도를 이유로 힌트·정답 정책을 바꾸지 않는다.
- 프로세스 명령은 신뢰하는 실행 파일 + 인자 배열이다. 사용자 문자열을 셸에 넣지 않는다.

## 8. 상태 전환과 오류 복구

```text
ready
  → draining (진행 중 요청 완료 대기, 새 추론 차단)
  → starting (소유 엔진 종료 후 새 장치 실행)
  → warming (짧은 내부 추론)
  → ready

GPU 초기화·워밍업 실패
  → CPU(LiteRT)로 한 번 복구
  → warming
  → ready 또는 failed
```

- 적용 요청은 하나. 중복은 기존 작업 반환 또는 충돌.
- 진행 중 스트림은 정상 완료를 기다린다. 제한시간 초과 시 기존 취소·실패 후 전환.
- FastAPI에서 요청 진입을 잠근다. Unity 입력 차단만으로 경쟁을 풀지 않는다.
- 초기화·워밍업에 유한 타임아웃. 값은 실측 후 고정. UI는 실시간 폴링 (`timeScale`과 무관).
- GPU→CPU 복구는 한 번. CPU도 실패하면 `failed`와 재시도 안내.
- GPU 추론 중 오류는 그 요청을 기존 오류 계약으로 끝낸다. 이미 그린 스트림을 자동 재생하지 않는다. 다음 요청을 위해 CPU 복구를 한다.
- 장치 전환으로 대화 기록·게임 상태를 지우지 않는다.
- 설정창을 닫아도 적용은 계속된다. 다시 열면 백엔드 상태를 읽는다.
- 클라우드 fallback을 GPU 복구로 쓰지 않는다.

## 9. API 계약

제어 API는 loopback만. 비어 있지 않은 로컬 세션 토큰. 토큰은 시작 시 신뢰된 로컬 클라이언트에만 주고 로그에 쓰지 않는다. 임의 명령·경로를 body로 받지 않는다. 브라우저 임의 origin을 허용하지 않는다.

### GET /local-ai/status

```json
{
  "requested_mode": "gpu",
  "effective_backend": "cpu",
  "gpu_offload": "medium",
  "state": "ready",
  "model_available": true,
  "inference_ready": true,
  "managed": true,
  "vram_mib": 4096,
  "latency_class": "cpu_5s",
  "operation_id": null,
  "fallback_reason": "gpu_initialization_failed"
}
```

- `effective_backend`: `cpu` | `gpu` | `unknown`
- `latency_class`: `cpu_5s` | `gpu_2s` | `gpu_1s` | `unknown` — 안내용. 벤치 결과가 있으면 그 값을 우선한다.
- `vram_mib`는 확인될 때만.
- 장치 이름은 런타임이 줄 때만 선택 필드.

### PUT /local-ai/settings

```json
{
  "mode": "gpu",
  "gpu_offload": "medium"
}
```

- `mode`: `auto` | `cpu` | `gpu`
- `gpu_offload` 생략 시 저장된 값 또는 `medium`
- 저장 후 적용 시작, `202` + `operation_id`. 즉시 완료로 응답하지 않는다.
- 외부 관리·다른 변경과 충돌 시 `409` + 구조화 사유.
- CPU 복구 후에도 `requested_mode`는 유지. 실제 장치와 `fallback_reason`으로 차이를 설명한다.

### POST /local-ai/benchmark

- 유휴·ready만. 대화/전환과 겹치면 `409`.
- 고정 짧은 샘플. 대화 기록·툴에 영향 없음.
- 반환: 백엔드 TTFT, 완료 시간, 가능하면 tokens/sec.
- UI에서 게임 대화 성능과 구분.
- 비교를 위해 장치를 몰래 바꾸지 않는다.

### 호환

- `/chat`, `/chat/stream`, `/tutor/grade`, SSE 스키마 유지.
- 기존 health 필드는 남기고 `inference_ready` 등을 확장.
- 전환 중 새 채팅은 재시도 가능한 `503` + 사유. Unity는 입력을 보존하고 준비 후 재시도를 안내.

## 10. 구현 순서

1. **게이트 0 — LiteRT GPU 실측:** Windows 0.16.1, 기존 `.litertlm`, 전용 config. 실제 장치 증거(로그/초기화 결과)와 워밍업 후 TTFT/완료를 4GB·6GB·8GB급에서 잰다. SLO 충족 시에만 LiteRT GPU를 배달 수단으로 채택.
2. **게이트 0 실패 시 CUDA 사이드카:** 후보 서버 하나 핀, GGUF(또는 동등) SHA, 동의 다운로드, FastAPI provider, 포트 9380, 50케이스 재게이트.
3. **LocalRuntimeManager:** 단일 소유권, 저장, 전환 잠금, 워밍업, CPU 복구, 오프로드 단계.
4. **API·readiness:** 상태/설정/벤치 API, 채팅 진입 잠금, 로컬 토큰.
5. **Unity UI:** 장치, 오프로드 단계, 실제 상태, 지연 안내(~5s/~2s/~1s), 적용 중, 현지화, 키보드.
6. **성능·회귀:** 샘플 vs 실게임(프롬프트가 긴 방 포함), 프레임 시간, #326 대화 eval. 품질·프레임 검증 전에 GPU `auto` 기본 배포를 완료로 치지 않음.

0.16.1 공식 GPU 설정이 안 되면 최신 LiteRT로 임의 올리지 않는다. 호환 버전을 따로 핀하고 모델·스트림·eval을 다시 검증하는 별 변경으로 다룬다.

## 11. 검증 및 완료 기준

### 기능

- `cpu` 모드에서 CUDA/LiteRT GPU 초기화가 없다.
- GPU 성공 시 런타임 증거와 UI가 같다.
- GPU 실패 시 CPU 복구 한 번, 원인과 실제 장치 표시. 완료 체감은 ~5s 안내.
- CPU도 실패하면 입력 차단과 재시도 안내.
- 적용 중 연타, 채팅, 설정창 닫기, 씬 전환으로 엔진이 두 개 뜨지 않는다.
- 외부 프로세스를 죽이지 않고 공용 LiteRT 설정을 안 바꾼다.
- 재실행 시 모드·오프로드가 유지된다. 깨진 설정 파일은 `cpu`로 안전하게.
- `timeScale == 0`이어도 폴링·타임아웃이 간다.
- 스트림 중 전환으로 대사 중복·기록 유실이 없다.

관리자 단위 테스트: 성공·실패·복구·경쟁·소유권. Unity EditMode: 상태 매핑·readiness. PlayMode 또는 실빌드: 적용 흐름·표시.

### 성능

같은 모델 계열, 같은 프롬프트·64토큰 상한으로 CPU와 GPU를 비교한다. 콜드 로드와 워밍업 이후를 분리한다.

| 지표 | 범위 |
|---|---|
| 첫 글자 표시 | Unity 요청 → SayDialog 실제 표시 |
| TTFT | 백엔드 추론 요청 → 첫 토큰 |
| 완료 | 요청 → 응답 종료 |
| 생성 속도 | 런타임이 줄 때만 tokens/sec |
| 게임 영향 | 플레이 중 프레임 시간, 메모리, VRAM |

완료 기준(워밍업 후, 중앙값과 p95를 함께 기록):

- CPU 경로: 완료가 실측과 같이 **약 5초** 대역이면 통과. CPU를 2초로 만들었는지로 완료하지 않음.
- CUDA 4GB: 완료 중앙값 **2.5초 이하**를 1차 게이트로 제안. 미달이면 4GB에 2초를 제품 문구에서 뺀다.
- CUDA 6GB+: 완료 중앙값 **1.5초 이하**를 1차 게이트로 제안. 미달이면 1초 문구를 권장 사양에서 뺀다.

위 2.5s/1.5s는 1절 목표(2s/1s)에 대한 검증 여유이며, 첫 실측 후 조정할 수 있다. 조정 시 이 문서의 표를 같이 고친다.

GPU 없음, GPU 초기화 실패, VRAM 부족, NVIDIA 4/6/8GB, 내장 GPU(CUDA 아님 → CPU)를 검증한다. 보고서에 GPU·드라이버·RAM·런타임·장면·샘플 수를 남긴다. 프레임 허용은 프로젝트 기존 예산을 따른다.

#326 대화 eval, SSE, 준비 상태, 튜터 채점 회귀를 돌린다.

## 12. 이번 범위에서 제외

- CPU LiteRT를 2초 또는 API급으로 만드는 작업.
- GPU 사용률(%) 슬라이더, 특정 GPU 픽커, NPU.
- vLLM, 클라우드를 GPU 실패 폴백으로 쓰는 것.
- 힌트·정답 정책 변경, `max_tokens`/`num_ctx`를 속도 이유로 더 깎아 CPU 5초를 깨는 것(CPU SLO는 5초 유지).
- 동의 없는 추가 모델 다운로드.
- 게이트 0이 성공했는데도 두 번째 런타임을 넣는 것.

## 13. 구현 전 위험

- LiteRT 0.16.1 Windows GPU ≠ CUDA일 수 있다. 게이트 0 없이 1초/2초를 약속하지 않는다.
- `.litertlm`과 GGUF는 다른 파일이다. CUDA 경로의 리포·용량·라이선스를 검증 전 단정하지 않는다.
- 모델 파일 크기만으로 VRAM을 단정하지 않는다. 컨텍스트·런타임·유니티를 포함해 잰다.
- 같은 GPU에서 대화 가속과 프레임 저하를 같이 본다.
- 엔진 재시작의 첫 로드는 길다. 즉시 적용 완료를 약속하지 않는다.
- GPU 미확인 상태를 성공으로 치지 않는다.

## 14. 대화에서 고정한 비목표 (번복 금지)

- 로컬 CPU만으로 Groq급 속도.
- 지금 클라이언트에서 GPU 사용량 조절 (구현 후에만 오프로드 단계).
- 현재 최소 사양(GPU 없음)에서 2초.
- 4GB VRAM에서 1초 보장.
- 5초가 CPU의 물리 상수. (현재 스택의 **실용 바닥**이며, 이번 기능의 CPU 목표이기도 하다.)
