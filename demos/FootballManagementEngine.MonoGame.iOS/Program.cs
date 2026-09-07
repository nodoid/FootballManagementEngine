using Foundation;
using Microsoft.Xna.Framework;
using UIKit;

namespace FootballManagementEngine.MonoGameDemo;

[Register("AppDelegate")]
public class AppDelegate : UIApplicationDelegate
{
    private DemoGame? _game;

    public override UIWindow? Window { get; set; }

    public override bool FinishedLaunching(UIApplication application, NSDictionary? launchOptions)
    {
        // Library/ is the iOS app's private, writable storage - where the SQLite save belongs,
        // and the folder Environment.SpecialFolder.LocalApplicationData maps to.
        var databasePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "football-demo.db");

        _game = new DemoGame(databasePath);
        _game.Run();
        return true;
    }
}

public static class Program
{
    public static void Main(string[] args) => UIApplication.Main(args, null, typeof(AppDelegate));
}
