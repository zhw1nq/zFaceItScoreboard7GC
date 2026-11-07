using SwiftlyS2.Shared.Players;

namespace zFaceItScoreboard7GC.Services;

public interface ITranslationHelper
{
    Task SendPlayerMessageAsync(IPlayer player, string key, params object[] args);
}

