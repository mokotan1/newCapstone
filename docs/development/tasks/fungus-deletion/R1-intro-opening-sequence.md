# R1 IntroScene 오프닝 연출 이전

상태: implementation-applied, verification-pending (2026-09-28). `BetaEnd`는 범위 밖이다. 게임 QA 도구 문제로 플레이 검증은 보류하고, 사용자 요청에 따라 독립 리뷰 에이전트는 실행하지 않는다. 따라서 이 작업은 `verified`가 아니다.

## 관찰 가능한 동작

새 게임이 `IntroScene`에 진입하면 기존 이미지 전환·눈 노출 연출·효과음·검은 페이드의 순서를 유지하고 `Opening_Office`로 한 번 이동한다. 씬의 단일 Fungus `start` 블록과 Flowchart를 제거한다. 개발자용 F6 오프닝 스킵 경로는 유지한다.

## 원본 명령 대응

`IntroScene.unity`의 `start` 블록(fileID `684100715`) `commandList` 순서:

1. Wait 2초, `PostExposureController.SetEyesClosed`, `first_chainsaw_1_0` off, `first_chainsaw_2_0` on, SFX 23.
2. Wait 1초, `SetEyesOpenSmoothly(0.7)`, Wait 2초, `SetEyesClosed`, `first_chainsaw_2_0` off, `first_chainsaw_3_0` on, SFX 23.
3. Wait 1초, `SetEyesOpenSmoothly(0.7)`, SFX 2, Wait 2초, `SetEyesClosed`, `first_chainsaw_3_0` off, `first_chainsaw_4_0` on, SFX 23.
4. Wait 1초, `SetEyesOpened`, `SetDramaticEnding`, Wait 1.5초 두 번, 검은 FadeScreen alpha 1을 1초 동안 완료까지 대기, SFX 2 정지, `Opening_Office` 로드.

씬 이미지 네 개와 `PostExposureController`는 직렬화 참조로 연결한다. 효과음은 기존 Fungus 커맨드와 같이 런타임 `SfxController.Instance`를 사용한다. `GameplayScreenFade`, `SceneTransitionService`, `SceneNames`를 재사용한다. 코루틴이 씬 종료로 취소된 뒤 페이드 콜백에서 다른 씬을 로드하지 않도록 막는다.

## 작업 소유

- 하위 모델 구현 허용: 신규 `disputatio/Assets/godlotto/Script/Title/IntroOpeningSequence.cs` 한 파일. Unity Editor/씬/meta 및 다른 파일은 수정하지 않는다.
- 부모 소유: 스크립트 `.meta`, `IntroScene.unity` 연결과 Flowchart 제거, 관련 문서와 검증.
- 제외: 공용 `SequencePlayer` 확장, `Assets/Fungus/` 변경, Say/Menu 이전, 이후 오프닝 씬 변경, 기존 사용자 변경 정리.

## 검증 계획

- 플레이 QA가 복구되면 새 게임 → Intro → Opening_Office, F6 스킵, 설정 유지와 새 Console 오류를 격리 프로필에서 확인한다.
- 독립 리뷰를 생략했으므로 R2/R3 완료 판정은 보류한다.

## 현재 확인한 증거

- `dotnet run --no-restore --project scripts/CSharpSyntaxChecker/CSharpSyntaxChecker.csproj -- disputatio/Assets`: exit 0. 일반 `dotnet run`은 사용자 NuGet.Config 읽기 권한 때문에 실패했으며 `--no-restore`로 기존 assets를 사용했다.
- Unity Editor 6000.0.36f1에서 `editor refresh --compile`: 컴파일 완료.
- Editor에서 `IntroScene`을 열어 원본 Flowchart 1개·블록 1개, 네 이미지 각각 1개, `PostExposureController` 1개를 확인한 뒤 대상 씬만 저장했다.
- 씬 재로드: Flowchart 0, `IntroOpeningSequence` 1, 이미지/노출 참조 5개 유지, 초기 이미지 active 상태 `true,false,false,false`, dirty=false. `IntroScene`과 `Opening_Office`는 모두 빌드 씬 목록에 있다.
- EditMode Console 오류·경고 출력 `[]`. `SettingPanelManager`의 누락 스크립트 1개는 HEAD 씬에도 같은 GUID로 존재하는 기존 상태다.
- 씬 재직렬화에서 빈 `m_Name`/`m_EditorClassIdentifier`의 Unity 표준 공백 때문에 이 씬의 `git diff --check`는 경고를 낸다. 그 외 변경은 컨트롤러 추가, Flowchart 제거, 삭제된 Flowchart 선택 참조 정리다.
