namespace Diagnostic.Protocols;

/// <summary>One classical CAN frame (up to 8 data bytes).</summary>
public sealed record CanFrame(uint Id, bool IsExtended, byte[] Data);

/// <summary>
/// Temporary, replaceable frame-level abstraction. It is not part of the shared contracts; a hardware
/// adapter can implement it, or it can be replaced once a frame-level contract is approved.
/// </summary>
public interface ICanChannel
{
    Task SendAsync(CanFrame frame, CancellationToken ct);

    Task<CanFrame> ReceiveAsync(CancellationToken ct);
}
