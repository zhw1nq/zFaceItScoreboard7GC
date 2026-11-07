using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;

namespace zFaceItScoreboard7GC.Services;

public sealed class TranslationHelper : ITranslationHelper
{
    private readonly ISwiftlyCore _core;
    private const string DefaultPrefix = "[gold]FaceIT [silver]» ";

    public TranslationHelper(ISwiftlyCore core)
    {
        _core = core;
    }

    public async Task SendPlayerMessageAsync(IPlayer player, string key, params object[] args)
    {
        if (!player.IsValid || player.IsFakeClient)
            return;

        var localizer = _core.Translation.GetPlayerLocalizer(player);
        var prefix = localizer["prefix"];
        if (string.IsNullOrEmpty(prefix))
        {
            prefix = DefaultPrefix;
        }

        var message = args.Length > 0 ? localizer[key, args] : localizer[key];
        if (string.IsNullOrEmpty(message))
            return;

        var formatted = $"{prefix}{message}";
        await player.SendMessageAsync(MessageType.Chat, formatted).ConfigureAwait(false);
    }
}

