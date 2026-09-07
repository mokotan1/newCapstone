# AI and Dialogue

Back to [Home](Home.md).

## Cheshire, prompts, and tutor behavior

- Main AI system: dynamic NPC dialogue generated at runtime instead of only pre-written lines, with scene-specific context flags shaping prompts. ([source_id: planning:b98bbfbdb019](sources/planning/민원번호-33의-챗봇-개발-기획--b98bbfbdb019.md))
- Persona and instructions live in external prompt files so behavior can change without code edits; Fungus bool variables inject situational orders. ([source_id: planning:b98bbfbdb019](sources/planning/민원번호-33의-챗봇-개발-기획--b98bbfbdb019.md))
- Production stack routes Unity chat UI through FastAPI `/chat` endpoints with Groq primary and Gemini fallback providers. ([source_id: technical:194f6010e716](sources/technical/architecture--194f6010e716.md))
- LLM abuse defenses and play-test guidance are documented for Cheshire prompt hardening and rate limiting. ([source_id: technical:ca17d157de10](sources/technical/llm-abuse-defense-plan--ca17d157de10.md))

## Related sources

- [Architecture](Architecture.md)
- [Source Index — Planning](Source-Index-Planning.md)
- [Source Index — Technical](Source-Index-Technical.md)
