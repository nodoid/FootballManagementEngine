using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Microsoft.Xna.Framework;

namespace FootballManagementEngine.MonoGameDemo;

[Activity(
    Label = "FM Engine Demo",
    MainLauncher = true,
    Theme = "@style/Theme.Demo",
    AlwaysRetainTaskState = true,
    LaunchMode = LaunchMode.SingleInstance,
    ScreenOrientation = ScreenOrientation.Portrait,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.Keyboard |
                           ConfigChanges.KeyboardHidden | ConfigChanges.ScreenSize)]
public class MainActivity : AndroidGameActivity
{
    private DemoGame? _game;
    private View? _view;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // FilesDir is the app's private, writable storage - where the SQLite save belongs.
        var databasePath = Path.Combine(
            FilesDir?.AbsolutePath ?? System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
            "football-demo.db");

        _game = new DemoGame(databasePath);
        _view = (View)_game.Services.GetService(typeof(View));

        SetContentView(_view);
        _game.Run();
    }

    protected override void OnDestroy()
    {
        _game?.Dispose();
        base.OnDestroy();
    }
}
