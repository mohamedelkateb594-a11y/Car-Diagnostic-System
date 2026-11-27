using Adapters.IsoTp;
using Contracts;
using Diagnostic.Protocols;

namespace Adapters.Mock;

/// <summary>SIMULATED UDS test data. Identifiers and codes have no real-vehicle meaning.</summary>
/// <param name="DtcRecords">Each record is 3 DTC bytes followed by a status byte.</param>
public sealed record SimulatedUdsData(
    IReadOnlyDictionary<ushort, byte[]> DataIdentifiers,
    IReadOnlyList<byte[]> DtcRecords)
{
    public static SimulatedUdsData Sample { get; } = new(
        new Dictionary<ushort, byte[]> { [0x1234] = [0x53, 0x49, 0x4D] },
        [[0x01, 0x00, 0x00, 0x08], [0x01, 0x71, 0x00, 0x04]]);
}

public static class SimulatedCanAdapter
{
    // Arbitrary identifiers used only between the simulated tester and the simulated ECU.
    private const uint RequestId = 0x100;
    private const uint ResponseId = 0x200;

    /// <summary>The adapter id is the vehicle's adapter id followed by "-can".</summary>
    public static IsoTpVciAdapter Create(SimulatedVehicle vehicle, SimulatedUdsData? uds = null)
    {
        ArgumentNullException.ThrowIfNull(vehicle);
        var info = new AdapterInfo(
            $"{vehicle.AdapterId}-can",
            $"{vehicle.DisplayName} (CAN)",
            "Simulated",
            "MockCAN",
            "sim-1.0",
            ["SIMULATED-ISO-TP"]);
        return new IsoTpVciAdapter(
            info,
            () => new SimulatedCanEcu(RequestId, ResponseId, request => Respond(vehicle, uds, request)),
            new IsoTpOptions(RequestId, ResponseId, false),
            isSimulated: true);
    }

    private static byte[]? Respond(SimulatedVehicle vehicle, SimulatedUdsData? uds, byte[] request)
    {
        if (uds is not null && request.Length == 3)
        {
            if (request[0] == UdsService.ReadDataByIdentifier)
            {
                var identifier = (ushort)((request[1] << 8) | request[2]);
                if (uds.DataIdentifiers.TryGetValue(identifier, out var data))
                {
                    return [0x62, request[1], request[2], .. data];
                }

                return [0x7F, UdsService.ReadDataByIdentifier, 0x31];
            }

            if (request[0] == UdsService.ReadDtcInformation && request[1] == UdsService.ReportDtcByStatusMask)
            {
                var mask = request[2];
                return [0x59, 0x02, 0xFF, .. uds.DtcRecords.Where(r => (r[3] & mask) != 0).SelectMany(r => r)];
            }
        }

        return RespondFromServiceMock(vehicle, request);
    }

    // The service-level mock has no delay or fault configured here, so both calls complete synchronously.
    private static byte[]? RespondFromServiceMock(SimulatedVehicle vehicle, byte[] request)
    {
        var source = new MockVciAdapter([vehicle]);
        source.ConnectAsync(vehicle.AdapterId, CancellationToken.None).GetAwaiter().GetResult();
        return source.SendAsync(request, TimeSpan.FromSeconds(1), CancellationToken.None)
            .GetAwaiter()
            .GetResult()
            .Data;
    }
}
