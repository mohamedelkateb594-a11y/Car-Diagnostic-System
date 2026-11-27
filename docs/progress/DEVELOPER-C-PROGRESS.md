# Developer C Progress

Branch: `dev-c/desktop-ui` (created from `main`; nothing merged into `main`).

## Last completed task
None fully completed. C-101 is written but NOT yet built or tested (see below).

## Current task
C-101 Create WPF MVVM shell and navigation. Status: written, unverified.

## Files created (all under Developer C ownership)
- `src/Diagnostic.Desktop/Diagnostic.Desktop.csproj`
- `src/Diagnostic.Desktop/App.xaml`, `App.xaml.cs`
- `src/Diagnostic.Desktop/Mvvm/` (`ObservableObject`, `RelayCommand`)
- `src/Diagnostic.Desktop/Navigation/` (`NavigationItem`, `INavigationService`, `NavigationService`)
- `src/Diagnostic.Desktop/Services/ThemeService.cs`
- `src/Diagnostic.Desktop/ViewModels/` (`ViewModelBase`, `MainViewModel`, `PlaceholderViewModel`)
- `src/Diagnostic.Desktop/Views/` (`MainWindow`, `PlaceholderView`)
- `src/Diagnostic.Desktop/Theme/` (`Light.xaml`, `Dark.xaml`, `Styles.xaml`)

## Tests and results
- Build: NOT run. The authoring environment has no Windows/.NET SDK, and WPF only builds on Windows.
- Automated tests: none written yet. No test framework is approved (see decisions).
- Visual check of Light/Dark contrast: NOT done; colors are chosen but not measured.

## Decisions and approvals needed
1. FND-001: target framework is provisionally `net10.0-windows` (.NET 10 is LTS to 2028-11-14; .NET 8 and 9 reached end of support on 2026-11-10 per Microsoft). Needs A+C approval.
2. NuGet packages: none added. Need approval for a DI container, logging, a test framework (xUnit proposed in PROJECT_CONTEXT.md), a PDF library and LiveCharts2.
3. FND-003: contracts in `API_CONTRACTS.md` are still "suggested". `src/Contracts` on `main` and on `dev-a/contract-phase1` contains only `.gitkeep`. No screen that consumes contracts is started.
4. Open questions raised to the project owner: who provides the Mock `IDiagnosticService`; how Desktop discovers adapters; whether a Clear DTC operation will exist; `NotTested` missing from `EcuScanStatus`; free-text `DtcRecord.Status`; missing DTOs for vehicle-definition metadata and reports.
5. `.gitignore` and the `.sln` belong to FND-004 (A coordinates); not created here. Do not commit `bin/` or `obj/`.

## Known limitations of C-101
- Navigation labels are hard-coded English; Arabic/English resources and RTL arrive in C-106.
- Placeholder screens show no diagnostic data.
- Keyboard focus visuals on the navigation list are not styled yet.

## Next step
Once the project owner approves FND-001 and the package list: build `Diagnostic.Desktop` on Windows to verify C-101, add a test project, then start C-102 (adapter selection/connection screen) after the adapter/service questions in decision 4 are answered.
