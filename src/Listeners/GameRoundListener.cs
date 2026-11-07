using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.GameEvents;
using SwiftlyS2.Shared.Misc;
using zFaceItScoreboard7GC.Services;

namespace zFaceItScoreboard7GC.Listeners;

public sealed class GameRoundListener
{
    private readonly IScoreboardService _scoreboardService;

    public GameRoundListener(IScoreboardService scoreboardService)
    {
        _scoreboardService = scoreboardService;
    }

    [GameEventHandler(HookMode.Post)]
    public HookResult OnPlayerSpawn(EventPlayerSpawn @event)
    {
        var player = @event.Accessor.GetPlayer("userid");
        if (player != null && player.IsValid && !player.IsFakeClient)
        {
            _scoreboardService.RefreshPlayer(player);
        }

        return HookResult.Continue;
    }

    [GameEventHandler(HookMode.Post)]
    public HookResult OnRoundFreezeEnd(EventRoundFreezeEnd @event)
    {
        _scoreboardService.RefreshAllPlayers();
        return HookResult.Continue;
    }

    [EventListener<EventDelegates.OnMapUnload>]
    public void OnMapUnload(IOnMapUnloadEvent @event)
    {
        // When match/map ends, clear all temporary fake faceit overrides
        _scoreboardService.ClearAllRuntimeFakes();
    }
}
