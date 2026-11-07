using zFaceItScoreboard7GC.Models;

namespace zFaceItScoreboard7GC.Services;

public interface IFaceitApiService : IDisposable
{
    Task<PlayerFaceitRecord?> FetchPlayerDataAsync(ulong steamId, bool defaultShowStatus, CancellationToken ct = default);
}
