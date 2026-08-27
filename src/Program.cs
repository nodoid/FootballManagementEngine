using FootballManagementEngine;

var game = UkDatabase.Create();
var season = new SeasonEngine(game);

// Generate the initial season data once at startup.
season.GenerateDomesticSeason();
season.GenerateFaCup();

game.State.Competitions["UCL"].TeamIds.AddRange(
    new[] { "ARS", "LIV", "MCI", "MUN", "CHE", "NEW" });
game.State.Competitions["UEL"].TeamIds.AddRange(
    new[] { "AVL", "TOT", "BHA", "WHU" });
game.State.Competitions["UECL"].TeamIds.AddRange(
    new[] { "CRY", "FUL" });
season.GenerateEuropeanFixtures();

// Server-agnostic API layer. A web server, desktop app, mobile app, test,
// or another process can call GameApi.Handle(method, path, body).
var api = new GameApi(game);

var teamsResponse = api.Handle("GET", GameApi.TeamsPath);
Console.WriteLine($"Teams API: HTTP {teamsResponse.StatusCode}");
Console.WriteLine(teamsResponse.Body);

Console.WriteLine();
Console.Write("Enter team ID to manage (e.g. ARS): ");
var teamId = Console.ReadLine()?.Trim() ?? "";

var selectionResponse = api.Handle(
    "POST",
    GameApi.SelectTeamPath,
    $"{{\"teamId\":\"{teamId.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"}}");

Console.WriteLine($"Select team API: HTTP {selectionResponse.StatusCode}");
Console.WriteLine(selectionResponse.Body);

var gameResponse = api.Handle("GET", GameApi.GamePath);
Console.WriteLine($"Current game API: HTTP {gameResponse.StatusCode}");
Console.WriteLine(gameResponse.Body);
