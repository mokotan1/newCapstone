# 실행 재개 계약

계약 버전: 2. 역할과 완료 기준은 저장소에, 진행 상태는 작업 폴더에 둔다.
채팅 기억만으로 다음 세션을 시작하지 않는다.

## 시작 시 적을 것

`docs/development/tasks/<feature-id>/index.md`에 다음을 남긴다.

| 필드 | 내용 |
|---|---|
| runner | 예: Cursor Agent |
| modelId | 부모 모델. 모르면 unknown |
| checkedAt | 도구 목록 확인 시각 |
| capabilities | repo-read, file-write, shell, Task 위임, Task `model` 지정, Unity |
| assignment | 위임했다면 `subagent_type` + 실제 `model` 인자. 안 띄웠으면 순차/부모 |

비밀 키는 적지 않는다. 위임 로그가 없으면 멀티에이전트 완료라고 하지 않는다.

## 인계

1. index에 미커밋 파일, Editor/QA lease, 다음 단계를 적는다.
2. 쓰기 작업을 끝낸 뒤에만 새 총괄이 같은 파일을 연다.
3. 새 총괄은 git status·diff·증거 명령을 다시 본다. 기록과 코드가 다르면 관련 검사만 재실행한다.
4. 리뷰 세션이 없으면 `independentReview: false`를 유지한다.

모델이 바뀌어도 AC와 완료 기준은 바꾸지 않는다.
이 문서는 Grok/GPT API 연결이나 native agent 등록을 설치하지 않는다.
