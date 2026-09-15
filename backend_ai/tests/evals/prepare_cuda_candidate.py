"""Explicit developer download for Gate 1; does not enable the game runtime."""
from __future__ import annotations

import argparse
import hashlib
import json
import shutil
import urllib.request
import zipfile
from pathlib import Path


def prepare(destination: Path) -> None:
    manifest = json.loads((Path(__file__).parents[2] / "data/cuda_candidate_manifest.json").read_text())
    destination.mkdir(parents=True, exist_ok=True)
    for artifact in manifest["artifacts"]:
        path = destination / artifact["file"]
        if not path.exists():
            print(f"Downloading {path.name}: {artifact['bytes']} bytes", flush=True)
            temporary = path.with_suffix(path.suffix + ".part")
            request = urllib.request.Request(
                artifact["url"],
                headers={"User-Agent": "newCapstone-gate1/1.0"},
            )
            with urllib.request.urlopen(request, timeout=300) as response, temporary.open("wb") as output:
                shutil.copyfileobj(response, output, 1024 * 1024)
            temporary.replace(path)
        digest = hashlib.sha256()
        with path.open("rb") as stream:
            for block in iter(lambda: stream.read(8 * 1024 * 1024), b""):
                digest.update(block)
        if path.stat().st_size != artifact["bytes"] or digest.hexdigest() != artifact["sha256"]:
            raise RuntimeError(f"Artifact verification failed: {path.name}")
        print(f"SHA256 verified: {path.name}", flush=True)
        if path.suffix == ".zip":
            with zipfile.ZipFile(path) as archive:
                for member in archive.infolist():
                    target = (destination / member.filename).resolve()
                    if not target.is_relative_to(destination.resolve()):
                        raise RuntimeError("Archive path escapes destination")
                archive.extractall(destination)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--destination", type=Path, required=True)
    parser.add_argument("--accept-download-and-licenses", action="store_true", required=True)
    args = parser.parse_args()
    prepare(args.destination)
