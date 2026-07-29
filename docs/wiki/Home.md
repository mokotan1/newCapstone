# Project Knowledge Wiki

## Source of truth

Curated pages in `docs/wiki/` summarize reviewed project knowledge.
Each nontrivial claim cites one or more `source_id` values that link to
transcript pages under `docs/wiki/sources/`.
The manifest at `docs/wiki/_meta/source-manifest.yaml` is the inventory
of record for which originals were converted and their extraction status.

## Scope

- **Included:** scenario PDFs/Markdown, planning PDFs/PPTX, and allowlisted
  technical docs with extracted or needs-review transcripts.
- **Excluded from public navigation:** internal reports, pending or skipped
  HWP originals, and blocked conversions.
- **HWP:** owner-skipped; not converted in this pipeline.

- **Manifest records:** 52 total, 43 listed below.

## Update command

After inventory, conversion, or validation changes, regenerate this wiki:

```powershell
python tools/wiki_rag/build_wiki.py --manifest docs/wiki/_meta/source-manifest.yaml --wiki-root docs/wiki
```

## Curated pages

- [Game Overview](Game-Overview.md)
- [Story and World](Story-and-World.md)
- [Rooms and Progression](Rooms-and-Progression.md)
- [AI and Dialogue](AI-and-Dialogue.md)
- [Architecture](Architecture.md)
- [Development History](Development-History.md)
- [Operations](OPERATIONS.md)

## Source indexes

- [Scenario (6)](Source-Index-Scenario.md)
- [Planning (26)](Source-Index-Planning.md)
- [Technical (11)](Source-Index-Technical.md)

## Public source listing

