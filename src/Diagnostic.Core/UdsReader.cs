using Contracts;
using Diagnostic.Protocols;

namespace Diagnostic.Core;

/// <summary>
/// Read-only UDS requests over the current connection. Not part of the shared contracts.
/// No session control, security access or state-changing service is offered.
/// </summary>
public sealed class UdsReader
{
    private readonly VciConnectionManager _connection;

    public UdsReader(VciConnectionManager connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        _connection = connection;
    }

    /// <summary>The identifier must come from a verified vehicle definition.</summary>
    public Task<OperationResult<byte[]>> ReadDataByIdentifierAsync(ushort identifier, CancellationToken ct) =>
        ExchangeAsync(
            UdsRequest.ReadDataByIdentifier(identifier),
            response => UdsParser.ParseDataByIdentifier(response, identifier),
            ct);

    public Task<OperationResult<IReadOnlyList<UdsDtc>>> ReadDtcsByStatusMaskAsync(
        byte statusMask, CancellationToken ct) =>
        ExchangeAsync(UdsRequest.ReadDtcsByStatusMask(statusMask), UdsParser.ParseDtcsByStatusMask, ct);

    private async Task<OperationResult<T>> ExchangeAsync<T>(
        byte[] request, Func<byte[], ParseResult<T>> parse, CancellationToken ct)
    {
        var sent = await _connection.SendAsync(request, _connection.Generation, ct);
        if (sent.Status != OperationStatus.Success || sent.Data is null)
        {
            var status = sent.Status == OperationStatus.Success ? OperationStatus.InvalidResponse : sent.Status;
            return new OperationResult<T>(
                status, default, sent.Message, sent.ErrorCode, sent.TimestampUtc, sent.IsSimulated);
        }

        var parsed = parse(sent.Data);
        return new OperationResult<T>(
            parsed.Status, parsed.Value, parsed.Message, parsed.ErrorCode, sent.TimestampUtc, sent.IsSimulated);
    }
}
