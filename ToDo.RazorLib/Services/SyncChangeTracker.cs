using System.Text.Json;

namespace ToDo.RazorLib.Services;

public sealed class SyncChangeTracker {
    private const string LocalChangeKey = "Sync_LocalLastChangedAt";
    private const string EntityChangeKey = "Sync_EntityLastChangedAt";
    private readonly ISettingsService settings;

    public SyncChangeTracker(ISettingsService settings) {
        this.settings = settings;
    }

    public DateTimeOffset GetLastModified(Guid entityId) {
        var entityChanges = LoadEntityChanges();
        if (entityChanges.TryGetValue(entityId, out var entityChangedAt)) {
            return entityChangedAt;
        }

        return GetLocalLastChangedAt();
    }

    public void MarkLocalChange() {
        settings.Set(LocalChangeKey, DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString());
    }

    public void MarkImported(IEnumerable<(Guid EntityId, DateTimeOffset LastModifiedAt)> entities) {
        var entityChanges = LoadEntityChanges();
        foreach (var (entityId, lastModifiedAt) in entities) {
            entityChanges[entityId] = lastModifiedAt;
        }

        settings.Set(EntityChangeKey, JsonSerializer.Serialize(entityChanges));
    }

    private DateTimeOffset GetLocalLastChangedAt() {
        return long.TryParse(settings.Get(LocalChangeKey, ""), out var milliseconds)
            ? DateTimeOffset.FromUnixTimeMilliseconds(milliseconds)
            : DateTimeOffset.MinValue;
    }

    private Dictionary<Guid, DateTimeOffset> LoadEntityChanges() {
        var json = settings.Get(EntityChangeKey, "");
        if (string.IsNullOrWhiteSpace(json)) {
            return [];
        }

        try {
            return JsonSerializer.Deserialize<Dictionary<Guid, DateTimeOffset>>(json) ?? [];
        }
        catch (JsonException) {
            return [];
        }
    }
}
