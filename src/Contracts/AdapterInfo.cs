namespace Contracts;

public sealed record AdapterInfo(
    string AdapterId,
    string DisplayName,
    string Manufacturer,
    string Model,
    string FirmwareVersion,
    IReadOnlyList<string> SupportedProtocols);
