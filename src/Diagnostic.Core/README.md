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
- The composition root references `Adapters.Mock`. A consumer of the shared interfaces needs only these two objects:

```csharp
var mock = new MockDiagnosticService();
IVciAdapter adapter = mock.ManagedAdapter; // not mock.Adapter
IDiagnosticService diagnostics = mock;
```

- Always pass `ManagedAdapter` (`ManagedVciAdapter`) as the `IVciAdapter`. It shares one connection with the service. Connecting the raw `mock.Adapter` first makes `StartSessionAsync` fail with `AdapterBusy`.
- Flow: `DiscoverAsync`, `ConnectAsync(adapterId)`, `StartSessionAsync` with the same id, then reads. `DisconnectAsync` ends the session's connection; after reconnecting, start a new session.
- `ManagedVciAdapter.SendAsync` is refused (`RawAccessNotAllowed`). A failed discovery throws instead of returning an empty list.
- Live data: an empty parameter list reads every supported parameter.
- Every simulated result has `IsSimulated = true`, and simulated adapters say `SIMULATED` in `DisplayName`.
- Check `Status` before `Data`. `PartialSuccess` means `Data` is present but incomplete; see `Message`. Do not show it as a full success: an empty DTC list with `PartialSuccess` does not mean that there are no codes.
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
- An empty live-data parameter list means all supported parameters.
- `VciConnectionManager`, `ManagedVciAdapter`, `EndSessionAsync`, `GetSupportedParametersAsync`, `UdsReader` and `ICanChannel` are additions outside the shared contracts.

Definition sources and licences: [`../Diagnostic.Protocols/SOURCES.md`](../Diagnostic.Protocols/SOURCES.md).
