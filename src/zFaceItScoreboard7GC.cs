using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Plugins;
using zFaceItScoreboard7GC.Commands;
using zFaceItScoreboard7GC.Configuration;
using zFaceItScoreboard7GC.Listeners;
using zFaceItScoreboard7GC.Services;

namespace zFaceItScoreboard7GC;

[PluginMetadata(
    Id = "zFaceItScoreboard7GC",
    Version = "1.0.0",
    Name = "zFaceItScoreboard7GC",
    Author = "vhming",
    Description = "FaceIT Scoreboard for CS2 (SwiftlyS2)"
)]
public partial class zFaceItScoreboard7GC : BasePlugin
{
    private ServiceProvider? _serviceProvider;
    private IPlayerDataService? _dataService;
    private IScoreboardService? _scoreboardService;
    private CancellationTokenSource? _autoSaveTimerToken;
    private CancellationTokenSource? _safetyCheckTimerToken;

    public zFaceItScoreboard7GC(ISwiftlyCore core) : base(core)
    {
    }

    public override void ConfigureSharedInterface(IInterfaceManager interfaceManager)
    {
    }

    public override void UseSharedInterface(IInterfaceManager interfaceManager)
    {
    }

    public override void Load(bool hotReload)
    {
        InitializeConfiguration();
        InitializeDependencyInjection();
        RegisterHandlers();
        StartTimers();

        Core.Logger.LogInformation("[FaceIT] zFaceItScoreboard7GC loaded successfully.");
    }

    public override void Unload()
    {
        _autoSaveTimerToken?.Cancel();
        _autoSaveTimerToken = null;

        _safetyCheckTimerToken?.Cancel();
        _safetyCheckTimerToken = null;

        _dataService?.SaveIfDirty();

        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }

        _serviceProvider = null;
        _dataService = null;
        _scoreboardService = null;
    }

    private void InitializeConfiguration()
    {
        Core.Configuration
            .InitializeWithTemplate("config.jsonc", "config.template.jsonc")
            .Configure(builder =>
            {
                builder.AddJsonFile("config.jsonc", optional: false, reloadOnChange: true);
            });
    }

    private void InitializeDependencyInjection()
    {
        var services = new ServiceCollection();

        services.AddSwiftly(Core)
            .AddOptionsWithValidateOnStart<FaceitConfig>()
            .BindConfiguration("FaceIT");

        services.AddSingleton<ITranslationHelper, TranslationHelper>();
        services.AddSingleton<IFaceitApiService, FaceitApiService>();

        var dataPath = Path.Combine(Core.PluginDataDirectory, "faceit_cache.json");
        services.AddSingleton<IPlayerDataService>(sp =>
            new PlayerDataService(dataPath, sp.GetRequiredService<ILogger<PlayerDataService>>()));

        services.AddSingleton<IScoreboardService, ScoreboardService>();

        services.AddSingleton<PlayerConnectionListener>();
        services.AddSingleton<GameRoundListener>();
        services.AddSingleton<FaceitCommands>();

        _serviceProvider = services.BuildServiceProvider();

        _dataService = _serviceProvider.GetRequiredService<IPlayerDataService>();
        _scoreboardService = _serviceProvider.GetRequiredService<IScoreboardService>();

        _ = _dataService.LoadAsync();
    }

    private void RegisterHandlers()
    {
        if (_serviceProvider == null)
            return;

        Core.Registrator.Register(_serviceProvider.GetRequiredService<PlayerConnectionListener>());
        Core.Registrator.Register(_serviceProvider.GetRequiredService<GameRoundListener>());
        Core.Registrator.Register(_serviceProvider.GetRequiredService<FaceitCommands>());
    }

    private void StartTimers()
    {
        if (_serviceProvider == null)
            return;

        var config = _serviceProvider.GetRequiredService<IOptionsMonitor<FaceitConfig>>().CurrentValue;

        if (config.AutoSaveIntervalSeconds > 0)
        {
            _autoSaveTimerToken = Core.Scheduler.RepeatBySeconds(
                config.AutoSaveIntervalSeconds,
                () => _dataService?.SaveIfDirty());
        }

        if (config.SafetyCheckIntervalSeconds > 0)
        {
            _safetyCheckTimerToken = Core.Scheduler.RepeatBySeconds(
                config.SafetyCheckIntervalSeconds,
                () => _scoreboardService?.RefreshAllPlayers());
        }
    }
}
