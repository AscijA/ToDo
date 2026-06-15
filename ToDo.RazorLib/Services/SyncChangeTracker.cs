using System.Text.Json;

namespace ToDo.RazorLib.Services;

public sealed class SyncChangeTracker {
    private const string LocalChangeKey = "Sync_LocalLastChangedAt";
    private const string EntityChangeKey = "Sync_EntityLastChangedAt";
    private const string DeletedEntityKey = "Sync_DeletedEntities";
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

    public void MarkLocalChange(params Guid[] entityIds) {
        var changedAt = DateTimeOffset.Now;
        settings.Set(LocalChangeKey, changedAt.ToUnixTimeMilliseconds().ToString());

        var cleanEntityIds = entityIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
        if (cleanEntityIds.Count == 0) {
            return;
        }

        var entityChanges = LoadEntityChanges();
        foreach (var entityId in cleanEntityIds) {
            entityChanges[entityId] = changedAt;
        }

        settings.Set(EntityChangeKey, JsonSerializer.Serialize(entityChanges));
    }

    public void MarkImported(IEnumerable<(Guid EntityId, DateTimeOffset LastModifiedAt)> entities) {
        var entityChanges = LoadEntityChanges();
        foreach (var (entityId, lastModifiedAt) in entities) {
            entityChanges[entityId] = lastModifiedAt;
        }

        settings.Set(EntityChangeKey, JsonSerializer.Serialize(entityChanges));
    }

    public IReadOnlyDictionary<Guid, DateTimeOffset> GetDeletedEntities() {
        return LoadDeletedEntities();
    }

    public void MarkDeleted(IEnumerable<Guid> entityIds) {
        var deletedEntities = LoadDeletedEntities();
        var deletedAt = DateTimeOffset.Now;
        foreach (var entityId in entityIds.Where(id => id != Guid.Empty)) {
            deletedEntities[entityId] = deletedAt;
        }

        settings.Set(DeletedEntityKey, JsonSerializer.Serialize(deletedEntities));
        MarkLocalChange();
    }

    public void MarkRemoteDeleted(IEnumerable<SyncDeletedEntitySnapshot> deletedEntities) {
        var localDeletedEntities = LoadDeletedEntities();
        foreach (var deletedEntity in deletedEntities) {
            localDeletedEntities[deletedEntity.Id] = deletedEntity.DeletedAt;
        }

        settings.Set(DeletedEntityKey, JsonSerializer.Serialize(localDeletedEntities));
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

    private Dictionary<Guid, DateTimeOffset> LoadDeletedEntities() {
        var json = settings.Get(DeletedEntityKey, "");
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
