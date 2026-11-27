namespace Contracts;

public sealed record EcuScanResult(
    string EcuId,
    string DisplayName,
    EcuScanStatus Status,
    IReadOnlyList<DtcRecord> Dtcs,
    string? Notes);
