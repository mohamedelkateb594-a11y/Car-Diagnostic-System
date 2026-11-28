namespace Adapters.Mock;

/// <summary>
/// SIMULATED test data. It does not describe any real vehicle, VIN or fault.
/// </summary>
/// <param name="Vin">Null simulates an ECU that does not provide a VIN.</param>
/// <param name="LiveData">Mode 01 PID to raw data bytes.</param>
/// <param name="StoredDtcs">Two-byte encoded codes.</param>
/// <param name="PendingDtcs">Null simulates an ECU that rejects the pending-DTC service.</param>
public sealed record SimulatedVehicle(
    string AdapterId,
    string DisplayName,
    string? Vin,
    IReadOnlyDictionary<byte, byte[]> LiveData,
    IReadOnlyList<ushort> StoredDtcs,
    IReadOnlyList<ushort>? PendingDtcs);

public static class SimulatedVehicles
{
    public static SimulatedVehicle Sedan { get; } = new(
        "mock-sedan",
        "SIMULATED VCI - test sedan",
        "SMLTD0000TEST0001",
        new Dictionary<byte, byte[]>
        {
            [0x04] = [0x40],
            [0x05] = [0x7B],
            [0x0B] = [0x23],
            [0x0C] = [0x0C, 0x80],
            [0x0D] = [0x00],
            [0x0F] = [0x48],
            [0x11] = [0x26],
            [0x2F] = [0x99],
            [0x42] = [0x36, 0xB0],
        },
        [0x0100, 0x0300],
        [0x0171]);

    public static SimulatedVehicle LimitedSupport { get; } = new(
        "mock-limited",
        "SIMULATED VCI - limited support vehicle",
        null,
        new Dictionary<byte, byte[]>
        {
            [0x05] = [0x50],
            [0x0C] = [0x0B, 0xB8],
        },
        [],
        null);

    public static IReadOnlyList<SimulatedVehicle> All { get; } = [Sedan, LimitedSupport];
}
