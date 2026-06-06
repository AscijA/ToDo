using System.Net;
using System.Net.Sockets;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;

namespace ToDo.RazorLib.Services;

[UnsupportedOSPlatform("browser")]
public sealed class LocalHttpSyncTransportService : ISyncTransportService {
    private const int DefaultPort = 51234;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly object gate = new();
    private TcpListener? listener;
    private CancellationTokenSource? listenerCancellation;
    private Task? listenerTask;
    private SyncDeviceIdentity? currentIdentity;
    private SyncTransportStatus status = new(false, null, null, null, null);

    public SyncTransportStatus GetStatus() {
        lock (gate) {
            return status;
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
        var requestLine = await reader.ReadLineAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(requestLine)) {
            return;
        }

        while (!string.IsNullOrEmpty(await reader.ReadLineAsync(cancellationToken))) {
        }

        var parts = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || !string.Equals(parts[0], "GET", StringComparison.OrdinalIgnoreCase)) {
            await WriteResponseAsync(stream, 405, "Method Not Allowed", "text/plain", "Method not allowed", cancellationToken);
            return;
        }

        if (!string.Equals(parts[1], "/sync/hello", StringComparison.OrdinalIgnoreCase)) {
            await WriteResponseAsync(stream, 404, "Not Found", "text/plain", "Not found", cancellationToken);
            return;
        }

        var identity = currentIdentity;
        if (identity == null) {
            await WriteResponseAsync(stream, 503, "Service Unavailable", "text/plain", "Sync is not available", cancellationToken);
            return;
        }

        var response = new SyncHelloResponse(identity.DeviceId, identity.DeviceName, "1");
        var json = JsonSerializer.Serialize(response, JsonOptions);
        await WriteResponseAsync(stream, 200, "OK", "application/json", json, cancellationToken);
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
