# Authentication

---

## Named concern

Authentication. How a caller becomes a known identity in the system.

## Boundary

The boundary is between the caller and the processes. The system receives a caller; whether that caller is authenticated is currently outside the specification. The processes receive an `AdminID` and presume it resolves to a non-deleted `Admin` row. The specification does not state how the `AdminID` was obtained, how the caller proved they were the member the ID names, or what happens if a caller presents an `AdminID` that is not theirs.

The boundary is stated in `docs/processes/contracts.md` under "C6 — Identity and context."

## Status

Recognised, but not implementation-ready. The specification does not define the fields, the process contract, the storage, or the API surface of authentication. The concern is named; its content is open.

## Non-dependency

The thirteen defined processes do not depend on the content of authentication. Every process can be implemented, tested, and run without authentication being decided. A process receives an `AdminID`; where that ID came from is not a concern the process can see.

---

*Source: System Design Specification; docs/processes/contracts.md C6.*
