# 문제 은행 (기획자용)

`quiz_bank.csv`를 엑셀 또는 구글 시트에서 UTF-8 CSV로 저장해 두면 됩니다.

검증:

```bash
cd backend_ai
python scripts/validate_quiz_bank.py
```

필수 열: `question_id`, `question_ko`, `acceptable_answers_ko`, `reference_snippet_ko`

다국어(선택): `question_ja`, `question_en`, `acceptable_answers_ja`, `acceptable_answers_en`, `reference_snippet_ja`, `reference_snippet_en`  
(비어 있으면 서버가 한국어 열로 폴백합니다. 구형 `acceptable_answers` / `reference_snippet` 열도 로더가 인식합니다.)

- `acceptable_answers_*`: 정답을 `|`로 구분 (예: `베드로|시몬 베드로` / `Peter|Simon Peter`)
- `difficulty`, `tags`는 선택

Unity `Resources/TutorQuestionOrder.txt`에 출제 순서대로 `question_id`를 한 줄에 하나씩 적으면, 해당 순서로 서버 판정에 ID가 전달됩니다.

Unity 에디터에서는 메뉴 **Di Tools → Tutor Quiz Bank**에서 CSV와 순서 파일을 편집·저장할 수 있습니다. 기본 CSV 경로는 Unity 프로젝트(`disputatio`)의 상위 폴더 아래 `backend_ai/data/tutor_quiz/quiz_bank.csv`로 잡혀 있습니다.
