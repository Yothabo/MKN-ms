#!/usr/bin/env python3
"""Traceability tool for the MKN-MS acceptance matrix.

Reads docs/processes/acceptance-matrix.md, parses every testable row,
parses every [Trait("MatrixRow", "...")] on a fact under
tests/MknMs.IntegrationTests/, and reports:

  - matrix rows with no corresponding fact (the coverage gap);
  - traits that name a matrix row that no longer exists (the drift
    signal);
  - which rows are covered by how many facts (informational).

Report-only at this phase. Exits 0 always. When the map is closed —
every row has a fact, every trait resolves to a row — the tool becomes
a CI gate (Phase C).

Row identity
------------

Each row's identity is "<process>:<hash>", where process is the row's
first column ("1.0", "3.0", "Temporal", "Deletion", ...) and hash is
the first 12 hex chars of SHA-256 over "<process>|<given>|<when>".

Rationale: no slug scheme is both short and human-readable for every
row. Several matrix rows share a long prefix of their Given text and
differ only in a middle substring (the four 4.0 eligibility rows); a
few rows have identical Given text and are distinguished only by their
When text (the 7.0 confirm/decline/sweep triple). A hash over
(Given, When) is unique across the matrix by construction.

The hash is opaque to a human reader. That is the tradeoff. Each trait
is preceded by a comment naming the row in human-readable form so that
a reviewer reading the test file sees the label, not the hash. The tool
does not verify the comment — it would be a second copy of the
identity, subject to its own drift. The tool verifies that the trait
resolves to a matrix row and that every matrix row is claimed by some
trait.

Trait shape
-----------

A fact binds to a matrix row with an xUnit trait, preceded by a
human-readable comment naming the row:

    // Matrix row 7.0: An assignment with AssignmentStatusID = NULL | Decline is received
    [Trait("MatrixRow", "7.0:a3f9c1d2e5b7")]

A fact may carry zero, one, or many MatrixRow traits. A fact with no
trait is a test that does not correspond to a matrix row; that is legal.

Running
-------

    python3 scripts/matrix_tool.py

Optional flags:

    --strict      Exit 1 on any orphaned row or orphaned trait.
                  Default is report-only.
    --row ID      Print one row's full text and its covering facts.
    --list        Print every row's identity and its covering fact
                  count, one per line.
"""

import argparse
import re
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
MATRIX = REPO_ROOT / "docs" / "processes" / "acceptance-matrix.md"
TESTS = REPO_ROOT / "backend" / "tests" / "MknMs.IntegrationTests"

import hashlib

IDENTITY_HASH_LEN = 12


def row_hash(process: str, given: str, when: str) -> str:
    payload = f"{process}|{given}|{when}".encode("utf-8")
    digest = hashlib.sha256(payload).hexdigest()
    return digest[:IDENTITY_HASH_LEN]


def parse_matrix() -> list:
    """Return a list of dicts, one per testable matrix row.

    Each dict carries: identity, process, given, when, then, lineno.
    """
    lines = MATRIX.read_text(encoding="utf-8").splitlines()
    rows = []
    seen = {}  # (process, slug) -> identity; used to detect collisions

    # Two table shapes are in play:
    #   - Process sections: | Process | Given | When | Then |
    #   - Cross-cutting:    | Area   | Given | When | Then |
    # Boundary conditions: | Decision | Boundary tested |  (2-column,
    # excluded from the map — these describe boundaries, not Given/
    # When/Then invariants).

    for i, line in enumerate(lines, 1):
        stripped = line.strip()
        if not stripped.startswith("|"):
            continue

        cells = [c.strip() for c in stripped.strip("|").split("|")]
        if len(cells) < 4:
            continue

        # Skip the header row of each table.
        if cells[0] in ("Process", "Area", "Decision"):
            continue

        # Skip the separator row.
        if cells[0].startswith("---") or cells[1].startswith("---"):
            continue

        process = cells[0]
        given = cells[1]
        when = cells[2]
        then = cells[3]

        # Empty-cell rows are malformed; report and skip.
        if not given or not when or not then:
            continue

        identity = f"{process}:{row_hash(process, given, when)}"
        if identity in seen:
            print(f"COLLISION at line {i}: identity '{identity}' already "
                  f"defined at line {seen[identity]}. The second occurrence "
                  f"is skipped; the first is authoritative.",
                  file=sys.stderr)
            continue
        seen[identity] = i

        rows.append({
            "identity": identity,
            "process": process,
            "given": given,
            "when": when,
            "then": then,
            "lineno": i,
        })

    return rows


