using zFaceItScoreboard7GC.Models;

namespace zFaceItScoreboard7GC.Services;

public interface IPlayerDataService : IDisposable
{
    PlayerFaceitRecord GetOrCreate(ulong steamId, bool defaultShowStatus);
    bool TryGetPlayer(ulong steamId, out PlayerFaceitRecord? record);
    void UpdatePlayer(PlayerFaceitRecord record);
    void MarkDirty();
    void SaveIfDirty();
    Task LoadAsync(CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
    int Count { get; }
}

