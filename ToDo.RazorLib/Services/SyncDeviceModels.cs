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

public sealed record SyncPingRequest(
    string DeviceId,
    string TrustToken);

public sealed record SyncPingResponse(
    string DeviceId,
    string DeviceName,
    DateTimeOffset ServerTime);

public sealed record SyncSnapshotRequest(
    string DeviceId,
    string TrustToken);

public sealed record SyncImportNewRequest(
    string DeviceId,
    string TrustToken,
    SyncSnapshotResponse Snapshot);

public sealed record SyncSnapshotResponse(
    string DeviceId,
    string DeviceName,
    string ProtocolVersion,
    DateTimeOffset CreatedAt,
    IReadOnlyList<SyncTaskDefinitionSnapshot> TaskDefinitions,
    IReadOnlyList<SyncTaskListSnapshot> TaskLists,
    IReadOnlyList<SyncTaskListItemSnapshot> TaskListItems,
    IReadOnlyList<SyncDailyOccurrenceSnapshot> DailyOccurrences,
    IReadOnlyList<SyncWeeklyOccurrenceSnapshot> WeeklyOccurrences,
    IReadOnlyList<SyncMonthlyOccurrenceSnapshot> MonthlyOccurrences);

public sealed record SyncPreviewSummary(
    string DeviceName,
    DateTimeOffset CreatedAt,
    IReadOnlyList<SyncEntityPreviewCount> EntityCounts,
    IReadOnlyList<SyncConflictDetail> Conflicts) {
    public int NewCount => EntityCounts.Sum(count => count.NewCount);
    public int MatchingCount => EntityCounts.Sum(count => count.MatchingCount);
    public int ChangedCount => EntityCounts.Sum(count => count.ChangedCount);
    public int LocalOnlyCount => EntityCounts.Sum(count => count.LocalOnlyCount);
}

public sealed record SyncEntityPreviewCount(
    string Name,
    int NewCount,
    int MatchingCount,
    int ChangedCount,
    int LocalOnlyCount);

public sealed record SyncConflictDetail(
    string EntityName,
    Guid Id,
    string Label,
    IReadOnlyList<SyncFieldConflict> Fields);

public sealed record SyncFieldConflict(
    string Name,
    string LocalValue,
    string RemoteValue);

public sealed record SyncImportSummary(
    string DeviceName,
    IReadOnlyList<SyncEntityImportCount> EntityCounts) {
    public int ImportedCount => EntityCounts.Sum(count => count.ImportedCount);
    public int SkippedCount => EntityCounts.Sum(count => count.SkippedCount);
}

public sealed record SyncEntityImportCount(
    string Name,
    int ImportedCount,
    int SkippedCount);

public sealed record SyncTaskDefinitionSnapshot(
    Guid Id,
    string Title,
    string Description);

public sealed record SyncTaskListSnapshot(
    Guid Id,
    string Name,
    string Color,
    string? Description);

public sealed record SyncTaskListItemSnapshot(
    Guid Id,
    Guid TaskDefinitionId,
    Guid TaskListId,
    bool IsDone,
    int Position);

public sealed record SyncDailyOccurrenceSnapshot(
    Guid Id,
    Guid TaskDefinitionId,
    Guid DailyPlanId,
    DateOnly Date,
    bool IsDone,
    string? Timeslot,
    int SortOrder);

public sealed record SyncWeeklyOccurrenceSnapshot(
    Guid Id,
    Guid TaskDefinitionId,
    Guid WeeklyPlanId,
    DateOnly WeekStart,
    bool IsDone,
    DayOfWeek? DayOfWeek);

public sealed record SyncMonthlyOccurrenceSnapshot(
    Guid Id,
    Guid TaskDefinitionId,
    Guid MonthlyPlanId,
    DateOnly MonthStart,
    bool IsDone,
    int? DayOfMonth);

public sealed record IncomingPairingRequest(
    string SessionId,
    string DeviceId,
    string DeviceName,
    string Address,
    string VerificationCode,
    DateTimeOffset ExpiresAt,
    bool IsApproved);
