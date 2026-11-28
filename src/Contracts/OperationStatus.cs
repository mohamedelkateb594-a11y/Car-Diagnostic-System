namespace Contracts;

public enum OperationStatus
{
    Success, PartialSuccess, Unsupported, NotApplicable,
    NotConnected, Timeout, Cancelled, InvalidResponse,
    TransportError, ProtocolError, SafetyPreconditionFailed,
    Failed
}