def parse_traits() -> dict:
    """Return {identity: [(file, class, method), ...]}.

    Parsed by simple line scan: any [Trait("MatrixRow", "<value>")]
    line binds the enclosing class and next-declared method to the
    identity.
    """
    traits = {}
    trait_re = re.compile(r'\[Trait\("MatrixRow",\s*"([^"]+)"\)\]')
    method_re = re.compile(r"\bpublic\s+(?:async\s+)?(?:Task|void)\s+(\w+)\s*\(")
    class_re = re.compile(r"\bpublic\s+(?:sealed\s+)?class\s+(\w+)")

    for cs in sorted(TESTS.rglob("*.cs")):
        lines = cs.read_text(encoding="utf-8").splitlines()
        current_class = "?"
        for i, line in enumerate(lines):
            m_class = class_re.search(line)
            if m_class:
                current_class = m_class.group(1)
                continue

            m_trait = trait_re.search(line)
            if not m_trait:
                continue

            identity = m_trait.group(1)

            # Find the next method name on a subsequent line.
            method = "?"
            for j in range(i + 1, min(i + 20, len(lines))):
                m_method = method_re.search(lines[j])
                if m_method:
                    method = m_method.group(1)
                    break

            traits.setdefault(identity, []).append(
                (cs.relative_to(REPO_ROOT), current_class, method))

    return traits


def report(strict: bool) -> int:
    matrix = parse_matrix()
    traits = parse_traits()

    matrix_by_id = {r["identity"]: r for r in matrix}

    orphans_row = [r for r in matrix if r["identity"] not in traits]
    orphans_trait = [t for t in traits if t not in matrix_by_id]

    # Sort for stable output.
    orphans_row.sort(key=lambda r: (r["process"], r["identity"]))
    orphans_trait.sort()

    print(f"Matrix rows:       {len(matrix)}")
    print(f"Distinct traits:   {len(traits)}")
    print(f"Rows without fact: {len(orphans_row)}")
    print(f"Traits without row:{len(orphans_trait)}")
    print()

    if orphans_trait:
        print("Traits that name a row not in the matrix:")
        for t in orphans_trait:
            print(f"  {t}")
            for (path, cls, method) in traits[t]:
                print(f"    {path}::{cls}.{method}")
        print()

    if orphans_row:
        print("Matrix rows with no corresponding fact:")
        by_process = {}
        for r in orphans_row:
            by_process.setdefault(r["process"], []).append(r)
        for process in sorted(by_process, key=lambda p: _process_sort_key(p)):
            rows = by_process[process]
            print(f"  {process} ({len(rows)} row(s) without fact)")
            for r in rows:
                print(f"    {r['identity']}")
                print(f"      Given: {r['given'][:80]}")
                print(f"      Then:  {r['then'][:80]}")
        print()

    if orphans_row or orphans_trait:
        print(f"Run --strict to fail on any orphan.")
    else:
        print("Map is closed — every matrix row has a fact and every "
              "trait resolves to a row.")

    if strict and (orphans_row or orphans_trait):
        return 1
    return 0


def _process_sort_key(process: str) -> tuple:
    """Sort processes numerically when possible, alphabetically otherwise."""
    if re.match(r"^\d+\.\d+$", process):
        major, minor = process.split(".")
        return (0, int(major), int(minor), "")
    return (1, 0, 0, process)


def show_row(identity: str) -> int:
    matrix = parse_matrix()
    traits = parse_traits()
    row = next((r for r in matrix if r["identity"] == identity), None)
    if row is None:
        print(f"Row '{identity}' not found.", file=sys.stderr)
        return 1

    print(f"Identity: {row['identity']}")
    print(f"Process:  {row['process']}")
    print(f"Line:     {row['lineno']}")
    print(f"Given:    {row['given']}")
    print(f"When:     {row['when']}")
    print(f"Then:     {row['then']}")
    print()
    covering = traits.get(identity, [])
    if not covering:
        print("Covered by: (no fact)")
    else:
        print(f"Covered by {len(covering)} fact(s):")
        for (path, cls, method) in covering:
            print(f"  {path}::{cls}.{method}")
    return 0


def list_rows() -> int:
    matrix = parse_matrix()
    traits = parse_traits()
    for r in matrix:
        count = len(traits.get(r["identity"], []))
        print(f"{r['identity']}\t{count}")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--strict", action="store_true",
                        help="Exit 1 on any orphaned row or trait.")
    parser.add_argument("--row", metavar="IDENTITY",
                        help="Show one row's full text and its covering facts.")
    parser.add_argument("--list", action="store_true",
                        help="List every row's identity and covering-fact count.")
    args = parser.parse_args()

    if not MATRIX.exists():
        print(f"ERROR: matrix not found at {MATRIX}. Run from repository root.",
              file=sys.stderr)
        return 2

    if args.row:
        return show_row(args.row)
    if args.list:
        return list_rows()
    return report(args.strict)


if __name__ == "__main__":
    sys.exit(main())
