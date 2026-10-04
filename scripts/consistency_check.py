#!/usr/bin/env python3
"""Consistency check for the MKN-MS documentation tree.

Scans every markdown file under docs/ for stale references to counts and
invocation-edge descriptions that have drifted since the last architectural
change. Exits 0 if clean, 1 if any violation is found.

Run from the repository root:

    python3 scripts/consistency_check.py

The check is deliberately pattern-based. It catches the actual forms that
have drifted historically — process counts, edge counts, section ranges —
regardless of surrounding context. New patterns are added to STALE_PATTERNS
as new drift risks are identified.
"""

import re
import sys
from pathlib import Path

# The list of stale patterns.
# Each entry is (regex, human-readable label). When a pattern matches any
# line in any markdown file under docs/, the check fails and reports the
# file, line number, and matched text.
STALE_PATTERNS = [
    # Process counts
    (re.compile(r"\beleven processes\b", re.IGNORECASE), "eleven processes (should be twelve)"),
    (re.compile(r"\bsix processes\b", re.IGNORECASE), "six processes (operations should be seven)"),

    # Invocation edge counts and descriptions
    (re.compile(r"\btwo direct invocation edges\b", re.IGNORECASE), "two direct invocation edges (should be three)"),
    (re.compile(r"\btwo invocation edges\b", re.IGNORECASE), "two invocation edges (should be three)"),
    (re.compile(r"\btwo locked edges\b", re.IGNORECASE), "two locked edges (should be three)"),
    (re.compile(r"\btwo locked invocation edges\b", re.IGNORECASE), "two locked invocation edges (should be three)"),
    (re.compile(r"\btwo process-to-process invocation edges\b", re.IGNORECASE), "two process-to-process invocation edges (should be three)"),
    (re.compile(r"\btwo process-dependency edges\b", re.IGNORECASE), "two process-dependency edges (should be three)"),
    (re.compile(r"\bThe two invocation edges\b"), "The two invocation edges (should be three)"),
    (re.compile(r"\bthe two internal invocation edges\b", re.IGNORECASE), "the two internal invocation edges (should be three)"),
    (re.compile(r"\bIt states the two — and only two — direct process-invocation edges\b"), "two-and-only-two direct edges (should be three)"),

    # Specific 9.0 invocation descriptions
    (re.compile(r"Only 9\.0 has no independent trigger — it exists solely to be invoked by 5\.0 and 7\.0"), "trigger summary lists 5.0 and 7.0 only"),
    (re.compile(r"\bInvocation by 5\.0 or 7\.0\b"), "trigger table lists 5.0 and 7.0 only"),
    (re.compile(r"\bInvoked by exactly two processes\b"), "invoked by exactly two processes"),
    (re.compile(r"\bInvoked only by 5\.0 and 7\.0\b"), "invoked only by 5.0 and 7.0"),
    (re.compile(r"\bInvoked by 5\.0 and 7\.0\b"), "invoked by 5.0 and 7.0"),
    (re.compile(r"\bBoth invoke 9\.0\b"), "both invoke 9.0"),
    (re.compile(r"\binvoked by the two processes\b", re.IGNORECASE), "invoked by the two processes"),
    (re.compile(r"\bthe two processes that create assignments\b", re.IGNORECASE), "the two processes that create assignments"),
    (re.compile(r"\bthe two processes that produce outbound-worthy events\b", re.IGNORECASE), "the two processes that produce outbound-worthy events"),

    # Section ranges
    (re.compile(r"\b§1 through §16\b"), "§1 through §16 (spec now reaches §17)"),
    (re.compile(r"\b§8–§14 derive\b"), "§8–§14 derive (spec now reaches §17)"),
    (re.compile(r"\b§8–§14\b"), "§8–§14 (spec now reaches §17)"),
]

# The root of the documentation tree, relative to the repository root.
# The script assumes it is run from the repository root.
DOCS_ROOT = Path("docs")


def scan() -> list:
    """Return a list of (path, line_number, label, line_text) tuples for every violation."""
    violations = []
    for md in sorted(DOCS_ROOT.rglob("*.md")):
        # Skip anything inside a .git directory (shouldn't happen, but defensive).
        if ".git" in md.parts:
            continue
        try:
            lines = md.read_text(encoding="utf-8").splitlines()
        except UnicodeDecodeError:
            # Not UTF-8; skip silently. The docs tree is expected to be UTF-8.
            continue
        for i, line in enumerate(lines, 1):
            for pattern, label in STALE_PATTERNS:
                if pattern.search(line):
                    violations.append((md, i, label, line.strip()[:140]))
    return violations


def main() -> int:
    if not DOCS_ROOT.exists():
        print(f"ERROR: docs/ not found at {DOCS_ROOT}. Run this script from the repository root.")
        return 2

    violations = scan()

    if not violations:
        print("✓ No stale references found. Repository is consistent.")
        return 0

    print(f"✗ {len(violations)} stale reference(s) found:\n")
    for path, lineno, label, snippet in violations:
        print(f"  {path}:{lineno}  [{label}]")
        print(f"    {snippet}")
    print()
    print("Fix the stale references, then re-run this check.")
    return 1


if __name__ == "__main__":
    sys.exit(main())
