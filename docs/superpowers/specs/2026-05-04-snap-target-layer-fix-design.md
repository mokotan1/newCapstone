# SnapTarget 레이어 일괄 교정 및 재발 방지 설계서

작성일: 2026-05-04
대상 시스템: `disputatio` (Unity 2D), `DragManager2D` 스냅 시스템
관련 코드: `disputatio/Assets/godlotto/Script/DragManager2D.cs`, `disputatio/Assets/godlotto/Script/SnapTarget.cs`
관련 데이터: `disputatio/ProjectSettings/TagManager.asset`, `disputatio/Assets/Scenes/**/*.unity`

---

## 1. 배경 — 무엇이 잘못되어 있었나

`DragManager2D.FindBestSnapTarget` 은 스냅 후보를 찾을 때 `SnapTarget` 레이어 마스크로만 `Physics2D.OverlapCircleAll` 을 돈다.

```csharp
int snapMask = LayerMask.GetMask("SnapTarget");
var hits = snapMask != 0
    ? Physics2D.OverlapCircleAll(draggablePosition, snapSearchRadius, snapMask)
    : Physics2D.OverlapCircleAll(draggablePosition, snapSearchRadius);
```

`ProjectSettings/TagManager.asset` 상 `SnapTarget` 은 layer 인덱스 **7** 이다.

`Assets/Scenes/Mokotan/Second Floor/ChildRoom.unity` 에서 씰 타겟들의 실제 `m_Layer` 분포는 다음과 같다.

| 오브젝트 | `m_Layer` |
|---|---|
| 1st seal Target | **8** ← 이상치 |
| 2nd seal Target | 7 |
| 3rd seal Target | 7 |
| 4th seal Target | 7 |
| 5th seal Target | 7 |
| 6th seal Target | 7 |
| 7th seal Target | 7 |

`m_Layer: 8` 은 TagManager 에 이름이 없는 빈 슬롯이라 이름으로 마스크를 만들 때 잡히지 않는다. 결과적으로 1번 씰 타겟만 스냅 후보군에서 누락되어 1st seal 이 스냅되지 않는다. 코드 결함이 아니라 씬 데이터 결함이다.

같은 종류의 누락이 다른 씬·다른 SnapTarget 에도 잠복할 수 있으므로, 본 문서는 (a) 알려진 1건을 포함한 **전 씬 일괄 교정**, (b) **재발 방지 가드**, (c) **검증 절차** 를 함께 정의한다.

## 2. 목표

1. 모든 씬에서 `SnapTarget` 컴포넌트가 부착된 GameObject 의 `layer` 를 `SnapTarget`(인덱스 7) 으로 일치시킨다.
2. 동일한 실수가 재발해도 자동으로 잡히고/교정되도록 코드에 보호 장치를 심는다.
   - 에디터: 잘못된 layer 자동 교정.
   - 런타임: 잘못된 layer 발견 시 1회 경고 로그.
3. 변경 후 "전 씬에서 잘못된 SnapTarget layer 가 0건임" 을 자동 검증할 수 있다.

## 3. 비목표

- `SnapTarget` 외 다른 시스템(드래그 본체 layer, 정렬 레이어, 다른 마스크) 변경.
- `DragManager2D.cs` 의 마스킹 로직 자체 수정 — 사용자 분석대로 코드는 정상.
- 기존 layer 이름 `SnapTarget` 또는 인덱스 7 의 변경.
- 에디터 도구의 패키지화/메뉴 트리 정리 등 부가 인프라.

## 4. 컴포넌트 구성

### 4.1 신규 — `disputatio/Assets/godlotto/Script/Editor/SnapTargetLayerEnforcer.cs`

Editor 전용 클래스 (`Editor/` 폴더라 빌드에서 자동 제외).

