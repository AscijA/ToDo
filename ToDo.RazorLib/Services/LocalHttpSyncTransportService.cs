using System.Net;
using System.Net.Sockets;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ToDo.RazorLib.Services;

[UnsupportedOSPlatform("browser")]
public sealed class LocalHttpSyncTransportService : ISyncTransportService {
    private const int DefaultPort = 51234;
    private const int MaxRequestBodyBytes = 10 * 1024 * 1024;
    private const string PairedDevicesKey = "Sync_PairedDevices";
    private const string PairedDeviceTrustTokenPrefix = "Sync_PairedDeviceTrustToken_";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly object gate = new();
    private readonly ISettingsService settings;
    private readonly ISecureSettingsService secureSettings;
    private readonly ISyncSnapshotService snapshotService;
    private readonly ISyncSnapshotImportService snapshotImportService;
    private readonly Dictionary<string, PendingPairing> pendingPairings = new(StringComparer.OrdinalIgnoreCase);
    private TcpListener? listener;
    private CancellationTokenSource? listenerCancellation;
    private Task? listenerTask;
    private SyncDeviceIdentity? currentIdentity;
    private SyncTransportStatus status = new(false, null, null, null, null);

    public LocalHttpSyncTransportService(
        ISettingsService settings,
        ISecureSettingsService secureSettings,
        ISyncSnapshotService snapshotService,
        ISyncSnapshotImportService snapshotImportService) {
        this.settings = settings;
        this.secureSettings = secureSettings;
        this.snapshotService = snapshotService;
        this.snapshotImportService = snapshotImportService;
    }

    public SyncTransportStatus GetStatus() {
        lock (gate) {
            return status;
        }
    }

    public IReadOnlyList<IncomingPairingRequest> GetIncomingPairingRequests() {
        lock (gate) {
            RemoveExpiredPairings();
            return pendingPairings
                .Select(pairing => new IncomingPairingRequest(
                    pairing.Key,
                    pairing.Value.DeviceId,
                    pairing.Value.DeviceName,
                    pairing.Value.Address,
                    pairing.Value.VerificationCode,
                    pairing.Value.ExpiresAt,
                    pairing.Value.IsApproved))
                .ToList();
        }
    }

    public void ApproveIncomingPairing(string sessionId) {
        lock (gate) {
            if (pendingPairings.TryGetValue(sessionId, out var pending) && pending.ExpiresAt >= DateTimeOffset.Now) {
                pendingPairings[sessionId] = pending with { IsApproved = true };
            }
        }
    }

    public void RejectIncomingPairing(string sessionId) {
        lock (gate) {
            pendingPairings.Remove(sessionId);
        }
    }

    public async Task StartAsync(SyncDeviceIdentity identity, CancellationToken cancellationToken = default) {
        if (!identity.SyncEnabled) {
            await StopAsync();
            return;
        }

        lock (gate) {
            if (status.IsRunning) {
                currentIdentity = identity;
                return;
            }
        }

        try {
            var tcpListener = StartListener();
            var endpoint = (IPEndPoint)tcpListener.LocalEndpoint;
            var address = GetLocalAddress() ?? "127.0.0.1";
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            lock (gate) {
                listener = tcpListener;
                listenerCancellation = cts;
                currentIdentity = identity;
                status = new(true, address, endpoint.Port, DateTimeOffset.Now, null);
                listenerTask = Task.Run(() => AcceptLoopAsync(tcpListener, cts.Token), CancellationToken.None);
            }
        }
        catch (Exception ex) when (ex is SocketException or InvalidOperationException) {
            lock (gate) {
                status = new(false, null, null, null, ex.Message);
            }
        }
    }

    public async Task StopAsync() {
        Task? task;
        CancellationTokenSource? cts;
        TcpListener? tcpListener;

        lock (gate) {
            task = listenerTask;
            cts = listenerCancellation;
            tcpListener = listener;
            listenerTask = null;
            listenerCancellation = null;
            listener = null;
            currentIdentity = null;
            status = new(false, null, null, null, null);
        }

        cts?.Cancel();
        tcpListener?.Stop();

        if (task != null) {
            try {
                await task;
            }
            catch (OperationCanceledException) {
            }
            catch (SocketException) {
            }
        }

        cts?.Dispose();
    }

    public async ValueTask DisposeAsync() {
        await StopAsync();
    }

