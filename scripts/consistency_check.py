#!/usr/bin/env python3
"""Consistency check for the MKN-MS documentation tree.

Scans every markdown file under docs/ for stale references to counts and
invocation-edge descriptions that have drifted since the last architectural
change, for decision labels that are used but not defined, and for the seven
categories of drift the manual contradiction scan covers. Exits 0 if clean,
1 if any violation is found.

Run from the repository root:

    python3 scripts/consistency_check.py

The check is deliberately pattern-based. It catches the actual forms that
have drifted historically — process counts, edge counts, section ranges,
decision-label usage, store-footprint letters — regardless of surrounding
context. New patterns are added as new drift risks are identified.
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
    (re.compile(r"\beleven processes\b", re.IGNORECASE), "eleven processes (should be thirteen)"),
    (re.compile(r"\btwelve processes\b", re.IGNORECASE), "twelve processes (should be thirteen)"),
    (re.compile(r"\bsix processes\b", re.IGNORECASE), "six processes (operations should be eight)"),
    (re.compile(r"\bseven processes\b", re.IGNORECASE), "seven processes (operations should be eight)"),

    # Invocation edge counts and descriptions
    (re.compile(r"\btwo direct invocation edges\b", re.IGNORECASE), "two direct invocation edges (should be four)"),
    (re.compile(r"\btwo invocation edges\b", re.IGNORECASE), "two invocation edges (should be four)"),
    (re.compile(r"\btwo locked edges\b", re.IGNORECASE), "two locked edges (should be four)"),
    (re.compile(r"\btwo locked invocation edges\b", re.IGNORECASE), "two locked invocation edges (should be four)"),
    (re.compile(r"\btwo process-to-process invocation edges\b", re.IGNORECASE), "two process-to-process invocation edges (should be four)"),
    (re.compile(r"\btwo process-dependency edges\b", re.IGNORECASE), "two process-dependency edges (should be four)"),
    (re.compile(r"\bThe two invocation edges\b"), "The two invocation edges (should be four)"),
    (re.compile(r"\bthe two internal invocation edges\b", re.IGNORECASE), "the two internal invocation edges (should be four)"),
    (re.compile(r"\bIt states the two \u2014 and only two \u2014 direct process-invocation edges\b"), "two-and-only-two direct edges (should be four)"),
    (re.compile(r"\bthree invocation\b", re.IGNORECASE), "three invocation (should be four)"),
    (re.compile(r"\bthree direct\b", re.IGNORECASE), "three direct (should be four)"),
    (re.compile(r"\bthree edges\b", re.IGNORECASE), "three edges (should be four)"),
    (re.compile(r"\bthree locked\b", re.IGNORECASE), "three locked (should be four)"),
    (re.compile(r"\bthree process-to-process\b", re.IGNORECASE), "three process-to-process (should be four)"),
    (re.compile(r"\bthree solid\b", re.IGNORECASE), "three solid (should be four)"),
    (re.compile(r"\bAll three invoke\b"), "All three invoke (should be four)"),

    # Specific 9.0 invocation descriptions
    (re.compile(r"Only 9\.0 has no independent trigger \u2014 it exists solely to be invoked by 5\.0 and 7\.0"), "trigger summary lists 5.0 and 7.0 only"),
    (re.compile(r"\bInvocation by 5\.0 or 7\.0\b"), "trigger table lists 5.0 and 7.0 only"),
    (re.compile(r"\bInvoked by exactly two processes\b"), "invoked by exactly two processes"),
    (re.compile(r"\bInvoked only by 5\.0 and 7\.0\b"), "invoked only by 5.0 and 7.0"),
    (re.compile(r"\bInvoked by 5\.0 and 7\.0\b"), "invoked by 5.0 and 7.0"),
    (re.compile(r"\bBoth invoke 9\.0\b"), "both invoke 9.0"),
    (re.compile(r"\binvoked by the two processes\b", re.IGNORECASE), "invoked by the two processes"),
    (re.compile(r"\bthe two processes that create assignments\b", re.IGNORECASE), "the two processes that create assignments"),
    (re.compile(r"\bthe two processes that produce outbound-worthy events\b", re.IGNORECASE), "the two processes that produce outbound-worthy events"),

    # Section ranges
    (re.compile(r"Only two solid arrows"), "Mermaid legend says 'Only two solid arrows'"),
    (re.compile(r"Confirmation of the two-edge claim"), "confirmation block says 'two-edge claim'"),
    (re.compile(r"\u00a71 through \u00a716\b"), "\u00a71 through \u00a716 (spec now reaches \u00a717)"),
    (re.compile(r"\u00a78\u2013\u00a714 derive\b"), "\u00a78\u2013\u00a714 derive (spec now reaches \u00a717)"),
    (re.compile(r"\u00a78\u2013\u00a714\b"), "\u00a78\u2013\u00a714 (spec now reaches \u00a717)"),
]

# Additional patterns with per-line exclusions. Each entry is
# (pattern, label, exclusion_regex_or_None). If exclusion matches the same
# line, the pattern is not considered a violation for that line.
STALE_PATTERNS_WITH_EXCLUSIONS = [
    # Store-footprint phrasing that has drifted on 9.0 and 10.0
    (re.compile(r"does not write to any store"),
     "9.0 'does not write to any store' (Path 2 writes NotifiedAt)",
     re.compile(r"on this path")),
    (re.compile(r"Read-only on all stores"),
     "9.0 'Read-only on all stores' (Path 2 writes NotifiedAt)",
     None),
    (re.compile(r"only product is a message sent to a member"),
     "9.0 'only product is a message sent to a member' (two-path model)",
     None),

    # Readmission-write drift
    (re.compile(r"13\.0.*writes Readmission"),
     "13.0 writes Readmission (it reads; 3.0 owns creation)",
     re.compile(r"does not write|never writes")),
    (re.compile(r"engine writes Readmission"),
     "engine writes Readmission (it reads; 3.0 owns creation)",
     re.compile(r"does not write|never writes")),
    (re.compile(r"IncrementReadmissionCount"),
     "IncrementReadmissionCount (removed by B12)",
     re.compile(r"absent|removed|no such|There is no|never|does not")),

    # Authority-notification marked-state drift
    (re.compile(r"audit row is still marked"),
     "audit row is still marked (NotifiedAt is the marker)",
     None),
    (re.compile(r"still marked as notified"),
     "still marked as notified (NotifiedAt is the marker)",
     None),

    # Removed-column references
    (re.compile(r"EventDuty\.Label"),
     "EventDuty.Label (removed in the A\u2013G amendment set)",
     re.compile(r"no longer exists|pre-amendment|removed|dropped|15\.8|Drop|Migrate")),
    (re.compile(r"Event\.Location"),
     "Event.Location (removed in the A\u2013G amendment set)",
     re.compile(r"no longer exists|pre-amendment|removed|dropped|15\.8|Drop|Migrate")),
    (re.compile(r"Member\.IsActive"),
     "Member.IsActive (removed in the A\u2013G amendment set)",
     re.compile(r"no longer exists|pre-amendment|removed|dropped|15\.8|Drop|Migrate")),
]

# The root of the documentation tree, relative to the repository root.
# The script assumes it is run from the repository root.
DOCS_ROOT = Path("docs")
DECISIONS_FILE = DOCS_ROOT / "decisions.md"


def scan() -> list:
    """Return a list of (path, line_number, label, line_text) tuples for every violation."""
    violations = []
    for md in sorted(DOCS_ROOT.rglob("*.md")):
        if ".git" in md.parts:
            continue
        try:
            lines = md.read_text(encoding="utf-8").splitlines()
        except UnicodeDecodeError:
            continue
        for i, line in enumerate(lines, 1):
            for pattern, label in STALE_PATTERNS:
                if pattern.search(line):
                    violations.append((md, i, label, line.strip()[:140]))
            # decisions.md states what the decisions are, including the
            # names the decisions removed. It is a definition file, not a
            # reference. Skip it for the seven-dimension patterns.
            if md.name == "decisions.md":
                continue
            for pattern, label, exclusion in STALE_PATTERNS_WITH_EXCLUSIONS:
                if pattern.search(line):
                    if exclusion is not None and exclusion.search(line):
                        continue
                    violations.append((md, i, label, line.strip()[:140]))
    return violations


def collect_defined_labels() -> set:
    """Read docs/decisions.md and return the set of defined labels (e.g. B1, C4)."""
    if not DECISIONS_FILE.exists():
        return set()
    text = DECISIONS_FILE.read_text(encoding="utf-8")
    labels = set()
    for m in re.finditer(r"^\|\s*([BC]\d{1,2})\s*\|", text, re.MULTILINE):
        labels.add(m.group(1))
    return labels


def scan_decision_labels(defined: set) -> list:
    """Return (path, line, label, text) for any B<n> or C<n> reference whose
    label is not defined in docs/decisions.md. Lines in decisions.md itself
    and the definition headers in contracts.md are excluded."""
    label_pattern = re.compile(r"\b([BC])(\d{1,2})\b")
    violations = []
    for md in sorted(DOCS_ROOT.rglob("*.md")):
        if ".git" in md.parts:
            continue
        try:
            lines = md.read_text(encoding="utf-8").splitlines()
        except UnicodeDecodeError:
            continue
        for i, line in enumerate(lines, 1):
            # Skip the decisions.md definitions themselves.
            if md == DECISIONS_FILE:
                continue
            # Skip section headers of the form "## C4 — ...", which are
            # definitions, not references.
            stripped = line.strip()
            if stripped.startswith("## C") or stripped.startswith("# C"):
                continue
            for m in label_pattern.finditer(line):
                label = m.group(0)
                if label not in defined:
                    violations.append((md, i, f"undefined decision label {label}", line.strip()[:140]))
    return violations


def main() -> int:
    if not DOCS_ROOT.exists():
        print(f"ERROR: docs/ not found at {DOCS_ROOT}. Run this script from the repository root.")
        return 2

    violations = scan()

    defined_labels = collect_defined_labels()
    violations.extend(scan_decision_labels(defined_labels))

    if not violations:
        print("\u2713 No stale references found. Repository is consistent.")
        return 0

    print(f"\u2717 {len(violations)} stale reference(s) found:\n")
    for path, lineno, label, snippet in violations:
        print(f"  {path}:{lineno}  [{label}]")
        print(f"    {snippet}")
    print()
    print("Fix the stale references, then re-run this check.")
    return 1


if __name__ == "__main__":
    sys.exit(main())
