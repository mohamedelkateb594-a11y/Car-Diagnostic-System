# Project Tasks & Delivery Plan

Statuses: `Todo`, `In Progress`, `Blocked`, `Review`, `Done`. Update status only with evidence in a PR or test report.

## Phase 0 — Foundation
- [ ] FND-001 Confirm target .NET version, Windows support and package compatibility. Owner: A+C.
- [ ] FND-002 Review and approve architecture and dependency direction. Owner: A+B+C.
- [ ] FND-003 Approve shared API contracts. Owner: A+B+C.
- [ ] FND-004 Create solution/projects and CI build skeleton. Owner: A coordinates.
- [ ] FND-005 Add license/provenance schema for vehicle data. Owner: B.

## Phase 1 — Mock-first OBD-II vertical slice
### Developer A
- [ ] A-101 Define adapter abstractions and connection lifecycle.
- [ ] A-102 Implement Mock Adapter with deterministic responses.
- [ ] A-103 Implement OBD-II request/response parsing for agreed initial services.
- [ ] A-104 Implement read-only VIN/PID/DTC workflows where supported by the selected protocol profile.
- [ ] A-105 Add unit tests for parsing, timeout, disconnect and invalid response.

### Developer B
- [ ] B-101 Design initial SQLite vehicle-definition schema.
- [ ] B-102 Add source/license/provenance metadata model.
- [ ] B-103 Add small clearly labeled sample dataset.
- [ ] B-104 Build import validation and duplicate detection.
- [ ] B-105 Add schema and import tests.

### Developer C
- [ ] C-101 Create WPF MVVM shell and navigation.
- [ ] C-102 Build adapter selection/connection screen using Mock.
- [ ] C-103 Build vehicle identity screen.
- [ ] C-104 Build scan results and DTC screens using mock services.
- [ ] C-105 Build Live Data screen and basic chart.
- [ ] C-106 Add Arabic/English resource files and RTL test.
- [ ] C-107 Add simulated PDF/report output.

## Phase 2 — First real hardware integration
- [ ] HW-201 Record exact VCI make/model/firmware and available SDK/driver.
- [ ] HW-202 Verify license and Windows compatibility.
- [ ] HW-203 Implement one hardware adapter behind IVciAdapter.
- [ ] HW-204 Test on a known-safe supported vehicle and document results.
- [ ] HW-205 Compare real adapter behavior with Mock contract.

## Phase 3 — Coverage expansion
- [ ] COV-301 Add vehicle definitions with verified provenance.
- [ ] COV-302 Add supported ECU discovery profiles where legally sourced.
- [ ] COV-303 Add ISO-TP/CAN capabilities required by tested vehicles.
- [ ] COV-304 Add UDS read services only for documented/applicable profiles.
- [ ] COV-305 Publish coverage matrix and limitations.

## Phase 4 — Service functions
- [ ] SVC-401 Define per-function applicability and preconditions.
- [ ] SVC-402 Add explicit confirmation and audit/reporting.
- [ ] SVC-403 Test each service function on documented supported combinations.

## Phase 5 — Advanced functions (separate approval)
- [ ] ADV-501 Safety and threat review for Active Tests.
- [ ] ADV-502 Safety and applicability review for Coding/Adaptation.
- [ ] ADV-503 OEM-authorized integration assessment for ECU programming.
No advanced task starts without written project-owner approval.

## Release gates
- Build succeeds from clean checkout.
- Automated tests pass.
- No unlicensed datasets or secrets.
- Mock mode is visibly labeled.
- Compatibility matrix and known limitations are published.
- Hardware claims are backed by recorded tests.
