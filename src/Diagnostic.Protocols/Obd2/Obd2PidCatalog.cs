// Definitions in this file are adapted from Wikipedia, "OBD-II PIDs"
// https://en.wikipedia.org/wiki/OBD-II_PIDs (CC BY-SA 4.0), consulted 2026-11-27.
// They are not verified against the SAE J1979 text or against real hardware. See ../SOURCES.md.
// They are kept in this single file so that they can be replaced by another licensed source.
using System.Diagnostics.CodeAnalysis;

namespace Diagnostic.Protocols;

/// <param name="Length">Number of data bytes (A, or A and B).</param>
/// <param name="Decode">Receives A and B; B is zero when Length is 1.</param>
public sealed record Obd2PidDefinition(
    byte Pid,
    string Name,
    string Unit,
    int Length,
    Func<byte, byte, double> Decode)
{
    public string ParameterId { get; } = $"obd2.01.{Pid:X2}";
}

public static class Obd2PidCatalog
{
    public static IReadOnlyList<Obd2PidDefinition> All { get; } =
    [
        new(0x04, "Calculated engine load", "%", 1, (a, _) => a * 100.0 / 255),
        new(0x05, "Engine coolant temperature", "degC", 1, (a, _) => a - 40),
        new(0x06, "Short term fuel trim bank 1", "%", 1, (a, _) => (a * 100.0 / 128) - 100),
        new(0x0A, "Fuel pressure (gauge)", "kPa", 1, (a, _) => a * 3),
        new(0x0B, "Intake manifold absolute pressure", "kPa", 1, (a, _) => a),
        new(0x0C, "Engine speed", "rpm", 2, (a, b) => ((256 * a) + b) / 4.0),
        new(0x0D, "Vehicle speed", "km/h", 1, (a, _) => a),
        new(0x0E, "Timing advance", "deg", 1, (a, _) => (a / 2.0) - 64),
        new(0x0F, "Intake air temperature", "degC", 1, (a, _) => a - 40),
        new(0x10, "Mass air flow rate", "g/s", 2, (a, b) => ((256 * a) + b) / 100.0),
        new(0x11, "Throttle position", "%", 1, (a, _) => a * 100.0 / 255),
        new(0x1F, "Run time since engine start", "s", 2, (a, b) => (256 * a) + b),
        new(0x21, "Distance travelled with MIL on", "km", 2, (a, b) => (256 * a) + b),
        new(0x2F, "Fuel tank level", "%", 1, (a, _) => a * 100.0 / 255),
        new(0x31, "Distance since codes cleared", "km", 2, (a, b) => (256 * a) + b),
        new(0x33, "Absolute barometric pressure", "kPa", 1, (a, _) => a),
        new(0x42, "Control module voltage", "V", 2, (a, b) => ((256 * a) + b) / 1000.0),
        new(0x46, "Ambient air temperature", "degC", 1, (a, _) => a - 40),
        new(0x5C, "Engine oil temperature", "degC", 1, (a, _) => a - 40),
    ];

    public static bool TryGet(string parameterId, [NotNullWhen(true)] out Obd2PidDefinition? definition)
    {
        definition = All.FirstOrDefault(d => string.Equals(d.ParameterId, parameterId, StringComparison.OrdinalIgnoreCase));
        return definition is not null;
    }
}
