namespace Contracts;

public sealed record VehicleIdentity(
    string? Vin,
    string? Make,
    string? Model,
    int? ModelYear,
    string? Engine,
    string? Market);
