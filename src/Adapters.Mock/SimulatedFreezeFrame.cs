namespace Adapters.Mock;

/// <summary>
/// SIMULATED freeze frame number 0. Assign <see cref="Respond"/> to
/// <see cref="MockVciAdapter.ResponseOverride"/>.
/// </summary>
/// <param name="Dtc">Two-byte encoded trigger code; zero means that no frame is stored.</param>
/// <param name="Values">PID to raw data bytes.</param>
public sealed record SimulatedFreezeFrame(ushort Dtc, IReadOnlyDictionary<byte, byte[]> Values)
{
    public static SimulatedFreezeFrame Sample { get; } = new(
        0x0300,
        new Dictionary<byte, byte[]>
        {
            [0x05] = [0x78],
            [0x0C] = [0x1F, 0x40],
            [0x0D] = [0x32],
        });

    /// <summary>Returns null for other requests so that the mock adapter answers them.</summary>
    public byte[]? Respond(byte[] request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Length != 3 || request[0] != 0x02 || request[2] != 0x00)
        {
            return null;
        }

        var pid = request[1];
        if (pid == 0x02)
        {
            return [0x42, pid, 0x00, (byte)(Dtc >> 8), (byte)(Dtc & 0xFF)];
        }

        if (Values.TryGetValue(pid, out var data))
        {
            return [0x42, pid, 0x00, .. data];
        }

        return [0x7F, 0x02, 0x31];
    }
}
