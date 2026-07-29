# Architecture

Back to [Home](Home.md).

## Unity, backend, deployment, and tools

- Monorepo layout: Unity client in `disputatio/`, FastAPI AI backend in `backend_ai/`, CI scripts, and deploy compose under `deploy/`. ([source_id: technical:505bbb50868b](sources/technical/architecture--505bbb50868b.md))
- Unity 6 (6000.0.36f1) with URP, Fungus dialogue, Input System, and team gameplay code under `Assets/godlotto/Script/`. ([source_id: technical:505bbb50868b](sources/technical/architecture--505bbb50868b.md))
- Client persistence uses PlayerPrefs checkpoints and Fungus variables; server-side data includes CSV quiz banks and optional Redis rate limits. ([source_id: technical:505bbb50868b](sources/technical/architecture--505bbb50868b.md))
- Deployment path: GHCR images to EC2 with Docker Compose and Caddy per `deploy/docker-compose.prod.yml`. ([source_id: technical:505bbb50868b](sources/technical/architecture--505bbb50868b.md))

## Related sources

- [AI and Dialogue](AI-and-Dialogue.md)
- [Operations](OPERATIONS.md)
- [Source Index — Technical](Source-Index-Technical.md)