| Category | Title | Type | Status | Transcript |
| --- | --- | --- | --- | --- |
| Planning | .민원번호 33의 챗봇 개발 기획 | pdf | extracted | [transcript](sources/planning/민원번호-33의-챗봇-개발-기획--b98bbfbdb019.md) |
| Planning | .민원번호 33의 챗봇 개발 기획 | pptx | extracted | [transcript](sources/planning/민원번호-33의-챗봇-개발-기획--5656494d4c10.md) |
| Planning | 2층 복도 기획 | pdf | extracted | [transcript](sources/planning/2층-복도-기획--a54025e67028.md) |
| Planning | 2층 복도 기획 | pptx | needs_review | [transcript](sources/planning/2층-복도-기획--23235ab919ae.md) |
| Planning | The unholy of mention_컨셉 기획서 | pptx | needs_review | [transcript](sources/planning/the-unholy-of-mention_컨셉-기획서--189dba1942da.md) |
| Planning | The unholy of mention_컨셉 기획서 | pdf | extracted | [transcript](sources/planning/the-unholy-of-mention_컨셉-기획서--35ada8161577.md) |
| Planning | UI 컨셉 기획 | pdf | extracted | [transcript](sources/planning/ui-컨셉-기획--46be221ef692.md) |
| Planning | UI 컨셉 기획 | pptx | needs_review | [transcript](sources/planning/ui-컨셉-기획--d55dcff2ee6b.md) |
| Planning | disputatio_초기 계획안 | pdf | extracted | [transcript](sources/planning/disputatio_초기-계획안--9d4611de3ae3.md) |
| Planning | 공부방(2층) 기획서 | pdf | extracted | [transcript](sources/planning/공부방-2층-기획서--d716468cdf00.md) |
| Planning | 공부방(2층) 기획서 | pptx | needs_review | [transcript](sources/planning/공부방-2층-기획서--d6e66a02a4b8.md) |
| Planning | 서재 개발 기획 | pdf | extracted | [transcript](sources/planning/서재-개발-기획--2c99a6b50413.md) |
| Planning | 서재 개발 기획 | pptx | needs_review | [transcript](sources/planning/서재-개발-기획--1a7c6222ad99.md) |
| Planning | 아내 방 기획 | pdf | extracted | [transcript](sources/planning/아내-방-기획--af96b05baf10.md) |
| Planning | 아내 방 기획 | pptx | needs_review | [transcript](sources/planning/아내-방-기획--d61aadd7dd90.md) |
| Planning | 아들 방 기획 | pdf | extracted | [transcript](sources/planning/아들-방-기획--f142e62e4abf.md) |
| Planning | 안방 기획 | pdf | extracted | [transcript](sources/planning/안방-기획--0cdfdc88add7.md) |
| Planning | 안방 기획 | pptx | needs_review | [transcript](sources/planning/안방-기획--3f167ea617e5.md) |
| Planning | 오프닝 개발 기획서 | pdf | extracted | [transcript](sources/planning/오프닝-개발-기획서--e4e36660bb79.md) |
| Planning | 오프닝 개발 기획서 | pptx | needs_review | [transcript](sources/planning/오프닝-개발-기획서--11bd8b014e77.md) |
| Planning | 지하 연구실 기획 | pdf | extracted | [transcript](sources/planning/지하-연구실-기획--47f3be566f34.md) |
| Planning | 지하 연구실 기획 | pptx | needs_review | [transcript](sources/planning/지하-연구실-기획--e7ee91d062a2.md) |
| Planning | 첫 장면 기획 | pdf | extracted | [transcript](sources/planning/첫-장면-기획--ea29b35cc87e.md) |
| Planning | 첫 장면 기획 | pptx | needs_review | [transcript](sources/planning/첫-장면-기획--e16101da266f.md) |
| Planning | 현재 플로우 직관성 + 맵 제작 + 복도 기믹 추가 | pdf | extracted | [transcript](sources/planning/현재-플로우-직관성-맵-제작-복도-기믹-추가--53ed77ef462a.md) |
| Planning | 현재 플로우 직관성 + 맵 제작 + 복도 기믹 추가 | pptx | needs_review | [transcript](sources/planning/현재-플로우-직관성-맵-제작-복도-기믹-추가--acafc91a0413.md) |
| Scenario | The unholy of mention 세계관 | pdf | extracted | [transcript](sources/scenario/the-unholy-of-mention-세계관--93cff884e57e.md) |
| Scenario | The unholy of mention 최종 시나리오 | md | extracted | [transcript](sources/scenario/the-unholy-of-mention-최종-시나리오--31ea9031cf8f.md) |
| Scenario | The unholy of mention 플롯 1막 | pdf | extracted | [transcript](sources/scenario/the-unholy-of-mention-플롯-1막--f905409b92e5.md) |
| Scenario | The unholy of mention 플롯 3막 | pdf | needs_review | [transcript](sources/scenario/the-unholy-of-mention-플롯-3막--4dc9203c1c6b.md) |
| Scenario | The unholy of mention 플롯 4막 | pdf | extracted | [transcript](sources/scenario/the-unholy-of-mention-플롯-4막--2148f646a4c5.md) |
| Scenario | the unholy of mention 플롯 2막 | pdf | extracted | [transcript](sources/scenario/the-unholy-of-mention-플롯-2막--6547e2e5db43.md) |
| Technical | 2026-07-14-regression-playtest | md | extracted | [transcript](sources/technical/2026-07-14-regression-playtest--1634d7ac3efa.md) |
| Technical | architecture | md | extracted | [transcript](sources/technical/architecture--505bbb50868b.md) |
| Technical | fungus-migration-audit | md | extracted | [transcript](sources/technical/fungus-migration-audit--aacf05fd62fd.md) |
| Technical | fungus-room-migration-plan | md | extracted | [transcript](sources/technical/fungus-room-migration-plan--e52de73281b4.md) |
| Technical | glass-choice-menu-usage | md | extracted | [transcript](sources/technical/glass-choice-menu-usage--f05cffd194cf.md) |
| Technical | llm-abuse-defense-plan | md | extracted | [transcript](sources/technical/llm-abuse-defense-plan--ca17d157de10.md) |
| Technical | llm-defense-play-test-guide | md | extracted | [transcript](sources/technical/llm-defense-play-test-guide--873778a0ddcd.md) |
| Technical | play-log-analysis | md | extracted | [transcript](sources/technical/play-log-analysis--ab3836b72366.md) |
| Technical | play-log-pipeline | md | extracted | [transcript](sources/technical/play-log-pipeline--68636b3b936e.md) |
| Technical | play-log-sheets-upload | md | extracted | [transcript](sources/technical/play-log-sheets-upload--0b0032e4671b.md) |
| Technical | quest-tracker-manual-verification | md | extracted | [transcript](sources/technical/quest-tracker-manual-verification--9db1ee420efd.md) |
