# Developer C Progress

Branch: `dev-c/desktop-ui` (created from `main`; nothing merged into `main`).

## Last completed task
None fully completed. C-101 is written but its build and tests have NOT been confirmed yet.

## Current task
C-101 Create WPF MVVM shell and navigation. Status: written; verification blocked until the CI workflow exists on GitHub (see Blockers).
C-102: PAUSED until A approves the contracts and provides Mock Services.

## Final decisions from the project owner
1. .NET 10 LTS.
2. WPF, DI, Logging, xUnit v3 (with a compatible runner), LiveCharts2 approved in principle.
3. LiveCharts 2.2.0: postponed until its WPF package is tested for compatibility on Windows.
4. PDF library: postponed until its license is confirmed suitable for commercial use.
5. Wait for A's approved API Contracts and shared project structure. No separate Solution.
6. Responsibilities: A owns Mock VCI Adapter, Mock Diagnostic Service, device discovery, connection, DTC read/clear and safety checks. C owns UI, device list, connection status, DTC clear confirmation screen and results display. B owns vehicle database and definitions.
7. Build, test and verification of C-101 run on GitHub Actions (Windows). No local Windows is provided.
8. Do not pin any package version whose compatibility is unverified. Do not claim a test passed unless it ran.
9. Coordination with A is done through one GitHub Issue with three points: Mock Services and how to call them; VCI discovery mechanism; adding `ClearDtcsAsync` officially to the contracts. Issue link: see Open items.

## Files created or changed in this step
- `tests/Desktop.Tests/Desktop.Tests.csproj`
- `tests/Desktop.Tests/NavigationServiceTests.cs` (5 tests)
- `tests/Desktop.Tests/MainViewModelTests.cs` (2 tests)
- `docs/progress/DEVELOPER-C-PROGRESS.md`

## Blockers
- The CI workflow file `.github/workflows/desktop-ci.yml` could NOT be pushed: GitHub returned 403 ("Resource not accessible by integration"). The integration used here has no permission to write workflow files. The project owner must add the workflow file (or grant workflow permission). The intended workflow is Windows runner, .NET 10.0.x, restore, build and test of `src/Diagnostic.Desktop` and `tests/Desktop.Tests`.
- Until a workflow runs, restore/build/test results are unknown.

## Package versions pinned (only in the test project; checked on NuGet pages 2026-11-27)
- `xunit.v3` 3.2.2: latest 3.x stable (published 2026-01-14); its page states support for .NET 8 or later. A 4.0.0 stable of the `xunit.v3` package id also exists (2026-08-15); 3.x was kept because v3 was approved.
- `xunit.runner.visualstudio` 3.1.5: its page states support for .NET 8+.
- `Microsoft.NET.Test.Sdk` 18.8.1 (published 2026-07-14); newer 18.9.0 and 18.10.0 exist.
- Not pinned: `Microsoft.Extensions.DependencyInjection` (10.0.14 is the latest stable seen), `Microsoft.Extensions.Logging` (not checked), LiveCharts2 (postponed). Not needed by C-101; the app references no packages.

## Tests and results
- Written: 7 unit tests (navigation service and main view model). They test no diagnostic behavior and use no contracts.
- Restore / build / test results: NOT KNOWN. Nothing has run.
- Not covered: `ThemeService.Apply/Toggle` (needs a running WPF Application), XAML rendering, Light/Dark contrast, RTL.

## Open items
- GitHub Issue to A (three points above): not yet created at the time of this commit.
- Contract state: `API_CONTRACTS.md` on `dev-a/contract-phase1` has no Clear DTC operation (section 8) and `src/Contracts` is empty there.
- `NotTested` missing from `EcuScanStatus`; `DtcRecord.Status` is free text; vehicle-definition and report DTOs undefined.
- CI risks to check on first run: referencing a WinExe project from an xUnit v3 test executable; `dotnet test` mode under the .NET 10 SDK.
- `.gitignore` and the `.sln` belong to FND-004 (A coordinates); not created here. Do not commit `bin/` or `obj/`.

## Next step
1. Owner adds the CI workflow (or grants workflow permission).
2. Create the GitHub Issue and add its link here.
3. Read the first Actions run and record the real restore/build/test result and any fix needed.
4. After A's contracts and Mock Services are approved: rebase onto A's shared structure, add approved packages with verified versions, then start C-102.