- `[InitializeOnLoad]` 정적 클래스. Unity 에디터 부팅 시 1회 등록.
- 책임 ①: **단건 자동 교정** — `EditorSceneManager.sceneSaving` 콜백 훅. 저장 직전 그 씬의 모든 `SnapTarget` 컴포넌트를 `FindObjectsByType<SnapTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None)` 로 수집하여, 각 GO 의 `layer` 가 `LayerMask.NameToLayer("SnapTarget")` 와 다르면 보정. 보정 시 `Debug.LogWarning($"[SnapTargetLayerEnforcer] Auto-fixed '{path}': '{oldLayerName}' → 'SnapTarget'.", go)` 로 명시.
- 책임 ②: **일괄 교정 메뉴** — `Tools > GodLotto > Fix SnapTarget Layers (All Scenes)`.
  - 진입 시 `EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()` 호출, 사용자가 취소하면 작업 중단.
  - `EditorSceneManager.GetSceneManagerSetup()` 으로 현재 setup 백업.
  - `AssetDatabase.FindAssets("t:Scene")` 으로 씬 GUID 수집 → `Assets/Scenes/**` 만 필터.
  - 씬마다 `EditorSceneManager.OpenScene(path, OpenSceneMode.Single)` → SnapTarget 수집 → 위반 GO 의 `layer = LayerMask.NameToLayer("SnapTarget")` → `EditorSceneManager.MarkSceneDirty` → `EditorSceneManager.SaveScene`.
  - 모든 씬 처리 후 `EditorSceneManager.RestoreSceneManagerSetup(backup)`.
  - 콘솔 요약 `[SnapTargetLayerEnforcer] Fixed N targets in M scenes (scanned K).`
- 책임 ③: **Dry-run 메뉴** — `Tools > GodLotto > Validate SnapTarget Layers (Dry Run)`.
  - 메뉴 ② 와 동일하게 순회하지만 layer 변경/저장은 하지 않고 위반 목록만 콘솔에 출력. `Scene path · GO HierarchyPath · current layer` 형식.
- Reentrancy guard: `private static bool isFixing` 정적 플래그. 일괄 교정 메뉴 코드 진입 시 `true`, 종료 시 `false`. `sceneSaving` 콜백은 `isFixing == true` 면 즉시 return (메뉴가 이미 처리 중이므로 콜백 작업 불필요).
- `LayerMask.NameToLayer("SnapTarget")` 결과가 `-1` 이면 `Debug.LogError("[SnapTargetLayerEnforcer] Layer 'SnapTarget' is missing in TagManager.")` 후 작업 중단.

### 4.2 수정 — `disputatio/Assets/godlotto/Script/SnapTarget.cs`

`OnEnable()` 추가:

```csharp
private void OnEnable()
{
    int expected = LayerMask.NameToLayer("SnapTarget");
    if (expected >= 0 && gameObject.layer != expected)
    {
        Debug.LogWarning(
            $"[SnapTarget] '{name}' is on layer '{LayerMask.LayerToName(gameObject.layer)}' " +
            $"(expected 'SnapTarget') — DragManager2D will skip it.", this);
    }
}
```

- 빌드에도 남는 런타임 가드 (1회 분기, 비용 무시 가능).
- 자동 보정은 하지 않는다 — 런타임에서 조용히 고치면 원인 추적이 더 어렵다.

### 4.3 데이터 패치 — `disputatio/Assets/Scenes/**/*.unity`

- 4.1 의 일괄 교정 메뉴 1회 실행으로 발생하는 변경. 사람이 직접 YAML 을 손대지 않는다.
- 알려진 1건: `ChildRoom.unity` 의 `1st seal Target` (line 13811 부근, `m_Layer: 8 → 7`).
- 다른 씬의 결과는 메뉴 실행 후 콘솔에서 확인 가능. git diff 로 그대로 추적·리뷰된다.

## 5. 동작 흐름

### 5.1 최초 일괄 교정 (사람 1회 실행)

1. Unity 에디터를 연다 → `[InitializeOnLoad]` 가 `SnapTargetLayerEnforcer` 등록.
2. `Tools > GodLotto > Validate SnapTarget Layers (Dry Run)` 실행 → 위반 목록 콘솔 출력. 출력 0건이면 5.1 종료, 0건이 아니면 다음 단계.
3. `Tools > GodLotto > Fix SnapTarget Layers (All Scenes)` 실행 → 모든 씬 교정·저장.
4. `git status` / `git diff` 로 바뀐 씬 확인 → 커밋.

