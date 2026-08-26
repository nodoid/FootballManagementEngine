using System.Text.Json;
using FootballManagementEngine;

var game = UkDatabase.Create();
var season = new SeasonEngine(game);

Console.WriteLine($"English football database loaded: {game.State.Teams.Count} clubs.");

season.GenerateDomesticSeason();
season.GenerateFaCup();

// Qualification is normally determined from the previous season.
// This starter setup uses the top six as example European qualifiers.
game.State.Competitions["UCL"].TeamIds.AddRange(
    new[] { "ARS", "LIV", "MCI", "MUN", "CHE", "NEW" });
game.State.Competitions["UEL"].TeamIds.AddRange(
    new[] { "AVL", "TOT", "BHA", "WHU" });
game.State.Competitions["UECL"].TeamIds.AddRange(
    new[] { "CRY", "FUL" });
season.GenerateEuropeanFixtures();

Console.WriteLine($"Fixtures generated: {game.State.Fixtures.Count:N0}");

var firstFixtures = game.Fixtures("PL-COMP").Take(5).ToList();
var simulator = new MatchSimulator();

foreach (var fixture in firstFixtures)
{
    var result = simulator.Simulate(
        fixture,
        game.State.Teams[fixture.HomeTeamId],
        game.State.Teams[fixture.AwayTeamId]);

    game.ApplyResult(result);
}

var table = game.GetLeagueTable("PL");
Console.WriteLine("\nPremier League:");
var position = 1;
foreach (var row in table)
{
    Console.WriteLine(
        $"{position++,2}. {row.TeamName,-28} " +
        $"{row.Played,2} {row.Won,2} {row.Drawn,2} {row.Lost,2} " +
        $"{row.GoalsFor,2}:{row.GoalsAgainst,2} " +
        $"{row.GoalDifference,3} {row.Points,3}");
}

var json = JsonSerializer.Serialize(
    firstFixtures.Select((f, i) => new
    {
        fixtureId = f.Id,
        homeGoals = i + 1,
        awayGoals = i % 2
    }), game.JsonOptions);

Console.WriteLine("\nExample result JSON:");
Console.WriteLine(json);

// Save game.
File.WriteAllText("savegame.json", game.ExportState());

// Demonstrate reload.
var reloaded = FootballGameEngine.ImportState(File.ReadAllText("savegame.json"));
Console.WriteLine($"\nReloaded save: {reloaded.State.Teams.Count} teams, " +
                  $"{reloaded.State.Fixtures.Count} fixtures.");