    private static TcpListener StartListener() {
        try {
            var listener = new TcpListener(IPAddress.Any, DefaultPort);
            listener.Start();
            return listener;
        }
        catch (SocketException) {
            var listener = new TcpListener(IPAddress.Any, 0);
            listener.Start();
            return listener;
        }
    }

    private async Task AcceptLoopAsync(TcpListener tcpListener, CancellationToken cancellationToken) {
        while (!cancellationToken.IsCancellationRequested) {
            var client = await tcpListener.AcceptTcpClientAsync(cancellationToken);
            _ = Task.Run(() => HandleClientAsync(client, cancellationToken), CancellationToken.None);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken) {
        using var _ = client;
        using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
        try {
            var requestLine = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(requestLine)) {
                return;
            }

            var parts = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) {
                await WriteResponseAsync(stream, 405, "Method Not Allowed", "text/plain", "Method not allowed", cancellationToken);
                return;
            }

            var method = parts[0];
            var path = parts[1];

            if (string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(path, "/sync/hello", StringComparison.OrdinalIgnoreCase)) {
                await ReadHeadersAsync(reader, cancellationToken);
                await HandleHelloAsync(stream, cancellationToken);
                return;
            }

            if (string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(path, "/sync/pair/start", StringComparison.OrdinalIgnoreCase)) {
                var body = await ReadBodyAsync(reader, cancellationToken);
                await HandlePairStartAsync(stream, body, cancellationToken);
                return;
            }

            if (string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(path, "/sync/pair/confirm", StringComparison.OrdinalIgnoreCase)) {
                var body = await ReadBodyAsync(reader, cancellationToken);
                await HandlePairConfirmAsync(stream, body, cancellationToken);
                return;
            }

            if (string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(path, "/sync/ping", StringComparison.OrdinalIgnoreCase)) {
                var body = await ReadBodyAsync(reader, cancellationToken);
                await HandlePingAsync(stream, body, cancellationToken);
                return;
            }

            if (string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(path, "/sync/snapshot", StringComparison.OrdinalIgnoreCase)) {
                var body = await ReadBodyAsync(reader, cancellationToken);
                await HandleSnapshotAsync(stream, body, cancellationToken);
                return;
            }

            if (string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(path, "/sync/import-new", StringComparison.OrdinalIgnoreCase)) {
                var body = await ReadBodyAsync(reader, cancellationToken);
                await HandleImportNewAsync(stream, body, cancellationToken);
                return;
            }

            if (!string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase) && !string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase)) {
                await ReadHeadersAsync(reader, cancellationToken);
                await WriteResponseAsync(stream, 405, "Method Not Allowed", "text/plain", "Method not allowed", cancellationToken);
                return;
            }

            await ReadHeadersAsync(reader, cancellationToken);
            await WriteResponseAsync(stream, 404, "Not Found", "text/plain", "Not found", cancellationToken);
        }
        catch (RequestBodyTooLargeException) {
            await WriteResponseAsync(stream, 413, "Payload Too Large", "text/plain", "Sync request is too large", cancellationToken);
        }
    }

    private async Task HandleHelloAsync(NetworkStream stream, CancellationToken cancellationToken) {
        var identity = currentIdentity;
        if (identity == null) {
            await WriteResponseAsync(stream, 503, "Service Unavailable", "text/plain", "Sync is not available", cancellationToken);
            return;
        }

        var response = new SyncHelloResponse(identity.DeviceId, identity.DeviceName, "1");
        var json = JsonSerializer.Serialize(response, JsonOptions);
        await WriteResponseAsync(stream, 200, "OK", "application/json", json, cancellationToken);
    }

    private async Task HandlePairStartAsync(NetworkStream stream, string body, CancellationToken cancellationToken) {
        var identity = currentIdentity;
        if (identity == null) {
            await WriteResponseAsync(stream, 503, "Service Unavailable", "text/plain", "Sync is not available", cancellationToken);
            return;
        }

        var request = DeserializeBody<SyncPairStartRequest>(body);
        if (request == null || string.IsNullOrWhiteSpace(request.DeviceId) || string.IsNullOrWhiteSpace(request.DeviceName)) {
            await WriteResponseAsync(stream, 400, "Bad Request", "text/plain", "Invalid pairing request", cancellationToken);
            return;
        }

        if (string.Equals(request.DeviceId, identity.DeviceId, StringComparison.OrdinalIgnoreCase)) {
            await WriteResponseAsync(stream, 400, "Bad Request", "text/plain", "Cannot pair with self", cancellationToken);
            return;
        }

        var sessionId = Guid.NewGuid().ToString("N");
        var code = GeneratePairingCode();
        var expiresAt = DateTimeOffset.Now.AddMinutes(5);
        var remoteTrustToken = GenerateTrustToken();

        lock (gate) {
            pendingPairings[sessionId] = new PendingPairing(
                request.DeviceId,
                request.DeviceName,
                request.Address,
                code,
                expiresAt,
                remoteTrustToken,
                false);
        }

        var currentStatus = GetStatus();
        var responseAddress = currentStatus is { Address: not null, Port: not null }
            ? $"{currentStatus.Address}:{currentStatus.Port}"
            : string.Empty;
        var response = new SyncPairStartResponse(identity.DeviceId, identity.DeviceName, responseAddress, sessionId, code, expiresAt);
        await WriteJsonResponseAsync(stream, response, cancellationToken);
    }

    private async Task HandlePairConfirmAsync(NetworkStream stream, string body, CancellationToken cancellationToken) {
        var identity = currentIdentity;
        if (identity == null) {
            await WriteResponseAsync(stream, 503, "Service Unavailable", "text/plain", "Sync is not available", cancellationToken);
            return;
        }

        var request = DeserializeBody<SyncPairConfirmRequest>(body);
        if (request == null || string.IsNullOrWhiteSpace(request.SessionId)) {
            await WriteResponseAsync(stream, 400, "Bad Request", "text/plain", "Invalid confirmation request", cancellationToken);
            return;
        }

        PendingPairing? pending;
        lock (gate) {
            pendingPairings.TryGetValue(request.SessionId, out pending);
        }

        if (pending == null || pending.ExpiresAt < DateTimeOffset.Now) {
            await WriteResponseAsync(stream, 404, "Not Found", "text/plain", "Pairing session expired", cancellationToken);
            return;
        }

        if (!pending.IsApproved) {
            await WriteResponseAsync(stream, 409, "Conflict", "text/plain", "Pairing request is waiting for approval", cancellationToken);
            return;
        }

        if (!string.Equals(NormalizePairingCode(request.VerificationCode), NormalizePairingCode(pending.VerificationCode), StringComparison.Ordinal) ||
            !string.Equals(request.DeviceId, pending.DeviceId, StringComparison.OrdinalIgnoreCase)) {
            await WriteResponseAsync(stream, 403, "Forbidden", "text/plain", "Pairing code mismatch", cancellationToken);
            return;
        }

        AddOrReplacePairedDevice(new PairedSyncDevice(
            pending.DeviceId,
            pending.DeviceName,
            string.IsNullOrWhiteSpace(request.Address) ? pending.Address : request.Address,
            DateTimeOffset.Now,
            null,
            true,
            request.TrustToken));

        lock (gate) {
            pendingPairings.Remove(request.SessionId);
        }

        var currentStatus = GetStatus();
        var responseAddress = currentStatus is { Address: not null, Port: not null }
            ? $"{currentStatus.Address}:{currentStatus.Port}"
            : string.Empty;
        var response = new SyncPairConfirmResponse(identity.DeviceId, identity.DeviceName, responseAddress, request.TrustToken);
        await WriteJsonResponseAsync(stream, response, cancellationToken);
    }

    private async Task HandlePingAsync(NetworkStream stream, string body, CancellationToken cancellationToken) {
        var identity = currentIdentity;
        if (identity == null) {
            await WriteResponseAsync(stream, 503, "Service Unavailable", "text/plain", "Sync is not available", cancellationToken);
            return;
        }

        var request = DeserializeBody<SyncPingRequest>(body);
        if (request == null || string.IsNullOrWhiteSpace(request.DeviceId) || string.IsNullOrWhiteSpace(request.TrustToken)) {
            await WriteResponseAsync(stream, 400, "Bad Request", "text/plain", "Invalid ping request", cancellationToken);
            return;
        }

        if (!await TryAuthorizePairedDeviceAsync(stream, request.DeviceId, request.TrustToken, cancellationToken)) {
            return;
        }

        var response = new SyncPingResponse(identity.DeviceId, identity.DeviceName, DateTimeOffset.Now);
        await WriteJsonResponseAsync(stream, response, cancellationToken);
    }

    private async Task HandleSnapshotAsync(NetworkStream stream, string body, CancellationToken cancellationToken) {
        var identity = currentIdentity;
        if (identity == null) {
            await WriteResponseAsync(stream, 503, "Service Unavailable", "text/plain", "Sync is not available", cancellationToken);
            return;
        }

        var request = DeserializeBody<SyncSnapshotRequest>(body);
        if (request == null || string.IsNullOrWhiteSpace(request.DeviceId) || string.IsNullOrWhiteSpace(request.TrustToken)) {
            await WriteResponseAsync(stream, 400, "Bad Request", "text/plain", "Invalid snapshot request", cancellationToken);
            return;
        }

        if (!await TryAuthorizePairedDeviceAsync(stream, request.DeviceId, request.TrustToken, cancellationToken)) {
            return;
        }

        var snapshot = await snapshotService.CreateSnapshotAsync(identity, cancellationToken);
        await WriteJsonResponseAsync(stream, snapshot, cancellationToken);
    }

    private async Task HandleImportNewAsync(NetworkStream stream, string body, CancellationToken cancellationToken) {
        var identity = currentIdentity;
        if (identity == null) {
            await WriteResponseAsync(stream, 503, "Service Unavailable", "text/plain", "Sync is not available", cancellationToken);
            return;
        }

        var request = DeserializeBody<SyncImportNewRequest>(body);
        if (request == null ||
            string.IsNullOrWhiteSpace(request.DeviceId) ||
            string.IsNullOrWhiteSpace(request.TrustToken) ||
            request.Snapshot == null) {
            await WriteResponseAsync(stream, 400, "Bad Request", "text/plain", "Invalid import request", cancellationToken);
            return;
        }

        if (!string.Equals(request.Snapshot.DeviceId, request.DeviceId, StringComparison.OrdinalIgnoreCase)) {
            await WriteResponseAsync(stream, 400, "Bad Request", "text/plain", "Snapshot device does not match request device", cancellationToken);
            return;
        }

        if (!string.Equals(request.Snapshot.ProtocolVersion, "1", StringComparison.Ordinal)) {
            await WriteResponseAsync(stream, 400, "Bad Request", "text/plain", "Unsupported sync snapshot version", cancellationToken);
            return;
        }

        if (!await TryAuthorizePairedDeviceAsync(stream, request.DeviceId, request.TrustToken, cancellationToken)) {
            return;
        }

        var import = await snapshotImportService.ImportNewAsync(request.Snapshot, cancellationToken: cancellationToken);
        AddOrReplacePairedDevice(LoadPairedDevices()
            .First(device => string.Equals(device.DeviceId, request.DeviceId, StringComparison.OrdinalIgnoreCase))
            with { LastSyncedAt = DateTimeOffset.Now, IsOnline = true });
        await WriteJsonResponseAsync(stream, import with { DeviceName = identity.DeviceName }, cancellationToken);
    }

    private async Task<bool> TryAuthorizePairedDeviceAsync(
        NetworkStream stream,
        string deviceId,
        string trustToken,
        CancellationToken cancellationToken) {
        var pairedDevice = LoadPairedDevices()
            .FirstOrDefault(device => string.Equals(device.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase));

        if (pairedDevice == null) {
            await WriteResponseAsync(stream, 401, "Unauthorized", "text/plain", "Device is not paired", cancellationToken);
            return false;
        }

        if (!FixedTimeEquals(pairedDevice.TrustToken, trustToken)) {
            await WriteResponseAsync(stream, 403, "Forbidden", "text/plain", "Invalid trust token", cancellationToken);
            return false;
        }

        return true;
    }

    private async Task<string> ReadBodyAsync(StreamReader reader, CancellationToken cancellationToken) {
        var contentLength = await ReadHeadersAsync(reader, cancellationToken);

        if (contentLength <= 0) {
            return string.Empty;
        }

        var buffer = new char[contentLength];
        var read = 0;
        while (read < contentLength) {
            var count = await reader.ReadAsync(buffer.AsMemory(read, contentLength - read), cancellationToken);
            if (count == 0) {
                break;
            }

            read += count;
        }

        return new string(buffer, 0, read);
    }

    private static async Task<int> ReadHeadersAsync(StreamReader reader, CancellationToken cancellationToken) {
        var contentLength = 0;
        string? line;
        while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync(cancellationToken))) {
            const string prefix = "Content-Length:";
            if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(line[prefix.Length..].Trim(), out var parsedLength)) {
                if (parsedLength > MaxRequestBodyBytes) {
                    throw new RequestBodyTooLargeException();
                }

                contentLength = parsedLength;
            }
        }

        return contentLength;
    }

    private static T? DeserializeBody<T>(string body) {
        if (string.IsNullOrWhiteSpace(body)) {
            return default;
        }

        try {
            return JsonSerializer.Deserialize<T>(body, JsonOptions);
        }
        catch (JsonException) {
            return default;
        }
    }

    private static Task WriteJsonResponseAsync<T>(NetworkStream stream, T response, CancellationToken cancellationToken) {
        var json = JsonSerializer.Serialize(response, JsonOptions);
        return WriteResponseAsync(stream, 200, "OK", "application/json", json, cancellationToken);
    }

    private static bool FixedTimeEquals(string left, string right) {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);

        return leftBytes.Length == rightBytes.Length &&
               CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private sealed class RequestBodyTooLargeException : Exception;

    private void AddOrReplacePairedDevice(PairedSyncDevice device) {
        var devices = LoadPairedDevices();
        devices = devices
            .Where(existing => !string.Equals(existing.DeviceId, device.DeviceId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        devices.Add(device);
        SavePairedDevices(devices);
    }

    private List<PairedSyncDevice> LoadPairedDevices() {
        var json = settings.Get(PairedDevicesKey, "");
        if (string.IsNullOrWhiteSpace(json)) {
            return [];
        }

        try {
            var devices = JsonSerializer.Deserialize<List<PairedSyncDevice>>(json) ?? [];
            return devices.Select(HydrateTrustToken).ToList();
        }
        catch (JsonException) {
            return [];
        }
    }

    private void SavePairedDevices(IReadOnlyList<PairedSyncDevice> devices) {
        foreach (var device in devices.Where(device => !string.IsNullOrWhiteSpace(device.TrustToken))) {
            secureSettings.Set(GetTrustTokenKey(device.DeviceId), device.TrustToken);
        }

        var storedDevices = devices
            .Select(device => device with { TrustToken = string.Empty })
            .ToList();
        settings.Set(PairedDevicesKey, JsonSerializer.Serialize(storedDevices));
    }

    private static string GeneratePairingCode() {
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        return $"{code[..3]}-{code[3..]}";
    }

    private static string GenerateTrustToken() {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    private PairedSyncDevice HydrateTrustToken(PairedSyncDevice device) {
        if (!string.IsNullOrWhiteSpace(device.TrustToken)) {
            secureSettings.Set(GetTrustTokenKey(device.DeviceId), device.TrustToken);
        }

        var trustToken = secureSettings.Get(GetTrustTokenKey(device.DeviceId), device.TrustToken);
        return device with { TrustToken = trustToken };
    }

    private static string GetTrustTokenKey(string deviceId) {
        return $"{PairedDeviceTrustTokenPrefix}{deviceId}";
    }

    private static string NormalizePairingCode(string code) {
        return new string(code.Where(char.IsDigit).ToArray());
    }

    private sealed record PendingPairing(
        string DeviceId,
        string DeviceName,
        string Address,
        string VerificationCode,
        DateTimeOffset ExpiresAt,
        string RemoteTrustToken,
        bool IsApproved);

    private void RemoveExpiredPairings() {
        var now = DateTimeOffset.Now;
        foreach (var expiredSessionId in pendingPairings
                     .Where(pairing => pairing.Value.ExpiresAt < now)
                     .Select(pairing => pairing.Key)
                     .ToList()) {
            pendingPairings.Remove(expiredSessionId);
        }
    }

    private static async Task WriteResponseAsync(
        NetworkStream stream,
        int statusCode,
        string reason,
        string contentType,
        string body,
        CancellationToken cancellationToken) {
        var bodyBytes = Encoding.UTF8.GetBytes(body);
        var header = $"HTTP/1.1 {statusCode} {reason}\r\nContent-Type: {contentType}; charset=utf-8\r\nContent-Length: {bodyBytes.Length}\r\nConnection: close\r\n\r\n";
        var headerBytes = Encoding.ASCII.GetBytes(header);
        await stream.WriteAsync(headerBytes, cancellationToken);
        await stream.WriteAsync(bodyBytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static string? GetLocalAddress() {
        try {
            return Dns.GetHostEntry(Dns.GetHostName())
                .AddressList
                .Where(address => address.AddressFamily == AddressFamily.InterNetwork)
                .Select(address => address.ToString())
                .FirstOrDefault(address => !address.StartsWith("127.", StringComparison.Ordinal));
        }
        catch (SocketException) {
            return null;
        }
    }
}