### 5.2 단건 자동 교정 (재발 방지, 백그라운드)

1. 새 SnapTarget 추가 또는 기존 GO 의 layer 를 다시 잘못 바꿈.
2. `Ctrl+S` 또는 자동 저장 → `EditorSceneManager.sceneSaving` 콜백 발화.
3. 콜백이 그 씬의 모든 `SnapTarget` 을 훑어 잘못된 layer 를 7 로 끌어올림 + 워닝 로그.
4. 디스크에 저장된 씬에는 이미 교정된 layer 가 직렬화.

### 5.3 런타임 가드 (실행 시 후방 안전망)

1. 씬 로드 → `SnapTarget.OnEnable()` 발화.
2. 잘못된 layer 면 `Debug.LogWarning(...)` 1줄. 컨텍스트 GO 클릭 시 Hierarchy 핑.
3. 자동 보정 없음 — 빌드 결정성과 원인 추적 우선.

### 5.4 정적 검증 (PR/배포 직전 자가 점검)

1. SnapTarget GUID 확인:
   - 출처: `disputatio/Assets/godlotto/Script/SnapTarget.cs.meta`
   - 현재 값: `99e7bc1181b09294085c1acbeb3788eb`
2. ripgrep 으로 모든 `*.unity` 의 `m_Component`/`m_Layer` 영역을 훑어 SnapTarget GUID 가 들어간 컴포넌트의 부모 GameObject 의 `m_Layer` 가 7 이 아닌 케이스를 찾는다. (의심 후보 추출 용도.)
3. 권위 있는 0건 판정은 `Validate SnapTarget Layers (Dry Run)` 메뉴 결과로 갈음.
4. (선택) Play Mode 회귀 — 5.5 참조.

### 5.5 Play Mode 회귀

- ChildRoom 진입 → `1st seal` 드래그 → `1st seal Target` 위에 드롭 → 스냅 성공(`occupied = true`, 위치 정렬) 확인.
- 보조: 2nd/3rd seal 도 동일하게 스냅되는지.

## 6. 에지 케이스 및 리스크

| ID | 케이스 | 대응 |
|---|---|---|
| E1 | 비활성 GO 의 SnapTarget 누락 | `FindObjectsInactive.Include` 명시 (일괄/단건 양쪽). |
| E2 | Prefab 인스턴스의 layer override | 인스턴스 단위로 `gameObject.layer = 7` 적용(override 갱신 OK). 프리팹 자체 layer 가 잘못된 경우는 자동 패치 없이 콘솔 경고만 — 프리팹 변경은 전염성이 커서 사람 확인이 필요. |
| E3 | 미래에 SnapTarget layer 인덱스가 7 이 아니게 됨 | 코드는 `LayerMask.NameToLayer("SnapTarget")` 로만 조회, `7` 하드코딩 금지. `-1` 이면 LogError 후 스킵. |
| E4 | 일괄 교정 진입 시 미저장 변경 손실 | `SaveCurrentModifiedScenesIfUserWantsTo()` 호출, 사용자 취소 시 중단. |
| E5 | 빌드 사이즈/런타임 비용 | `OnEnable` 1회 분기, 무시 가능. Editor 폴더는 빌드 자동 제외. |
| E6 | 일괄 교정의 `SaveScene` 이 `sceneSaving` 콜백을 재진입 | `isFixing` 정적 플래그로 콜백을 즉시 return. |
| E7 | `SnapTarget.cs.meta` GUID 변경(메타 재생성) | 정적 검증 명령에 GUID 추출 한 줄(`grep guid disputatio/Assets/godlotto/Script/SnapTarget.cs.meta`)을 같이 적어 둠 — 누구나 재추출 가능. |
| E8 | 자동 교정의 silent change 위험 | 자동 교정 시 항상 `LogWarning` 으로 어느 GO 의 layer 를 무엇 → 7 로 바꿨는지 명시. SnapTarget 부착 GO 의 layer 가 7 이 아닌 의도된 케이스는 디자인상 존재하지 않음. |
| E9 | 정적 grep false positive | grep 은 보조 지표. 권위 있는 0건 판정은 에디터 정식 API(Validate Dry Run) 결과로 갈음. |

