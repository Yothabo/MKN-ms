#!/usr/bin/env python3
"""Bundle every text file in the repository into codebase.txt at the root.

Walks the repo tree, excludes VCS metadata, build artifacts, dependency
directories, and the output file itself. Writes a single codebase.txt that
contains every text file, each preceded by a header with its relative path.
"""

import os
import sys
from pathlib import Path

EXCLUDE_DIRS = {
    ".git",
    "node_modules",
    "dist",
    "build",
    "out",
    "bin",
    "obj",
    ".next",
    ".nuxt",
    ".vite",
    "coverage",
    ".pytest_cache",
    "__pycache__",
    ".venv",
    "venv",
    "env",
    ".idea",
    ".vscode",
    ".vs",
    "target",
    ".cache",
}

EXCLUDE_FILES = {
    "codebase.txt",
    "bundle.py",
    ".DS_Store",
    "Thumbs.db",
}

EXCLUDE_EXTENSIONS = {
    ".pyc",
    ".pyo",
    ".pyd",
    ".so",
    ".dll",
    ".exe",
    ".bin",
    ".jpg",
    ".jpeg",
    ".png",
    ".gif",
    ".ico",
    ".svg",
    ".webp",
    ".pdf",
    ".zip",
    ".tar",
    ".gz",
    ".7z",
    ".rar",
    ".woff",
    ".woff2",
    ".ttf",
    ".eot",
    ".mp3",
    ".mp4",
    ".mov",
    ".avi",
}

MAX_FILE_SIZE_BYTES = 2 * 1024 * 1024  # 2 MB safety cap per file


def is_text_file(path: Path) -> bool:
    if path.suffix.lower() in EXCLUDE_EXTENSIONS:
        return False
    if path.name in EXCLUDE_FILES:
        return False
    try:
        if path.stat().st_size > MAX_FILE_SIZE_BYTES:
            return False
        with path.open("rb") as f:
            chunk = f.read(8192)
        if b"\x00" in chunk:
            return False
        chunk.decode("utf-8")
        return True
    except (UnicodeDecodeError, OSError):
        return False


def walk(root: Path):
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [
            d for d in dirnames
            if d not in EXCLUDE_DIRS and not d.startswith(".")
        ]
        for name in sorted(filenames):
            p = Path(dirpath) / name
            if is_text_file(p):
                yield p


def main():
    root = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else Path.cwd().resolve()
    output = root / "codebase.txt"

    files = sorted(walk(root), key=lambda p: str(p.relative_to(root)))

    total_lines = 0
    total_files = 0

    with output.open("w", encoding="utf-8", newline="\n") as out:
        for path in files:
            rel = path.relative_to(root)
            try:
                text = path.read_text(encoding="utf-8")
            except (UnicodeDecodeError, OSError):
                continue

            line_count = text.count("\n") + (0 if text.endswith("\n") else 1)
            total_lines += line_count
            total_files += 1

            out.write(f"{'=' * 78}\n")
            out.write(f"FILE: {rel}\n")
            out.write(f"LINES: {line_count}\n")
            out.write(f"{'=' * 78}\n\n")
            out.write(text)
            if not text.endswith("\n"):
                out.write("\n")
            out.write("\n")

    print(f"Wrote {output}")
    print(f"Files: {total_files}")
    print(f"Lines: {total_lines}")
    print(f"Size:  {output.stat().st_size:,} bytes")


if __name__ == "__main__":
    main()
