using System.Net.Http.Json;

namespace ToDo.RazorLib.Services;

public sealed class ManualAddressSyncDiscoveryService : ISyncDiscoveryService {
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(3);

    public async Task<SyncDiscoveryResult> DiscoverAsync(
        SyncDeviceIdentity localDevice,
        CancellationToken cancellationToken = default) {
        var address = NormalizeAddress(localDevice.ManualAddress);
        if (address == null) {
            return new SyncDiscoveryResult(Array.Empty<AvailableSyncDevice>(), null);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RequestTimeout);

        SyncHelloResponse? hello;
        try {
            using var httpClient = new HttpClient {
                Timeout = RequestTimeout
            };

            hello = await httpClient.GetFromJsonAsync<SyncHelloResponse>(
                $"http://{address}/sync/hello",
                timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
            return new SyncDiscoveryResult(Array.Empty<AvailableSyncDevice>(), $"No device answered at {address}. Make sure Sync is turned on there and the address is correct.");
        }
        catch (HttpRequestException) {
            return new SyncDiscoveryResult(Array.Empty<AvailableSyncDevice>(), $"Could not connect to {address}. Make sure both devices are on the same network and Sync is turned on.");
        }
        catch (NotSupportedException) {
            return new SyncDiscoveryResult(Array.Empty<AvailableSyncDevice>(), $"The device at {address} did not answer like a ToDo sync device.");
        }

        if (hello == null || string.IsNullOrWhiteSpace(hello.DeviceId) || string.IsNullOrWhiteSpace(hello.DeviceName)) {
            return new SyncDiscoveryResult(Array.Empty<AvailableSyncDevice>(), $"The device at {address} did not send enough information to pair.");
        }

        if (string.Equals(hello.DeviceId, localDevice.DeviceId, StringComparison.OrdinalIgnoreCase)) {
            return new SyncDiscoveryResult(Array.Empty<AvailableSyncDevice>(), "That address points to this device. Enter the address of the other device.");
        }

        var device = new AvailableSyncDevice(
            hello.DeviceId,
            hello.DeviceName,
            address,
            DateTimeOffset.Now);

        return new SyncDiscoveryResult([device], null);
    }

    private static string? NormalizeAddress(string address) {
        if (string.IsNullOrWhiteSpace(address)) {
            return null;
        }

        address = address.Trim();
        if (address.Contains("://", StringComparison.Ordinal)) {
            if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) || string.IsNullOrWhiteSpace(uri.Host)) {
                return null;
            }

            return uri.IsDefaultPort ? uri.Host : $"{uri.Host}:{uri.Port}";
        }

        if (!Uri.TryCreate($"sync://{address}", UriKind.Absolute, out var parsed) || string.IsNullOrWhiteSpace(parsed.Host)) {
            return null;
        }

        return parsed.IsDefaultPort ? parsed.Host : $"{parsed.Host}:{parsed.Port}";
    }
}
