#!/usr/bin/env python3
"""Check LibmemCli's declared public C++/CLI API against a committed baseline."""

from __future__ import annotations

import argparse
import difflib
from pathlib import Path
import re
import sys


def normalize(text: str) -> str:
    return re.sub(r"\s+", " ", text).strip()


def public_api_lines(header_text: str) -> list[str]:
    text = re.sub(r"/\*.*?\*/", "", header_text, flags=re.S)
    lines = []
    for raw in text.splitlines():
        raw = re.sub(r"//.*$", "", raw).strip()
        if raw:
            lines.append(raw)

    result: list[str] = []
    i = 0
    while i < len(lines):
        line = lines[i]

        if (
            (line.startswith("public enum class ") or line.startswith("[Flags] public enum class "))
            and "{" in line
        ):
            block = line
            while "};" not in block:
                i += 1
                if i >= len(lines):
                    raise ValueError("Unterminated public enum declaration.")
                block += " " + lines[i]

            open_brace = block.index("{")
            close_brace = block.rfind("}")
            result.append("TYPE " + normalize(block[:open_brace]))
            for item in block[open_brace + 1 : close_brace].split(","):
                item = normalize(item)
                if item:
                    result.append("  " + item)
            i += 1
            continue

        if line.startswith("public ref class ") and "{" in line:
            result.append("TYPE " + normalize(line[: line.index("{")]))
            access = "private"
            i += 1
            while i < len(lines):
                current = lines[i]
                if current == "};":
                    break
                if current in {"public:", "private:", "internal:"}:
                    access = current[:-1]
                elif access == "public" and (
                    current.startswith("property ") or current.endswith(";")
                ):
                    result.append("  " + normalize(current))
                i += 1
            if i >= len(lines):
                raise ValueError("Unterminated public ref class declaration.")
            i += 1
            continue

        i += 1

    return result


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--header", type=Path, default=Path("src/LibmemCli.h"))
    parser.add_argument(
        "--baseline",
        type=Path,
        default=Path("api/LibmemCli.PublicApi.txt"),
    )
    parser.add_argument("--write", action="store_true")
    args = parser.parse_args()

    current = public_api_lines(args.header.read_text(encoding="utf-8"))
    rendered = "\n".join(
        [
            "# LibmemCli public API baseline",
            "# Generated from src/LibmemCli.h. Intentional public API changes must update this file and CHANGELOG.md.",
            *current,
            "",
        ]
    )

    if args.write:
        args.baseline.parent.mkdir(parents=True, exist_ok=True)
        args.baseline.write_text(rendered, encoding="utf-8", newline="\n")
        print(f"WROTE PUBLIC API BASELINE: {args.baseline}")
        return 0

    expected = args.baseline.read_text(encoding="utf-8")
    if expected == rendered:
        print(f"PASS public API baseline: {len(current)} entries")
        return 0

    diff = difflib.unified_diff(
        expected.splitlines(),
        rendered.splitlines(),
        fromfile=str(args.baseline),
        tofile=str(args.header),
        lineterm="",
    )
    print("PUBLIC API BASELINE MISMATCH", file=sys.stderr)
    print("\n".join(diff), file=sys.stderr)
    return 1


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError) as exc:
        print(f"PUBLIC API CHECK FAIL: {exc}", file=sys.stderr)
        raise SystemExit(1)
