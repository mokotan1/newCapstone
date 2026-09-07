# Architecture

Back to [Home](Home.md).

## Unity, backend, deployment, and tools

- Monorepo layout: Unity client in `disputatio/`, FastAPI AI backend in `backend_ai/`, CI scripts, and deploy compose under `deploy/`. ([source_id: technical:194f6010e716](sources/technical/architecture--194f6010e716.md))
- Unity 6 (6000.0.36f1) with URP, Fungus dialogue, Input System, and team gameplay code under `Assets/godlotto/Script/`. ([source_id: technical:194f6010e716](sources/technical/architecture--194f6010e716.md))
- Client persistence uses PlayerPrefs checkpoints and Fungus variables; server-side data includes CSV quiz banks and optional Redis rate limits. ([source_id: technical:194f6010e716](sources/technical/architecture--194f6010e716.md))
- Deployment path: GHCR images to EC2 with Docker Compose and Caddy per `deploy/docker-compose.prod.yml`. ([source_id: technical:194f6010e716](sources/technical/architecture--194f6010e716.md))

## Related sources

- [AI and Dialogue](AI-and-Dialogue.md)
- [Operations](OPERATIONS.md)
- [Source Index — Technical](Source-Index-Technical.md)
