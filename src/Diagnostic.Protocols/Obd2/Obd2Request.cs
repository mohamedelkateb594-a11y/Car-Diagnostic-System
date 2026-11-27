namespace Diagnostic.Protocols;

/// <summary>Read-only OBD-II service identifiers. State-changing services are deliberately absent.</summary>
public static class Obd2Service
{
    public const byte CurrentData = 0x01;
    public const byte StoredDtcs = 0x03;
    public const byte PendingDtcs = 0x07;
    public const byte VehicleInformation = 0x09;
}

public static class Obd2Request
{
    public const byte VinPid = 0x02;

    public static byte[] CurrentData(byte pid) => [Obd2Service.CurrentData, pid];

    public static byte[] StoredDtcs() => [Obd2Service.StoredDtcs];

    public static byte[] PendingDtcs() => [Obd2Service.PendingDtcs];

    public static byte[] Vin() => [Obd2Service.VehicleInformation, VinPid];
}
