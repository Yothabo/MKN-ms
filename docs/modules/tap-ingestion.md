# Tap Ingestion

---

## Named concern

Tap ingestion. The path by which an NFC tap or equivalent signal becomes an attendance record.

## Boundary

The boundary is between the physical source of a tap and the `AttendanceRecord` the system writes. The specification defines `AttendanceRecord.Source` with a `Tap` value, and states that `Tap` is defined in the vocabulary but is not produced by any implemented path in the current phase. The specification does not define what a tap payload carries, how the payload resolves to a member, whether the resolution uses the card number or a distinct token, or which process receives the tap.

The manual register marking path is the only implemented producer of `AttendanceRecord` rows.

## Status

Recognised, but not implementation-ready. The specification does not define the process contract, the payload, the resolution step, or the storage. The concern is named; its content is open.

## Non-dependency

The thirteen defined processes do not depend on the content of tap ingestion. Every process can be implemented, tested, and run without tap ingestion being decided. 6.0 writes attendance rows; whether a row's `Source` is `Manual` or, in a future phase, `Tap` does not change what 6.0 does.

---

*Source: System Design Specification §4 (AttendanceRecord.Source), §13.*
