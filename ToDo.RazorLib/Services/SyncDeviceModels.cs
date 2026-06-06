namespace ToDo.RazorLib.Services;

public sealed record SyncDeviceIdentity(
    string DeviceId,
    string DeviceName,
    bool SyncEnabled,
    bool AutoSyncOnChanges,
    string ManualAddress);

public sealed record AvailableSyncDevice(
    string DeviceId,
    string DeviceName,
    string Address,
    DateTimeOffset LastSeen);

public sealed record PairedSyncDevice(
    string DeviceId,
    string DeviceName,
    string Address,
    DateTimeOffset PairedAt,
    DateTimeOffset? LastSyncedAt,
    bool IsOnline,
    string TrustToken);

public sealed record SyncPairingSession(
    string DeviceId,
    string DeviceName,
    string Address,
    string RemoteSessionId,
    string VerificationCode,
    DateTimeOffset ExpiresAt,
    string TrustToken);

public sealed record SyncTransportStatus(
    bool IsRunning,
    string? Address,
    int? Port,
    DateTimeOffset? StartedAt,
    string? Error);

public sealed record SyncHelloResponse(
    string DeviceId,
    string DeviceName,
    string ProtocolVersion);

public sealed record SyncDiscoveryResult(
    IReadOnlyList<AvailableSyncDevice> Devices,
    string? Message);

public sealed record SyncPairStartRequest(
    string DeviceId,
    string DeviceName,
    string Address);

public sealed record SyncPairStartResponse(
    string DeviceId,
    string DeviceName,
    string Address,
    string SessionId,
    string VerificationCode,
    DateTimeOffset ExpiresAt);

public sealed record SyncPairConfirmRequest(
    string SessionId,
    string VerificationCode,
    string DeviceId,
    string DeviceName,
    string Address,
    string TrustToken);

public sealed record SyncPairConfirmResponse(
    string DeviceId,
    string DeviceName,
    string Address,
    string TrustToken);

public sealed record IncomingPairingRequest(
    string SessionId,
    string DeviceId,
    string DeviceName,
    string Address,
    string VerificationCode,
    DateTimeOffset ExpiresAt,
    bool IsApproved);
