# Changelog

## 4.1.0

Brings the .NET PEP to the SAPL 4.1 enforcement model. Constraint handling
now uses a planner that binds handlers to lifecycle signals, and streaming
runs through `[StreamEnforce]`. Adds the `SUSPEND` decision verb and the
RSocket transport.

### Added

- **Planner-based constraint enforcement.** An enforcement planner binds
  constraint handlers to lifecycle signals (`decision` / `input` / `output`
  / `error`, and the streaming signals), resolving which provider claims each
  obligation and failing closed on an unresolved or ambiguous obligation.
- **`SUSPEND` decision verb** (new in SAPL 4.1.0). On a streaming
  subscription a `SUSPEND` decision pauses delivery (items drop silently)
  and resumes on the next `PERMIT`, rather than terminating; the
  `signalTransitions` option surfaces `ACCESS_SUSPENDED` / `ACCESS_GRANTED`
  boundary frames to the subscriber.
- **Streaming enforcement** via a four-state Mealy machine
  (`Pending` / `Permitting` / `Suspended` / `Terminated`) backing
  `[StreamEnforce]`.
- **`Sapl.Rsocket`** - RSocket transport for the PDP client as an
  alternative to HTTP.

### Notes

- The .NET PEP intentionally does not ship data-layer query-manipulation
  shims; enforcement covers method, HTTP, and streaming concerns.
