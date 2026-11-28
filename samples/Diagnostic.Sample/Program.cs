using Adapters.Mock;
using Contracts;
using Diagnostic.Core;

Console.WriteLine("SIMULATED DATA - no real vehicle or VCI is involved.");

await using var service = new MockDiagnosticService();
using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
var ct = timeout.Token;

var discovery = await service.Connection.DiscoverAsync(ct);
if (discovery.Data is null || discovery.Data.Count == 0)
{
    return Fail("discovery", discovery.Status);
}

foreach (var adapter in discovery.Data)
{
    Console.WriteLine();
    Console.WriteLine($"== {adapter.DisplayName} [{adapter.AdapterId}]");

    var session = await service.StartSessionAsync(adapter.AdapterId, null, ct);
    if (session.Data is null)
    {
        return Fail("session start", session.Status);
    }

    var id = session.Data.SessionId;

    var identity = await service.ReadVehicleIdentityAsync(id, ct);
    Console.WriteLine($"VIN [{identity.Status}]: {identity.Data?.Vin ?? "not available"}");

    var scan = await service.FullScanAsync(id, ct);
    if (scan.Data is null)
    {
        return Fail("scan", scan.Status);
    }

    foreach (var ecu in scan.Data)
    {
        Console.WriteLine($"ECU {ecu.DisplayName}: {ecu.Status} {ecu.Notes}");
        foreach (var dtc in ecu.Dtcs)
        {
            Console.WriteLine($"  {dtc.Code} ({dtc.Status})");
        }
    }

    var parameters = await service.GetSupportedParametersAsync(id, DiagnosticService.Obd2EcuId, ct);
    if (parameters.Data is null)
    {
        return Fail("supported parameters", parameters.Status);
    }

    var live = await service.ReadLiveDataAsync(
        id, DiagnosticService.Obd2EcuId, parameters.Data.Select(p => p.ParameterId).ToArray(), ct);
    if (live.Status != OperationStatus.Success || live.Data is null)
    {
        return Fail("live data", live.Status);
    }

    foreach (var point in live.Data)
    {
        Console.WriteLine($"  {point.Name}: {point.Value:0.##} {point.Unit} (simulated={point.IsSimulated})");
    }

    var freezeFrame = await service.ReadFreezeFrameAsync(id, DiagnosticService.Obd2EcuId, null, ct);
    Console.WriteLine($"Freeze frame: {freezeFrame.Status} - {freezeFrame.Message}");

    var end = await service.EndSessionAsync(id, ct);
    if (end.Status != OperationStatus.Success)
    {
        return Fail("session end", end.Status);
    }
}

return 0;

static int Fail(string step, OperationStatus status)
{
    Console.Error.WriteLine($"Sample failed at {step}: {status}");
    return 1;
}