## 7. 테스트 전략

### T1. 사전(Before) 스냅샷
- 작업 시작 전 `Validate SnapTarget Layers (Dry Run)` 실행 → 콘솔 출력 보관. 최소 ChildRoom/`1st seal Target` 1건은 반드시 잡혀야 함 — 안 잡히면 도구 자체가 잘못 만들어진 것.

### T2. 코드 단위 — Editor 도구 자체 검증 (수작업)
- 임시 빈 씬에서 GO 두 개 만들고 둘 다 `SnapTarget` 부착, 하나는 layer 7, 하나는 layer 0.
- Dry-run 메뉴 → 0번 GO 만 위반 리스트에 나오는지.
- Fix 메뉴 → 0번 GO 의 layer 가 7 로 바뀌고 씬 저장되는지.
- Fix 메뉴 재실행 → 위반 0건, 변경 0건(idempotent).
- 임시 씬 삭제.

### T3. 단건 자동 교정 검증
- 임시 씬에서 SnapTarget 부착 GO 의 layer 를 7 → 0 으로 변경 후 `Ctrl+S`.
- 콘솔에 `Auto-fixed ...` 워닝 + 디스크 layer 가 7 로 저장됨.

### T4. 런타임 가드 검증
- 임시 씬에서 자동 교정 회피 후(콜백 직후 Play 진입) layer 0 으로 Play.
- `[SnapTarget] '...' is on layer 'Default' ...` 워닝 + 컨텍스트 GO 핑 확인.

### T5. 전 씬 일괄 교정 결과 검증
- `Fix SnapTarget Layers (All Scenes)` 1회 실행 → `Fixed N targets in M scenes` 요약 보관.
- 곧바로 Dry-run 재실행 → 위반 0건.
- 정적 grep 명령 → 위반 0건.
- `git status` / `git diff` 로 변경 씬 수·라인이 콘솔 요약과 일치, 각 씬 변경이 `m_Layer: X → 7` 단순 한 줄인지 확인.

### T6. Play Mode 회귀 — 핵심 시나리오
- ChildRoom 에서 `1st seal` 드래그 → `1st seal Target` 위 드롭 → 스냅 성공.
- 2nd/3rd seal 도 동일하게 스냅되는지(기존 동작 유지).

### T7. 음성 회귀 — 인접 시스템 영향 없음
- ChildRoom 안의 다른 드래그/픽업 가능 오브젝트가 여전히 잡히고 움직이는지.

### T8. (선택) 빌드 영향 확인
- Editor 폴더 코드가 빌드 산출물에 포함되지 않는지 — 폴더명 규칙 준수 여부 확인.

## 8. 롤백 계획

- 코드 두 파일은 단일 커밋이라 `git revert` 1회로 되돌릴 수 있다.
- 씬 데이터 변경도 같은 커밋 또는 직후 커밋이라 동일하게 revert 가능. 단, revert 직후 다시 1번 씰이 스냅되지 않는 원래 버그 상태로 돌아간다.

## 9. 의존성·전제

- Unity 2D 프로젝트, `SnapTarget` layer 가 TagManager 인덱스 7 에 존재해야 한다.
- `SnapTarget.cs` 의 GUID(`99e7bc1181b09294085c1acbeb3788eb`) 는 작성 시점 기준이며, meta 재생성 시 정적 검증 명령에서 재추출 필요.
- Editor 폴더 규약(`Assets/.../Editor/*.cs` 는 에디터 전용)이 그대로 유효하다고 가정.

## 10. 후속(이번 범위 밖)

- 같은 류의 "특정 컴포넌트가 붙은 GO 는 특정 layer 여야 함" 규칙이 다른 시스템에도 있다면 동일 패턴(Editor enforcer)으로 일반화 가능. 본 문서 범위는 SnapTarget 한 가지로 한정.
