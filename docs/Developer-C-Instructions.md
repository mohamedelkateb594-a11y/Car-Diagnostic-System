# Developer C — Desktop UI & UX

## Mission
Build the Windows desktop experience using WPF and MVVM. You own:
- `src/Diagnostic.Desktop/`
- `tests/Desktop.Tests/` (if created)
- UI resources, localization, navigation and report presentation

## Read first
`PROJECT_CONTEXT.md`, `DEVELOPMENT_RULES.md`, `ARCHITECTURE.md`, `API_CONTRACTS.md`, and `TASKS.md`.

## Work sequence
1. Review approved service contracts and identify UI integration needs.
2. Create WPF MVVM shell, navigation and dependency injection.
3. Build screens against Mock services; do not wait for hardware.
4. Add connection status, vehicle identity, scan summary, ECU results, DTC and Live Data.
5. Add Arabic/English resources and verify RTL layout.
6. Add report preview/export with simulated/real labeling.
7. Test loading, empty, unsupported, timeout, disconnected and error states.

## UX requirements
- Make connection state and selected adapter visible.
- Distinguish `Unsupported`, `NotTested`, `NoResponse` and `Error`.
- Never show simulated readings as real readings.
- Show units beside all Live Data values.
- Confirm before clearing DTC and explain that clearing does not repair the fault.
- Do not show advanced controls unless capability and applicability are verified.
- Ensure readable contrast in Light and Dark themes.
- Keep technical details available without overwhelming the main workflow.

## Out of scope
Do not implement protocol logic, CAN/UDS transport or vehicle database ingestion. Do not add fake diagnostic coverage claims. Use approved contracts and mock services.

## Definition of done
- App builds from clean checkout.
- Screens work with Mock services.
- Arabic RTL and English layouts are checked.
- UI handles loading, empty, unsupported, cancellation and failure states.
- Reports clearly state data source and simulated status.

## Handoff
Report Task ID, screens/components changed, test/build results, accessibility/localization notes, mock integration assumptions, and requests for A/B.
