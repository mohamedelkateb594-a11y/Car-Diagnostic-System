# ISO-TP VCI adapter

`IsoTpVciAdapter` connects the Core to any `ICanChannel`. **It has only been run against the simulated ECU in `Adapters.Mock`; no hardware is supported yet.**

- CAN identifiers come from the caller (`IsoTpOptions`); none are built in.
- A real device needs its own `ICanChannel` implementation, after its model and licence terms are verified.
- Limits: classical CAN, normal addressing, up to 4095 bytes. No waiting on UDS response pending (0x78).
- A late reply that arrives after a timeout stays in the channel and could be read by the next request. A hardware channel must discard stale frames.

Simulated use:

```csharp
await using var manager = new VciConnectionManager(
    SimulatedCanAdapter.Create(SimulatedVehicles.Sedan, SimulatedUdsData.Sample));
await manager.ConnectAsync("mock-sedan-can", ct);
var uds = new UdsReader(manager);
var dtcs = await uds.ReadDtcsByStatusMaskAsync(0xFF, ct);
```
