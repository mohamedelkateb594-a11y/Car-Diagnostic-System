# Diagnostic Core (Developer A)

Read-only diagnostic engine. **Only simulated data has been exercised. No real VCI or vehicle is supported yet.**

## Run

```
dotnet build CarDiagnosticSystem.sln -c Release -warnaserror
dotnet test CarDiagnosticSystem.sln -c Release --no-build
dotnet run --project samples/Diagnostic.Sample -c Release
```

## Using it from the UI (answers issue #3)

- Mock services live in `src/Adapters.Mock`: `MockVciAdapter` and `MockDiagnosticService`.
- The composition root references `Adapters.Mock` and registers:

```csharp
var mock = new MockDiagnosticService();
services.AddSingleton<IDiagnosticService>(mock);
services.AddSingleton(mock.Connection); // VciConnectionManager
```

- View models need only `Contracts` and `Diagnostic.Core`.
- Discovery: `VciConnectionManager.DiscoverAsync` (wraps `IVciAdapter.DiscoverAsync`). `Success` with an empty list means no device; any other status is a discovery failure.
- Connect: `ConnectAsync(adapterId)`, then `StartSessionAsync` with the same id reuses the connection. `EndSessionAsync` or `DisconnectAsync` closes it.
- Every simulated result has `IsSimulated = true`, and simulated adapters say `SIMULATED` in `DisplayName`.
- Check `Status` before `Data`. `PartialSuccess` means `Data` is present but incomplete; see `Message`.
- Freeze frame: `Success` with `Data = null` means no stored frame. To simulate one, set `mock.Adapter.ResponseOverride = SimulatedFreezeFrame.Sample.Respond`.
- Clear DTC is not implemented and is not in the contract.

## Supported

| Function | Notes |
| --- | --- |
| Discovery, connect, disconnect | One connection and one session at a time |
| VIN (service 09 PID 02) | |
| Stored and pending DTCs (services 03, 07) | No descriptions |
| Live data (service 01) | PIDs listed in `Obd2PidCatalog`, only when the ECU reports support |
| Freeze frame (service 02) | Frame 0 only, same PID catalog |
| UDS 0x22 and 0x19/02 | Through `UdsReader`; not part of `IDiagnosticService` |
| CAN / ISO-TP | `IsoTpVciAdapter` over `ICanChannel`; tested with simulated channels only |

## Not supported

Real VCI hardware, ECU discovery and addressing, non-CAN protocols' multi-message responses, permanent DTCs, monitor status, waiting on UDS response pending (0x78), UDS sessions and security access, Clear DTC, active tests, coding, programming.

## Temporary decisions (pending contract approval, PR #1)

- A negative ECU response is never a success: `ErrorCode = NegativeResponse`, `Message` holds the raw bytes.
- The generic ECU id is `obd2`.
- `VciConnectionManager`, `EndSessionAsync`, `GetSupportedParametersAsync`, `UdsReader` and `ICanChannel` are additions outside the shared contracts.

Definition sources and licences: [`../Diagnostic.Protocols/SOURCES.md`](../Diagnostic.Protocols/SOURCES.md).
