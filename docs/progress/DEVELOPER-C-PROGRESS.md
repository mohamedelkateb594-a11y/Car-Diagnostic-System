# Developer C Progress

Branch: `dev-c/desktop-ui` (created from `main`; nothing merged into `main`).

## Last completed task
None fully completed. C-101 is written but NOT yet built or tested.

## Current task
C-101 Create WPF MVVM shell and navigation. Status: written, unverified. C-102 is blocked (see below).

## Files created (all under Developer C ownership)
- `src/Diagnostic.Desktop/Diagnostic.Desktop.csproj` (target `net10.0-windows`, no packages yet)
- `src/Diagnostic.Desktop/App.xaml`, `App.xaml.cs`
- `src/Diagnostic.Desktop/Mvvm/`, `Navigation/`, `Services/ThemeService.cs`
- `src/Diagnostic.Desktop/ViewModels/` (`ViewModelBase`, `MainViewModel`, `PlaceholderViewModel`)
- `src/Diagnostic.Desktop/Views/` (`MainWindow`, `PlaceholderView`)
- `src/Diagnostic.Desktop/Theme/` (`Light.xaml`, `Dark.xaml`, `Styles.xaml`)

## Tests and results
- Build: NOT run (no Windows/.NET SDK where the code was written; WPF builds on Windows only).
- `dotnet restore`: NOT run, so no package has been restored or confirmed to resolve.
- Automated tests: none written yet.
- Light/Dark contrast: colors chosen, not measured.

## Project owner decisions received
1. .NET 10 LTS approved.
2. WPF, DI, Logging, xUnit and LiveCharts2 approved.
3. PDF library choice postponed until its license is confirmed suitable for commercial use.
4. Wait for A's approved API Contracts and shared project structure. Do not create a separate Solution.
5. Responsibilities: A owns Mock VCI Adapter, Mock Diagnostic Service, device discovery, connection, DTC read/clear and safety checks. C owns UI, device list, connection status, DTC clear confirmation screen and results display. B owns vehicle database and definitions.

## Package versions (NuGet pages checked 2026-11-27; nothing pinned in the csproj yet)
- `Microsoft.Extensions.DependencyInjection`: 10.0.14 is the latest stable (matches .NET 10.0.14 patch release of 2026-10-25). Compatible with net10.0.
- `Microsoft.Extensions.Logging`: not checked individually; expected on the same 10.0.x line. Confirm before pinning.
- `xunit`: not checked. v2 vs v3 is an open choice; confirm with the team.
- `LiveChartsCore.SkiaSharpView` 2.2.0 was published 2026-11-19 (8 days ago). Its NuGet page lists net8.0-windows7.0 as the compatible framework and net10.0 only as computed. The matching WPF package (`LiveChartsCore.SkiaSharpView.WPF`) version was not confirmed. Verify it restores and renders on net10.0-windows before pinning, and consider waiting until the release is older.

## Open items
- Contract state: `API_CONTRACTS.md` on `dev-a/contract-phase1` still has no Clear DTC operation (section 8 says it needs a separate reviewed contract). `src/Contracts` is still empty on that branch.
- Not yet answered by A: how the Mock Diagnostic Service is provided and called by Desktop; how Desktop discovers adapters (`IVciAdapter.DiscoverAsync` lives in `Adapters.Abstractions`; `IDiagnosticService` has no discovery method); the Clear DTC contract. A GitHub issue for these was drafted but NOT created (the form was not submitted). Until it is, treat these as unrequested.
- `NotTested` is missing from `EcuScanStatus`; `DtcRecord.Status` is free text; vehicle-definition and report DTOs are undefined.
- `.gitignore` and the `.sln` belong to FND-004 (A coordinates); not created here. Do not commit `bin/` or `obj/`.

## Next step
1. Get the Clear DTC / Mock Service / adapter-discovery questions to A (create the GitHub issue).
2. On a Windows machine with the .NET 10 SDK: run `dotnet build src/Diagnostic.Desktop` and record the result here.
3. After A's shared structure and approved contracts land: rebase onto it, add the approved packages with pinned versions, add `tests/Desktop.Tests`, then start C-102.
