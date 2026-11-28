using System.Text;
using Adapters.Abstractions;
using Contracts;

namespace Adapters.Mock;

/// <summary>Faults injected into <see cref="MockVciAdapter.SendAsync"/>.</summary>
public enum MockFault
{
    None,
    NoResponse,
    TransportError,
    Disconnect,
    MalformedResponse,
    Throw
}

/// <summary>
/// Deterministic simulated adapter working at service-payload level. It never touches hardware and
/// every result has IsSimulated = true. Configure it before use; settings are not thread-safe.
/// </summary>
public sealed class MockVciAdapter : IVciAdapter
{
    private readonly IReadOnlyList<SimulatedVehicle> _vehicles;
    private readonly TimeProvider _time;
    private SimulatedVehicle? _connected;
    private bool _disposed;

    public MockVciAdapter(IReadOnlyList<SimulatedVehicle>? vehicles = null, TimeProvider? timeProvider = null)
    {
        _vehicles = vehicles ?? SimulatedVehicles.All;
        _time = timeProvider ?? TimeProvider.System;
    }

    public bool IsConnected => _connected is not null;

    public MockFault Fault { get; set; }

    public TimeSpan ResponseDelay { get; set; }

    /// <summary>Optional custom response; returning null falls back to the simulated vehicle.</summary>
    public Func<byte[], byte[]?>? ResponseOverride { get; set; }

    public Task<IReadOnlyList<AdapterInfo>> DiscoverAsync(CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
        {
            return Task.FromCanceled<IReadOnlyList<AdapterInfo>>(ct);
        }

        IReadOnlyList<AdapterInfo> adapters = _disposed
            ? []
            : _vehicles
                .Select(v => new AdapterInfo(
                    v.AdapterId, v.DisplayName, "Simulated", "MockVCI", "sim-1.0", ["SIMULATED-OBD2"]))
                .ToArray();
        return Task.FromResult(adapters);
    }

    public Task<OperationResult<bool>> ConnectAsync(string adapterId, CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
        {
            return Task.FromResult(Failure<bool>(OperationStatus.Cancelled, "Cancelled", "Connection cancelled."));
        }

        var vehicle = _disposed ? null : _vehicles.FirstOrDefault(v => v.AdapterId == adapterId);
        if (vehicle is null)
        {
            return Task.FromResult(Failure<bool>(
                OperationStatus.Failed, "AdapterNotFound", "Unknown simulated adapter."));
        }

        if (_connected is not null)
        {
            return Task.FromResult(Failure<bool>(
                OperationStatus.Failed, "AdapterBusy", "The simulated adapter is already connected."));
        }

        _connected = vehicle;
        return Task.FromResult(Success(true));
    }

    public Task<OperationResult<bool>> DisconnectAsync(CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
        {
            return Task.FromResult(Failure<bool>(OperationStatus.Cancelled, "Cancelled", "Disconnection cancelled."));
        }

        _connected = null;
        return Task.FromResult(Success(true));
    }

    public async Task<OperationResult<byte[]>> SendAsync(byte[] request, TimeSpan timeout, CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
        {
            return Failure<byte[]>(OperationStatus.Cancelled, "Cancelled", "Request cancelled.");
        }

        var vehicle = _connected;
        if (vehicle is null)
        {
            return Failure<byte[]>(OperationStatus.NotConnected, "NotConnected", "No adapter is connected.");
        }

        if (request is null || request.Length == 0 || timeout <= TimeSpan.Zero)
        {
            return Failure<byte[]>(OperationStatus.Failed, "InvalidArgument", "Invalid request or timeout.");
        }

        if (Fault == MockFault.Throw)
        {
            throw new InvalidOperationException("Simulated adapter exception.");
        }

        try
        {
            if (Fault == MockFault.NoResponse || ResponseDelay >= timeout)
            {
                await Task.Delay(timeout, _time, ct);
                return Failure<byte[]>(OperationStatus.Timeout, "Timeout", "Simulated timeout.");
            }

            if (ResponseDelay > TimeSpan.Zero)
            {
                await Task.Delay(ResponseDelay, _time, ct);
            }
        }
        catch (OperationCanceledException)
        {
            return Failure<byte[]>(OperationStatus.Cancelled, "Cancelled", "Request cancelled.");
        }

        switch (Fault)
        {
            case MockFault.TransportError:
                return Failure<byte[]>(OperationStatus.TransportError, "TransportError", "Simulated transport error.");
            case MockFault.Disconnect:
                _connected = null;
                return Failure<byte[]>(OperationStatus.TransportError, "ConnectionLost", "Simulated disconnection.");
            case MockFault.MalformedResponse:
                return Success<byte[]>([0x00]);
            default:
                return Success(ResponseOverride?.Invoke([.. request]) ?? BuildResponse(vehicle, request));
        }
    }

    public ValueTask DisposeAsync()
    {
        _connected = null;
        _disposed = true;
        return ValueTask.CompletedTask;
    }

    private static byte[] BuildResponse(SimulatedVehicle vehicle, byte[] request)
    {
        var service = request[0];
        return service switch
        {
            0x01 when request.Length == 2 => CurrentData(vehicle, request[1]),
            0x03 when request.Length == 1 => Dtcs(0x43, vehicle.StoredDtcs),
            0x07 when request.Length == 1 && vehicle.PendingDtcs is not null => Dtcs(0x47, vehicle.PendingDtcs),
            0x09 when request.Length == 2 && request[1] == 0x02 && vehicle.Vin is not null =>
                [0x49, 0x02, 0x01, .. Encoding.ASCII.GetBytes(vehicle.Vin)],
            0x09 => Negative(service, 0x12),
            _ => Negative(service, 0x11),
        };
    }

    private static byte[] CurrentData(SimulatedVehicle vehicle, byte pid)
    {
        if (pid is 0x00 or 0x20 or 0x40)
        {
            uint bits = 0;
            for (var offset = 1; offset <= 32; offset++)
            {
                var candidate = pid + offset;
                var supported = vehicle.LiveData.ContainsKey((byte)candidate)
                    || (offset == 32 && vehicle.LiveData.Keys.Any(key => key > candidate));
                if (supported)
                {
                    bits |= 1u << (32 - offset);
                }
            }

            return [0x41, pid, (byte)(bits >> 24), (byte)(bits >> 16), (byte)(bits >> 8), (byte)bits];
        }

        if (vehicle.LiveData.TryGetValue(pid, out var data))
        {
            return [0x41, pid, .. data];
        }

        return Negative(0x01, 0x31);
    }

    private static byte[] Dtcs(byte responseId, IReadOnlyList<ushort> codes)
    {
        var response = new List<byte> { responseId, (byte)codes.Count };
        foreach (var code in codes)
        {
            response.Add((byte)(code >> 8));
            response.Add((byte)(code & 0xFF));
        }

        return [.. response];
    }

    private static byte[] Negative(byte service, byte code) => [0x7F, service, code];

    private OperationResult<T> Success<T>(T data) =>
        new(OperationStatus.Success, data, null, null, _time.GetUtcNow(), true);

    private OperationResult<T> Failure<T>(OperationStatus status, string errorCode, string message) =>
        new(status, default, message, errorCode, _time.GetUtcNow(), true);
}
