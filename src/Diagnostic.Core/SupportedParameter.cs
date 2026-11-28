namespace Diagnostic.Core;

/// <summary>A live-data parameter that the ECU reports as supported and the Core can decode.</summary>
public sealed record SupportedParameter(string ParameterId, string Name, string Unit);
