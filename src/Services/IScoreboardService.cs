using SwiftlyS2.Shared.Players;
using zFaceItScoreboard7GC.Models;

namespace zFaceItScoreboard7GC.Services;

public interface IScoreboardService
{
    int ResolveCoinId(PlayerFaceitRecord record);
    void ApplyCoin(IPlayer player, int coinId);
    void RemoveCoin(IPlayer player);
    void RefreshPlayer(IPlayer player);
    void RefreshAllPlayers();
    void SetRuntimeFake(ulong steamId, string fakeValue);
    void ClearRuntimeFake(ulong steamId);
    void ClearAllRuntimeFakes();
    string? GetEffectiveFake(ulong steamId);
    string GetFakeDisplayLabel(string fakeKey);
}
