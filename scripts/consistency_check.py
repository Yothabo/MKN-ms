#!/usr/bin/env python3
"""Consistency check for the MKN-MS documentation tree.

Scans every markdown file under docs/ for stale references to counts and
invocation-edge descriptions that have drifted since the last architectural
change, for decision labels that are used but not defined, and for the seven
categories of drift the manual contradiction scan covers. Exits 0 if clean,
1 if any violation is found.

Run from the repository root:

    python3 scripts/consistency_check.py

The check runs in two parts.

First, a pattern-based scan. It catches the actual forms that have drifted
historically — process counts, edge counts, section ranges, decision-label
usage, store-footprint letters — regardless of surrounding context. New
patterns are added as new drift risks are identified.

Second, an amendment-register scan. It checks the tree against the terms
the specification's amendment register names. The term table is
hand-maintained from §15.1-§15.4 and §15.8 of the specification. When the
register gains a row, the table gains a row; when the register amends a
row, the table is amended. The scan is checked against the table, not
against the register text; the table is checked against the register by a
human at the time of the amendment.

Rows in the register that describe a rule or a relationship rather than a
term cannot be checked by keyword. They are listed in
AMENDMENT_REGISTER_UNVERIFIABLE and printed by the check so that a human
verifies them when the register changes.
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

# --------------------------------------------------------------------------
# The amendment register — hand-maintained term table
# --------------------------------------------------------------------------
#
# Source of truth: system-design-spec.md §15.1-§15.4 and §15.8.
#
# The register stores sentences about terms, not terms themselves. This
# table is the human reading of those sentences, reduced to (term,
# polarity, source) triples. When the register gains a row, this table
# gains a row; when the register amends a row, this table is amended.
# The scan is checked against this table, not against the register text.
#
# Polarity:
#   "removed" — the term must not appear in the tree except in an explicit
#               removal context (see REMOVAL_CONTEXT_PHRASES).
#   "added"   — the term must appear at least once in the tree.
#   "setting" — the term must appear in docs/database/settings.md and in
#               the process document that consumes it (SETTING_CONSUMERS).
#
AMENDMENT_REGISTER_TERMS = [
    # §15.8.1
    ("Member.IsActive", "removed", "§15.8.1"),
    ("MemberStatus", "added", "§15.8.1"),
    ("Member.MemberStatusID", "added", "§15.8.1"),
    # §15.8.2
    ("Member.ReceiptNumber", "added", "§15.8.2"),
    ("Member.CardNumber", "added", "§15.8.2"),
    # §15.8.3
    ("Member.JoinReason", "added", "§15.8.3"),
    # §15.8.4
    ("AttributeType", "added", "§15.8.4"),
    # §15.8.5
    ("MemberAttributeValue", "added", "§15.8.5"),
    # §15.8.6
    ("DutyRule.ServiceDefID", "added", "§15.8.6"),
    # §15.8.8
    ("Event.Location", "removed", "§15.8.8"),
    ("Event.HostBranchID", "added", "§15.8.8"),
    # §15.8.9
    ("EventBranch", "added", "§15.8.9"),
    # §15.8.10
    ("EventDuty.Label", "removed", "§15.8.10"),
    ("EventDuty.DutyID", "added", "§15.8.10"),
    ("EventDuty.ServiceDefID", "added", "§15.8.10"),
    # §15.8.11
    ("ProgramItem.Location", "added", "§15.8.11"),
    # §15.8.13
    ("Role.IsDefault", "added", "§15.8.13"),
    # §15.8.14
    ("ConfigurationAuditLog", "added", "§15.8.14"),
    # §15.8.15
    ("EntityDeletionPolicy", "added", "§15.8.15"),
    # §15.8.16
    ("NotificationSubscription", "added", "§15.8.16"),
    # §15.8.17
    ("Capability", "added", "§15.8.17"),
    # §15.8.19
    ("AttendanceRecord.Source", "added", "§15.8.19"),
    # §15.8.20
    ("Branch.UsesAttendanceRegister", "added", "§15.8.20"),
    ("Event.UsesAttendanceRegister", "added", "§15.8.20"),
    # §15.8.21
    ("AttendanceRule", "added", "§15.8.21"),
    # §15.8.22
    ("AttendanceRuleScope", "added", "§15.8.22"),
    # §15.8.23
    ("Readmission", "added", "§15.8.23"),
    # §15.8.27
    ("Admin.IsActive", "added", "§15.8.27"),
    ("Member.IsDeleted", "removed", "§15.8.27"),
    # §15.3.1-§15.3.8
    ("OccurrenceHorizonDays", "setting", "§15.3.1"),
    ("InitialAssignmentStatusID", "setting", "§15.3.2"),
    ("ConfirmationTimeoutHours", "setting", "§15.3.3"),
    ("OutcomeStateUnfilledID", "setting", "§15.3.4"),
    ("OutcomeStatePartiallyFilledID", "setting", "§15.3.5"),
    ("OutcomeStateFilledID", "setting", "§15.3.6"),
    ("OutcomeStateCancelledID", "setting", "§15.3.7"),
    ("NotificationChannel", "setting", "§15.3.8"),
    # §15.8.32
    ("DeclinedStatusID", "setting", "§15.8.32"),
    ("TimedOutStatusID", "setting", "§15.8.32"),
    ("ApplicationTimeZone", "setting", "§15.8.32"),
    ("AttendanceRegisterEnabled", "setting", "§15.8.32"),
    # §15.8.18
    ("ReceiptToCardDurationDays", "setting", "§15.8.18"),
    ("YouthAgeMin", "setting", "§15.8.18"),
    ("YouthAgeMax", "setting", "§15.8.18"),
]

# Register rows that describe a rule or a relationship rather than a term.
# A keyword scan cannot check these. They are printed by the check so a
# human verifies them when the register changes.
AMENDMENT_REGISTER_UNVERIFIABLE = [
    ("§15.8.7",  "MemberAttribute and Youth criteria types — named in the register; the criteria's documented names are 'Member Attribute' and 'Youth'"),
    ("§15.8.12", "IsDeleted on every configuration entity — a structural property, not a term"),
    ("§15.8.24", "13.0 Attendance Rule Engine — a process, not a term"),
    ("§15.8.25", "Attendance register scope resolution — a rule stated in §9.6"),
    ("§15.8.26", "Branch-Attendance Recency skips when register off — a rule stated in §10.1.5"),
    ("§15.8.27", "Member has no flags — the term Member.IsDeleted is checked; the structural property itself is human-verified"),
    ("§15.8.28", "Member Status criterion — a criterion type, checked against the §5 vocabulary by a human"),
    ("§15.8.29", "Member register scope and event scope fallback — a rule stated in §9.6"),
    ("§15.8.31", "13.0 process contract items — prose describing behaviour, not terms"),
    ("§15.8.33", "ApplicationTimeZone required — the setting is checked; the required-ness rule is human-verified"),
    ("§15.8.34", "Attendance Rule ownership commands — process commands, not terms"),
    ("§15.8.35", "Specification stale text — a remediation note, not a term"),
    ("§15.8.36", "API reconciliation — a remediation note, not a term"),
]

# For a "setting" term, the process document that consumes it. The scan
# confirms the setting appears in both settings.md and in this document.
SETTING_CONSUMERS = {
    "OccurrenceHorizonDays":           "docs/processes/operations/11.0-materialize-occurrences.md",
    "InitialAssignmentStatusID":       "docs/processes/operations/7.0-manage-confirmation.md",
    "ConfirmationTimeoutHours":        "docs/processes/operations/7.0-manage-confirmation.md",
    "OutcomeStateUnfilledID":          "docs/processes/operations/10.0-evaluate-fill-status.md",
    "OutcomeStatePartiallyFilledID":   "docs/processes/operations/10.0-evaluate-fill-status.md",
    "OutcomeStateFilledID":            "docs/processes/operations/10.0-evaluate-fill-status.md",
    "OutcomeStateCancelledID":         "docs/processes/operations/10.0-evaluate-fill-status.md",
    "NotificationChannel":             "docs/processes/operations/9.0-dispatch-notification.md",
    "DeclinedStatusID":                "docs/processes/operations/7.0-manage-confirmation.md",
    "TimedOutStatusID":                "docs/processes/operations/7.0-manage-confirmation.md",
    "ApplicationTimeZone":             "docs/processes/operations/11.0-materialize-occurrences.md",
    "AttendanceRegisterEnabled":       "docs/processes/operations/13.0-attendance-rule-engine.md",
    "ReceiptToCardDurationDays":       "docs/processes/configuration/3.0-manage-membership.md",
    "YouthAgeMin":                     "docs/processes/operations/5.0-generate-assignment.md",
    "YouthAgeMax":                     "docs/processes/operations/5.0-generate-assignment.md",
}

# A "removed" term is not a defect when it appears within this many lines
# of one of these phrases. The window is symmetric: the phrase may be on
# the same line, the previous line, or the next line.
REMOVAL_CONTEXT_PHRASES = (
    "removed", "no longer exists", "no longer", "pre-amendment",
    "dropped", "replaced by", "replaced", "§15.8", "15.8",
    "migration", "was removed", "were removed", "has been removed",
    "have been removed", "removed in", "removed by",
)

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
            # Skip Mermaid diagram lines: node names such as C1, C2, C3,
            # C4, C8 identify components in the diagram, not decisions.
            if "flowchart" in stripped or stripped.startswith("C") and stripped[1:2].isdigit():
                # Only skip when the line is clearly a Mermaid node
                # declaration or edge, recognised by the bracket or arrow.
                if "[" in line or "-.->" in line or "-->" in line:
                    continue
            for m in label_pattern.finditer(line):
                label = m.group(0)
                if label not in defined:
                    violations.append((md, i, f"undefined decision label {label}", line.strip()[:140]))
    return violations


def _removal_context(lines, idx, window=1):
    """True if any of the removal-context phrases appears within `window`
    lines of `idx` (inclusive of both)."""
    lo = max(0, idx - window)
    hi = min(len(lines), idx + window + 1)
    for j in range(lo, hi):
        if any(phrase in lines[j] for phrase in REMOVAL_CONTEXT_PHRASES):
            return True
    return False


def scan_amendment_register_terms() -> list:
    """Check the tree against the amendment register's term table.

    Returns a list of (path, line_number, label, text) tuples for every
    violation, matching the shape of scan()'s output.
    """
    violations = []

    # Preload every markdown file once, since we scan the tree several
    # times (once per term).
    files = []
    for md in sorted(DOCS_ROOT.rglob("*.md")):
        if ".git" in md.parts:
            continue
        try:
            lines = md.read_text(encoding="utf-8").splitlines()
        except UnicodeDecodeError:
            continue
        files.append((md, lines))

    # Removed-term scan: the term must not appear outside a removal context.
    for term, polarity, source in AMENDMENT_REGISTER_TERMS:
        if polarity != "removed":
            continue
        for md, lines in files:
            # decisions.md states what the decisions removed; it is a
            # definition file and legitimately names removed terms.
            if md.name == "decisions.md":
                continue
            # The specification is the authority; §15 and §9.7 legitimately
            # name removed terms when they describe the removal.
            if md.name == "system-design-spec.md":
                continue
            for i, line in enumerate(lines):
                if term in line and not _removal_context(lines, i):
                    violations.append((
                        md, i + 1,
                        f"removed term '{term}' ({source}) appears outside a removal context",
                        line.strip()[:140]))

    # Added-term scan: the term must appear at least once in the tree.
    for term, polarity, source in AMENDMENT_REGISTER_TERMS:
        if polarity != "added":
            continue
        found = False
        for md, lines in files:
            if md.name in ("decisions.md", "consistency_check.py"):
                continue
            if any(term in line for line in lines):
                found = True
                break
        if not found:
            violations.append((
                Path("<tree>"), 0,
                f"added term '{term}' ({source}) not found anywhere in docs/",
                ""))

    # Setting scan: the key must appear in settings.md and in the process
    # document that consumes it.
    settings_md = DOCS_ROOT / "database" / "settings.md"
    settings_lines = settings_md.read_text(encoding="utf-8").splitlines() if settings_md.exists() else []
    for term, polarity, source in AMENDMENT_REGISTER_TERMS:
        if polarity != "setting":
            continue
        if not any(term in line for line in settings_lines):
            violations.append((
                settings_md, 0,
                f"setting '{term}' ({source}) missing from docs/database/settings.md",
                ""))
        consumer_rel = SETTING_CONSUMERS.get(term)
        if consumer_rel is not None:
            consumer = Path.home().joinpath("MKN-ms", consumer_rel)
            if not consumer.exists():
                violations.append((
                    consumer, 0,
                    f"setting '{term}' ({source}) consumer document not found: {consumer_rel}",
                    ""))
            else:
                consumer_lines = consumer.read_text(encoding="utf-8").splitlines()
                if not any(term in line for line in consumer_lines):
                    violations.append((
                        consumer, 0,
                        f"setting '{term}' ({source}) not mentioned in its consumer {consumer_rel}",
                        ""))

    return violations


def main() -> int:
    if not DOCS_ROOT.exists():
        print(f"ERROR: docs/ not found at {DOCS_ROOT}. Run this script from the repository root.")
        return 2

    violations = scan()

    defined_labels = collect_defined_labels()
    violations.extend(scan_decision_labels(defined_labels))

    violations.extend(scan_amendment_register_terms())

    if not violations:
        print("\u2713 No stale references found. Repository is consistent.")
        print()
        print(f"Amendment register: {len(AMENDMENT_REGISTER_UNVERIFIABLE)} register row(s) require human verification.")
        for section, description in AMENDMENT_REGISTER_UNVERIFIABLE:
            print(f"  {section}  {description}")
        print()
        print("The amendment-register term table is hand-maintained from the")
        print("specification's §15.1-§15.4 and §15.8. See the module docstring.")
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
