# Unity Client Release Pipeline Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a GitHub Actions workflow that builds the Unity Windows client and publishes tagged builds to GitHub Releases.

**Architecture:** A single workflow owns Unity client build and release packaging. PR and manual runs upload an artifact; tag runs also create or update the matching GitHub Release.

**Tech Stack:** GitHub Actions, GameCI Unity Builder, Unity 6000.0.36f1, StandaloneWindows64, GitHub CLI.

---

## File Structure

- Create: `.github/workflows/unity-client-build.yml`
  - Builds `disputatio` with GameCI.
  - Zips the build output.
  - Uploads an Actions artifact.
  - Publishes the zip to GitHub Releases only on `v*` tags.
- Create: `docs/superpowers/specs/2026-06-15-unity-client-release-pipeline-design.md`
  - Records the approved design and release model.

## Task 1: Add Unity Client Build Workflow

**Files:**
- Create: `.github/workflows/unity-client-build.yml`

- [ ] **Step 1: Create the workflow**

```yaml
name: Unity Client Build

on:
  workflow_dispatch:
  pull_request:
    paths:
      - ".github/workflows/unity-client-build.yml"
      - "disputatio/Assets/**"
      - "disputatio/Packages/**"
      - "disputatio/ProjectSettings/**"
  push:
    tags:
      - "v*"

permissions:
  contents: write

concurrency:
  group: unity-client-build-${{ github.ref }}
  cancel-in-progress: true

env:
  UNITY_VERSION: "6000.0.36f1"
  PROJECT_PATH: disputatio
  TARGET_PLATFORM: StandaloneWindows64
  BUILD_NAME: The Unholy of Mention
  ARTIFACT_NAME: The-Unholy-of-Mention-Windows

jobs:
  build-windows:
    name: Build Windows client
    runs-on: ubuntu-latest
    steps:
      - name: Checkout code
        uses: actions/checkout@v4
        with:
          lfs: true

      - name: Cache Unity Library
        uses: actions/cache@v4
        with:
          path: ${{ env.PROJECT_PATH }}/Library
          key: Library-${{ env.PROJECT_PATH }}-${{ env.TARGET_PLATFORM }}-${{ hashFiles('disputatio/Assets/**', 'disputatio/Packages/**', 'disputatio/ProjectSettings/**') }}
          restore-keys: |
            Library-${{ env.PROJECT_PATH }}-${{ env.TARGET_PLATFORM }}-
            Library-${{ env.PROJECT_PATH }}-

      - name: Build Windows client
        uses: game-ci/unity-builder@v4
        env:
          UNITY_LICENSE: ${{ secrets.UNITY_LICENSE }}
          UNITY_EMAIL: ${{ secrets.UNITY_EMAIL }}
          UNITY_PASSWORD: ${{ secrets.UNITY_PASSWORD }}
        with:
          projectPath: ${{ env.PROJECT_PATH }}
          unityVersion: ${{ env.UNITY_VERSION }}
          targetPlatform: ${{ env.TARGET_PLATFORM }}
          buildName: ${{ env.BUILD_NAME }}
          buildsPath: build

      - name: Package build
        shell: bash
        run: |
          set -euo pipefail
          SAFE_REF="$(printf '%s' "${GITHUB_REF_NAME}" | tr '/\\:*?"<>|' '-')"
          ZIP_NAME="${ARTIFACT_NAME}-${SAFE_REF}-${GITHUB_SHA::7}.zip"
          cd build
          zip -r "../${ZIP_NAME}" .
          cd ..
          echo "ZIP_NAME=${ZIP_NAME}" >> "${GITHUB_ENV}"

      - name: Upload build artifact
        uses: actions/upload-artifact@v4
        with:
          name: ${{ env.ARTIFACT_NAME }}-${{ github.run_id }}
          path: ${{ env.ZIP_NAME }}
          retention-days: 14

      - name: Publish GitHub Release asset
        if: startsWith(github.ref, 'refs/tags/v')
        env:
          GH_TOKEN: ${{ github.token }}
        shell: bash
        run: |
          set -euo pipefail
          TAG="${GITHUB_REF_NAME}"
          NOTES="Unity Windows client build for ${TAG}.

          Commit: ${GITHUB_SHA}
          Unity: ${UNITY_VERSION}
          Target: ${TARGET_PLATFORM}"

          if gh release view "${TAG}" >/dev/null 2>&1; then
            gh release upload "${TAG}" "${ZIP_NAME}" --clobber
          else
            gh release create "${TAG}" "${ZIP_NAME}" --title "The Unholy of Mention ${TAG}" --notes "${NOTES}"
          fi
```

- [ ] **Step 2: Validate workflow syntax locally**

Run: `Get-Content .github\workflows\unity-client-build.yml`

Expected: file exists and contains valid YAML indentation.

- [ ] **Step 3: Commit**

```bash
git add .github/workflows/unity-client-build.yml docs/superpowers/specs/2026-06-15-unity-client-release-pipeline-design.md docs/superpowers/plans/2026-06-15-unity-client-release-pipeline.md
git commit -m "ci: add Unity client release pipeline"
```

## Task 2: Verify in GitHub Actions

**Files:**
- No local file changes.

- [ ] **Step 1: Add required GitHub secrets**

Set these repository secrets:

```text
UNITY_LICENSE
UNITY_EMAIL
UNITY_PASSWORD
```

- [ ] **Step 2: Run manual workflow**

Run the `Unity Client Build` workflow with `workflow_dispatch`.

Expected: the workflow builds successfully and uploads a `The-Unholy-of-Mention-Windows-*` artifact.

- [ ] **Step 3: Test a release tag**

```bash
git tag v0.1.0-test
git push origin v0.1.0-test
```

Expected: the workflow creates or updates the `v0.1.0-test` GitHub Release and attaches the Windows zip.

- [ ] **Step 4: Download and launch the build**

Download the zip from GitHub Releases, extract it, and launch `The Unholy of Mention.exe`.

Expected: the application opens and reaches the main menu.

## Self-Review

- Spec coverage: The plan implements manual build, PR artifact build, and tag release publishing.
- Placeholder scan: No deferred implementation placeholders remain.
- Scope check: This plan only covers Windows client release automation and does not alter backend CD or WebGL hosting.
