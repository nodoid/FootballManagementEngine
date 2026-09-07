using FootballManagementEngine.Demo;
using FootballManagementEngine.Maui.Pages;
using Microsoft.Extensions.Logging;

namespace FootballManagementEngine.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // One session for the whole app: it owns the engine, the SQLite save and the calendar.
        builder.Services.AddSingleton(_ => GameSession.Open(FootballManagementEngine.Maui.Storage.DemoDatabasePath()));

        builder.Services.AddSingleton<ClubPage>();
        builder.Services.AddSingleton<FixturesPage>();
        builder.Services.AddSingleton<TablePage>();
        builder.Services.AddSingleton<SquadPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}

/// <summary>Where the demo save lives on the device.</summary>
public static class Storage
{
    public static string DemoDatabasePath() =>
        Path.Combine(FileSystem.AppDataDirectory, "football-demo.db");
}
