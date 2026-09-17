# P0-inventory-scanner / Flowchart inventory + C# Fungus scanner

- state: done (cloud)
- phase: P0

## AC

- 씬 YAML에서 Flowchart 관련 신호(블록 이름, ExecuteBlock, Clickable2D 참조 수) 수집
- `disputatio/Assets` C#에서 `using Fungus` / ExecuteBlock / Flowchart 참조 파일 목록
- read-only; 씬·코드 변경 없음

## 사용

```bash
export PYTHONPATH=scripts
python3 -m pytest scripts/fungus_deletion/tests -q
python3 -m fungus_deletion.cli inventory --repo-root .
python3 -m fungus_deletion.cli scan --repo-root .
```
