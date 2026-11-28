# Diagnostic.Desktop (Developer C)

WPF shell for the diagnostic engine. MVVM, dependency injection and logging (`Microsoft.Extensions.*` 10.0.0, Debug output).

## What is real and what is simulated

| Part | State |
| --- | --- |
| Screens: VCI connection, vehicle/VIN, full scan, DTC, live data (with chart) | Real UI code, runs against the engine below |
| Engine behind the screens | **SIMULATED.** `Diagnostic.Core` (Developer A) running over `Adapters.Mock`. No VCI hardware, no real vehicle |
| English / Arabic with RTL, light / dark | Real |
| Dashboard, freeze frame, reports | **Placeholders** (nothing shown) |
| Clear DTC, active tests, coding | Not implemented (not in the contracts) |
| PDF reports | Not implemented (library licence not yet confirmed) |

When the engine is simulated every screen that shows engine data displays a *SIMULATION* badge, and the window footer names the engine in use.

## Layout

- `src/Diagnostic.Presentation` (net10.0): view models, commands, session state. No WPF, tested in the shared CI.
- `src/Diagnostic.Desktop` (net10.0-windows): views, theme, localization, navigation, composition root.
- `tests/Diagnostic.Presentation.Tests`: view models against the real engine over the simulated adapter. Runs in the shared CI (Linux).
- `tests/Desktop.Tests`: navigation, localization and DI composition. Windows only.

## Replacing the simulated engine

Screens depend only on `IVciAdapter` and `IDiagnosticService` from the shared contracts. The single swap point is `Composition/ServiceRegistration.cs`:

```csharp
services.AddSingleton<IDiagnosticBackend, SimulatedBackend>();   // replace with the real backend
```

Implement `IDiagnosticBackend` (name, `IVciAdapter`, `IDiagnosticService`) for the real engine and register it instead. No screen, view model or XAML changes. Per `src/Diagnostic.Core/README.md` the `IVciAdapter` given to the UI must be the managed adapter that shares its connection with the service.

## Build and run (Windows)

```
dotnet build src/Diagnostic.Desktop -c Release
dotnet run --project src/Diagnostic.Desktop -c Release
dotnet test tests/Desktop.Tests -c Release
```

## Try it

1. VCI Connection: Discover devices, select one, Connect. Two simulated vehicles are offered: a test sedan and a limited-support vehicle.
2. Vehicle: Read vehicle identity (the limited vehicle has no VIN, so the engine reports it as unsupported).
3. Full Scan, then DTC (the limited vehicle answers PartialSuccess: the UI shows it as partial, never as "no codes").
4. Live Data: Start. An empty parameter list asks the engine for every parameter the ECU supports.

## Known limits

- DTC descriptions are blank (the engine has no licensed text source), shown as a dash.
- Live data parameter ids are typed by hand; there is no picker yet (the engine's supported-parameters call is not part of the shared contract).
- Only the generic OBD-II ECU (`obd2`) exists in the engine.
