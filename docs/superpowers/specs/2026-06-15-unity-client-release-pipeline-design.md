# Unity Client Release Pipeline Design

## Goal

Add a GitHub Actions pipeline that builds the Unity Windows client from `disputatio` and publishes tagged release builds as downloadable GitHub Release assets.

## Current Context

- Backend deployment is already automated through GitHub Actions, GHCR, Docker Compose, and server health checks.
- Unity client deployment is not automated yet.
- The Unity project lives in `disputatio`.
- The Unity editor version is `6000.0.36f1`.
- Build scenes are already registered in `disputatio/ProjectSettings/EditorBuildSettings.asset`.
- The intended client platform is Windows, not WebGL.

## Release Model

The client pipeline uses three levels of confidence:

- `workflow_dispatch`: manual smoke test for the GitHub Actions build pipeline.
- `pull_request`: build validation for Unity project changes, with an artifact uploaded for inspection.
- `push` tag `v*`: production release path. Builds the Windows client, zips the output, creates or updates a GitHub Release, and uploads the zip.

Regular `main` pushes do not create releases. This avoids noisy release history and keeps release creation intentional.

## Workflow Behavior

The workflow creates a Windows x64 standalone build using GameCI Unity Builder. It uses:

- `projectPath: disputatio`
- `unityVersion: 6000.0.36f1`
- `targetPlatform: StandaloneWindows64`
- `buildName: Disputatio`

The workflow uploads the build zip as a GitHub Actions artifact on every successful run. On `v*` tags, it also publishes the zip to GitHub Releases.

## Required GitHub Secrets

The repository must define these secrets before the workflow can build:

- `UNITY_LICENSE`
- `UNITY_EMAIL`
- `UNITY_PASSWORD`

These are required by GameCI for Unity activation in CI.

## Trigger Scope

Pull request builds only run for Unity-relevant changes:

- `.github/workflows/unity-client-build.yml`
- `disputatio/Assets/**`
- `disputatio/Packages/**`
- `disputatio/ProjectSettings/**`

Tag builds run for `v*` tags because tags are the release signal.

## Verification

The first verification path is:

1. Run the workflow manually with `workflow_dispatch`.
2. Confirm the Actions artifact exists.
3. Download and unzip the artifact.
4. Confirm `Disputatio.exe` exists in the zip.
5. Run the executable locally and verify the main menu opens.

The release verification path is:

1. Push a test tag such as `v0.1.0-test`.
2. Confirm the workflow succeeds.
3. Confirm a GitHub Release exists for the tag.
4. Download the attached zip.
5. Confirm the executable starts.

## Non-Goals

- This does not deploy WebGL.
- This does not upload the Windows build to the VM.
- This does not change backend deployment.
- This does not add Steam or itch.io deployment.
