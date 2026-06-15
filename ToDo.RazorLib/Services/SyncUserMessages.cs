namespace ToDo.RazorLib.Services;

public static class SyncUserMessages {
    public static string Explain(Exception exception) {
        return Explain(exception.Message);
    }

    public static string Explain(string? message) {
        if (string.IsNullOrWhiteSpace(message)) {
            return "Something went wrong. Please try again.";
        }

        var text = message.Trim();

        if (text.Contains("Pair start failed", StringComparison.OrdinalIgnoreCase)) {
            if (text.Contains("No connection could be made", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("actively refused", StringComparison.OrdinalIgnoreCase)) {
                return "Could not contact that device. Make sure Sync is turned on there and the address is correct.";
            }

            if (text.Contains("Cannot pair with self", StringComparison.OrdinalIgnoreCase)) {
                return "That address points to this device. Enter the address of the other device.";
            }

            return "Could not start pairing. Make sure the other device has Sync turned on and try again.";
        }

        if (text.Contains("Pair start returned an invalid response", StringComparison.OrdinalIgnoreCase)) {
            return "The other device did not answer like a ToDo sync device. Check the address and try again.";
        }

        if (text.Contains("Pair confirm failed", StringComparison.OrdinalIgnoreCase)) {
            if (text.Contains("waiting for approval", StringComparison.OrdinalIgnoreCase)) {
                return "The other device has not approved the pairing yet.";
            }

            if (text.Contains("Pairing code mismatch", StringComparison.OrdinalIgnoreCase)) {
                return "The pairing code does not match. Check the code on both devices and try again.";
            }

            if (text.Contains("Pairing session expired", StringComparison.OrdinalIgnoreCase)) {
                return "The pairing request expired. Start pairing again.";
            }

            return "Could not finish pairing. Check both devices and try again.";
        }

        if (text.Contains("Ping failed", StringComparison.OrdinalIgnoreCase)) {
            return "The device did not respond. Make sure it is on the same network and Sync is turned on.";
        }

        if (text.Contains("Ping returned an invalid device identity", StringComparison.OrdinalIgnoreCase)) {
            return "The device answered, but it does not look like the paired device.";
        }

        if (text.Contains("Snapshot fetch failed", StringComparison.OrdinalIgnoreCase)) {
            if (text.Contains("Device is not paired", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("Invalid trust token", StringComparison.OrdinalIgnoreCase)) {
                return "This device is no longer trusted by the other device. Remove it and pair again.";
            }

            return "Could not read data from the other device. Make sure Sync is turned on there and try again.";
        }

        if (text.Contains("Snapshot returned an invalid device identity", StringComparison.OrdinalIgnoreCase)) {
            return "The device answered, but it does not look like the paired device.";
        }

        if (text.Contains("Push failed", StringComparison.OrdinalIgnoreCase)) {
            if (text.Contains("Payload Too Large", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("request is too large", StringComparison.OrdinalIgnoreCase)) {
                return "There is too much data to send at once. Try syncing after both devices have received the latest changes.";
            }

            if (text.Contains("Device is not paired", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("Invalid trust token", StringComparison.OrdinalIgnoreCase)) {
                return "This device is no longer trusted by the other device. Remove it and pair again.";
            }

            return "Could not send changes to the other device. Make sure Sync is turned on there and try again.";
        }

        if (text.Contains("Push returned an invalid response", StringComparison.OrdinalIgnoreCase)) {
            return "The other device answered, but it did not confirm the sync correctly.";
        }

        if (text.Contains("No connection could be made", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("actively refused", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("Connection refused", StringComparison.OrdinalIgnoreCase)) {
            return "Could not connect. Make sure the other device is awake, on the same network, and has Sync turned on.";
        }

        if (text.Contains("timed out", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("operation was canceled", StringComparison.OrdinalIgnoreCase)) {
            return "The other device did not respond in time. Check the address and network connection.";
        }

        return text;
    }

    public static string Discovery(string? message) {
        if (string.IsNullOrWhiteSpace(message)) {
            return string.Empty;
        }

        return Explain(message);
    }
}
