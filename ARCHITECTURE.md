# Architecture

## 1. Architectural principles
- فصل الواجهة عن التشخيص وعن الاتصال وعن بيانات المركبات.
- الاعتماد على abstractions حتى لا يرتبط المحرك بمصنع VCI واحد.
- عدم افتراض دعم كل مركبة أو كل ECU.
- جعل الاختبار باستخدام Mock ممكنًا دون Hardware.
- البيانات والتعريفات قابلة للإصدار والتتبع بالمصدر والترخيص.

## 2. Logical layers
```text
Diagnostic.Desktop (WPF / MVVM)
          |
          v
Contracts (shared DTOs and interfaces)
          |
          v
Diagnostic.Core
  - session orchestration
  - vehicle context
  - scan coordination
  - DTC / live data workflows
  - capability checks
          |
          +--------------------+
          |                    |
          v                    v
Diagnostic.Protocols      Vehicle Definitions
  - OBD-II services         - SQLite repository
  - CAN transport           - ECU / PID / DTC metadata
  - ISO-TP                  - applicability metadata
  - UDS foundations
          |
          v
Adapters.Abstractions
          |
          +-------------------+
          |                   |
          v                   v
Adapters.Mock           Hardware Adapter(s)
                        - only after VCI is identified
                        - vendor SDK / driver / J2534
```

## 3. Project responsibilities
### `src/Contracts`
- Interfaces and DTOs shared across projects.
- No WPF dependencies.
- No vendor-specific implementation.
- Version changes require contract review.

### `src/Diagnostic.Core`
- Coordinates diagnostic sessions and workflows.
- Validates operation preconditions and cancellation.
- Converts protocol/adapter outcomes into domain results.
- Does not know UI controls or SQLite schema details.

### `src/Diagnostic.Protocols`
- Protocol implementations and message parsing.
- Initial focus: OBD-II request/response model.
- CAN/ISO-TP/UDS are added incrementally and tested independently.
- No hard-coded unsupported vehicle-specific commands.

### `src/Adapters.Abstractions`
- VCI connection and transport abstractions.
- Device capabilities and connection lifecycle.
- No specific vendor SDK dependency.

### `src/Adapters.Mock`
- Deterministic simulated adapter and vehicle responses.
- Clearly marks all data as simulated.
- Supports tests and UI development.

### `src/Diagnostic.Desktop`
- WPF application, MVVM, navigation, localization, charts, reports.
- Calls Core through Contracts.
- Must not implement diagnostic protocols inside ViewModels.

### `data/vehicle-definitions`
- Versioned, licensed vehicle/ECU metadata.
- Include source, license, applicability and review status.

### `tools/data-import`
- Validates and imports source data.
- Never silently overwrites reviewed records.
- Produces import report and provenance.

### `tests`
- Unit, integration-with-mock, contract and parsing tests.
- No hardware dependency in automated tests.

## 4. Dependency direction
- Desktop depends on Contracts and Core.
- Core depends on Contracts and abstractions.
- Protocols depend on Contracts/adapter abstractions as required.
- Hardware adapters implement adapter abstractions.
- Mock adapter implements the same abstractions.
- Data access is exposed through interfaces; Core must not depend on UI or vendor SDK.
- Avoid circular references.

## 5. Initial solution layout
```text
CarDiagnosticSystem.sln
src/
  Contracts/
  Diagnostic.Core/
  Diagnostic.Protocols/
  Adapters.Abstractions/
  Adapters.Mock/
  Diagnostic.Desktop/
data/
  vehicle-definitions/
    README.md
    sources/
    schemas/
    samples/
tools/
  data-import/
tests/
  Contracts.Tests/
  Diagnostic.Core.Tests/
  Diagnostic.Protocols.Tests/
  Data.Tests/
docs/
```

## 6. Runtime workflow
1. Desktop requests available adapters.
2. Adapter reports identity/capabilities and connection state.
3. Core creates a diagnostic session.
4. VIN identification is attempted only when supported.
5. Vehicle context is resolved from VIN/manual selection plus definitions.
6. Core builds a scan plan from supported capabilities and applicability.
7. Each ECU result is recorded independently.
8. DTC/live data are requested only from supported ECU/services.
9. Report includes all attempted operations and explicit unsupported/failed states.

## 7. Error model
At minimum distinguish:
- `NotConnected`
- `Timeout`
- `Cancelled`
- `Unsupported`
- `NotApplicable`
- `InvalidResponse`
- `TransportError`
- `ProtocolError`
- `PermissionDenied`
- `SafetyPreconditionFailed`
- `UnknownError`

Errors should include safe diagnostic context, not secrets.

## 8. Initial delivery slice
The first end-to-end slice should:
- Run using Mock Adapter.
- Establish a session.
- Return a simulated VIN or a clearly labeled unavailable result.
- Read a small set of standard OBD-II PIDs.
- Read simulated DTCs.
- Display results in Desktop.
- Generate a report clearly marked `SIMULATED`.
Only after this works should a real adapter be integrated.
