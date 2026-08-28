using FootballManagementEngine;

// Database-backed startup is optional. Pass --load to resume the default SQLite save,
// or --db <path> --load <slot> to choose a database and save slot.
var argsList = args.ToList();
var databasePath = GetOption("--db") ?? "football-management.db";
var slot = GetOption("--slot") ?? "default";
var load = argsList.Contains("--load", StringComparer.OrdinalIgnoreCase);

var game = UkDatabase.Create(databasePath, load, autoSave: true, slot);
var season = new SeasonEngine(game);

if (!load)
{
    // Generate the initial season data only for a new game. Loading a save must never
    // regenerate fixtures, cups or player state over the restored database state.
    season.GenerateDomesticSeason();
    season.GenerateFaCup();

    game.State.Competitions["UCL"].TeamIds.AddRange(new[] { "ARS", "LIV", "MCI", "MUN", "CHE", "NEW" });
    game.State.Competitions["UEL"].TeamIds.AddRange(new[] { "AVL", "TOT", "BHA", "WHU" });
    game.State.Competitions["UECL"].TeamIds.AddRange(new[] { "CRY", "FUL" });
    season.GenerateEuropeanFixtures();
    game.Save(slot);
}

var api = new GameApi(game);
var teamsResponse = api.Handle("GET", GameApi.TeamsPath);
Console.WriteLine($"Teams API: HTTP {teamsResponse.StatusCode}");
Console.WriteLine(teamsResponse.Body);

Console.WriteLine();
Console.Write("Enter team ID to manage (e.g. ARS), or press Enter to keep the saved selection: ");
var teamId = Console.ReadLine()?.Trim();
if (!string.IsNullOrWhiteSpace(teamId))
{
    var selectionResponse = api.Handle("POST", GameApi.SelectTeamPath,
        $"{{\"teamId\":\"{teamId.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"}}");
    Console.WriteLine($"Select team API: HTTP {selectionResponse.StatusCode}");
    Console.WriteLine(selectionResponse.Body);
}

Console.WriteLine();
var gameResponse = api.Handle("GET", GameApi.GamePath);
Console.WriteLine($"Current game API: HTTP {gameResponse.StatusCode}");
Console.WriteLine(gameResponse.Body);

string? GetOption(string option)
{
    var index = argsList.FindIndex(a => string.Equals(a, option, StringComparison.OrdinalIgnoreCase));
    return index >= 0 && index + 1 < argsList.Count ? argsList[index + 1] : null;
}
