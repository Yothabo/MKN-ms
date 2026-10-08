# Authorization

---

## Named concern

Authorization. How the system decides whether a given identity may perform a given operation.

## Boundary

The boundary is upstream of every process. The specification states that authorisation is upstream and that processes assume the caller has passed the applicable authority check. The specification does not state what that check is, what it consults, who decides which identity may perform which operation, or what happens when an identity lacks a required permission.

The `Capability` entity exists as a vocabulary of named actions. The specification does not state which role, tier, or admin holds which capability. The boundary between the capability vocabulary and the enforcement of a capability check is where authorisation sits.

## Status

Recognised, but not implementation-ready. The specification does not define the fields, the process contract, the storage, or the API surface of authorisation. The concern is named; its content is open.

## Non-dependency

The thirteen defined processes do not depend on the content of authorisation. Every process can be implemented, tested, and run without authorisation being decided. A process receives an `AdminID`; whether that ID was permitted to invoke the process is not a concern the process can see.

---

*Source: System Design Specification §4 (Capability); docs/processes/contracts.md C6.*
