# Developer A — Diagnostic Core & Protocols

## Mission
Build the diagnostic engine and protocol/VCI abstractions. You own:
- `src/Contracts/` (initial proposal; shared changes require approval)
- `src/Diagnostic.Core/`
- `src/Diagnostic.Protocols/`
- `src/Adapters.Abstractions/`
- `src/Adapters.Mock/`
- `tests/Diagnostic.Core.Tests/`
- `tests/Diagnostic.Protocols.Tests/`

## Read first
`PROJECT_CONTEXT.md`, `DEVELOPMENT_RULES.md`, `ARCHITECTURE.md`, `API_CONTRACTS.md`, and `TASKS.md`.

## Work sequence
1. Review API contracts and list unresolved decisions before coding.
2. Define adapter lifecycle, cancellation, timeout and error handling.
3. Implement deterministic Mock Adapter first.
4. Implement protocol parsing in isolated, testable components.
5. Build the first read-only OBD-II vertical slice.
6. Add tests for normal and failure paths.
7. Document what has only been simulated versus tested on real hardware.

## Technical requirements
- No WPF references in Core or Protocols.
- No hard dependency on a specific VCI vendor in Core.
- Do not invent CAN IDs, DIDs, PIDs or ECU commands.
- Do not send undocumented or state-changing commands.
- Use `CancellationToken`, explicit timeout and structured `OperationResult`.
- Treat unsupported and unknown as different outcomes.
- Never label Mock results as real vehicle results.

## Out of scope
Do not build UI screens or own the vehicle database/import pipeline. Do not implement Active Tests, Coding, Adaptation, Security Access or ECU Programming in the initial phase.

## Definition of done
- Tests cover parsing, timeout, disconnect, cancellation and malformed responses.
- Mock adapter can drive the initial UI workflow.
- Public interfaces match approved contracts.
- Build succeeds and limitations are documented.

## Handoff
Report Task ID, changed files, test commands/results, hardware actually tested, unresolved issues, and what Developer B/C need from your work.
