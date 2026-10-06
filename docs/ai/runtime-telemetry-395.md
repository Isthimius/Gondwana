# Runtime telemetry implementation record

Base: `b3fd92c21` (current master, October 6, 2026).

Issue #395 agrees with the October 6 handoff; no Studio description drift was found.

Implementation in progress: opt-in, lease-owned core collection; bounded aggregation
and detached history; existing rendering measurement routing; SceneViewer F3 as a
consumer. Renderer scheduling, mailbox ownership, and legacy diagnostic events remain
compatibility requirements. Studio, transport, and runtime object inspection are deferred.

Validation to complete: deterministic aggregation/lifecycle/concurrency tests, engine
and viewer regression suites, Release solution and formatting checks, browser helper
tests, and comparative disabled/collecting/display overhead measurements.
