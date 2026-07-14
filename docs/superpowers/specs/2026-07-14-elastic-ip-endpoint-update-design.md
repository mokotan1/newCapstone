# Elastic IP Endpoint Update Design

## Goal

Update the Unity client endpoint from the retired EC2 public IP to the new
Elastic IP so every chatbot scene targets the currently deployed FastAPI
backend.

## Endpoint

- Old: `http://15.134.24.132:8000/chat`
- New: `http://54.156.51.119:8000/chat`

## Scope

- Update the `ServerConfig` runtime default.
- Update serialized scene and prefab overrides that take precedence over the
  runtime default.
- Update the code-level parrot panel override.
- Update the corresponding EditMode test expectation.
- Update architecture documentation that records the production endpoint.
- Do not change the FastAPI backend, Docker port, or EC2 configuration.

## Runtime Behavior

`BaseChatbot` continues to prefer a non-empty serialized `localServerUrl` and
otherwise falls back to `ServerConfig.ChatUrl`. Both sources will use the same
Elastic IP endpoint after this change.

## Verification

1. Search the repository scope for the old IP and require zero matches outside
   historical design documentation.
2. Confirm all active client endpoint references contain the new IP.
3. Run the focused `ServerConfigTests` EditMode test when the Unity test runner
   is available; otherwise report that runtime test execution remains pending.

## Follow-up

Replacing the IP with an HTTPS domain is preferred later, but DNS, TLS, and
proxy configuration are intentionally outside this immediate recovery change.
