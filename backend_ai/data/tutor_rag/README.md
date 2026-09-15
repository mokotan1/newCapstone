# 튜터 RAG 코퍼스 (서술·인용)

이 폴더에 `.md` 또는 `.txt` 파일을 넣은 뒤 저장소 루트에서:

```bash
cd backend_ai
python scripts/build_tutor_rag_index.py
```

을 실행하면 `data/tutor_rag_index.json`이 갱신됩니다. 임베딩은 로컬 `local-hash-v1`이며 외부 API 키는 필요 없습니다.

문제 문항·정답 별칭은 `data/tutor_quiz/quiz_bank.csv`에서 편집합니다.
