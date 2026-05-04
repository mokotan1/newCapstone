#!/usr/bin/env python3
"""Minimal static server for Unity WebGL with precompressed .br assets."""
from __future__ import annotations

import argparse
import http.server
import os
import socketserver


def main() -> None:
    parser = argparse.ArgumentParser()
    default_dir = os.path.normpath(
        os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "disputatio", "Builds", "WebGL")
    )
    parser.add_argument(
        "--directory",
        default=default_dir,
        help="Path to WebGL build root (folder containing index.html)",
    )
    parser.add_argument("--port", type=int, default=8765)
    args = parser.parse_args()
    root = os.path.abspath(args.directory)

    class Handler(http.server.SimpleHTTPRequestHandler):
        def __init__(self, *a, **kw):
            super().__init__(*a, directory=root, **kw)

        def guess_type(self, path: str) -> tuple[str | None, str | None]:
            lp = path.lower()
            if lp.endswith(".wasm.br"):
                return "application/wasm", None
            if lp.endswith(".js.br"):
                return "application/javascript", None
            if lp.endswith(".data.br"):
                return "application/octet-stream", None
            return super().guess_type(path)

        def end_headers(self) -> None:
            path_only = self.path.split("?", 1)[0]
            if path_only.endswith(".br"):
                self.send_header("Content-Encoding", "br")
            super().end_headers()

    socketserver.TCPServer.allow_reuse_address = True
    with socketserver.TCPServer(("", args.port), Handler) as httpd:
        print(f"Serving WebGL from {root} at http://127.0.0.1:{args.port}/")
        httpd.serve_forever()


if __name__ == "__main__":
    main()
