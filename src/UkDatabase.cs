namespace FootballManagementEngine;

public static class UkDatabase
{
    public static FootballGameEngine Create()
    {
        var game = new FootballGameEngine();

        AddLeague(game, "PL", "Premier League", 1, 20, 3, 3, 4);
        AddLeague(game, "CH", "Championship", 2, 24, 3, 3, 4);
        AddLeague(game, "L1", "League One", 3, 24, 4, 4, 4);
        AddLeague(game, "L2", "League Two", 4, 24, 4, 2, 4);
        AddLeague(game, "NL", "National League", 5, 24, 4, 4, 2);

        var clubs = new[]
        {
            ("ARS","Arsenal","PL"),("AVL","Aston Villa","PL"),("BOU","Bournemouth","PL"),
            ("BRE","Brentford","PL"),("BHA","Brighton & Hove Albion","PL"),("BUR","Burnley","PL"),
            ("CHE","Chelsea","PL"),("CRY","Crystal Palace","PL"),("EVE","Everton","PL"),
            ("FUL","Fulham","PL"),("LEE","Leeds United","PL"),("LIV","Liverpool","PL"),
            ("MCI","Manchester City","PL"),("MUN","Manchester United","PL"),("NEW","Newcastle United","PL"),
            ("NFO","Nottingham Forest","PL"),("SUN","Sunderland","PL"),("TOT","Tottenham Hotspur","PL"),
            ("WHU","West Ham United","PL"),("WOL","Wolverhampton Wanderers","PL"),

            ("BIR","Birmingham City","CH"),("BLK","Blackburn Rovers","CH"),("BRI","Bristol City","CH"),
            ("CAR","Cardiff City","CH"),("COV","Coventry City","CH"),("DER","Derby County","CH"),
            ("HUL","Hull City","CH"),("IPS","Ipswich Town","CH"),("LEI","Leicester City","CH"),
            ("MID","Middlesbrough","CH"),("NOR","Norwich City","CH"),("QPR","Queens Park Rangers","CH"),
            ("SHW","Sheffield Wednesday","CH"),("SWA","Swansea City","CH"),("WAT","Watford","CH"),
            ("WBA","West Bromwich Albion","CH"),("STK","Stoke City","CH"),("MIL","Millwall","CH"),
            ("PRE","Preston North End","CH"),("SHE","Sheffield United","CH"),("PLY","Plymouth Argyle","CH"),
            ("POR","Portsmouth","CH"),("QPR2","Rotherham United","CH"),("LUT","Luton Town","CH"),

            ("BAR","Barnsley","L1"),("BOL","Bolton Wanderers","L1"),("BRD","Bradford City","L1"),
            ("CHA","Charlton Athletic","L1"),("EXE","Exeter City","L1"),("HUD","Huddersfield Town","L1"),
            ("LEY","Leyton Orient","L1"),("LIN","Lincoln City","L1"),("MAN","Mansfield Town","L1"),
            ("POS","Peterborough United","L1"),("REA","Reading","L1"),("RDN","Rotherham United","L1"),
            ("ROT","Rotherham United Res","L1"),("STV","Stevenage","L1"),("WIG","Wigan Athletic","L1"),
            ("WYC","Wycombe Wanderers","L1"),("DON","Doncaster Rovers","L1"),("BUR2","Burton Albion","L1"),
            ("CAM","Cambridge United","L1"),("FLE","Fleetwood Town","L1"),("NOR2","Northampton Town","L1"),
            ("SHR","Shrewsbury Town","L1"),("STO","Stockport County","L1"),("ACC","Accrington Stanley","L1"),

            ("AFCW","AFC Wimbledon","L2"),("BAR2","Barrow","L2"),("BRO","Bromley","L2"),
            ("CAR2","Carlisle United","L2"),("CHE2","Chesterfield","L2"),("COL","Colchester United","L2"),
            ("CRE","Crewe Alexandra","L2"),("DON2","Doncaster Rovers Res","L2"),("FGR","Forest Green Rovers","L2"),
            ("GIL","Gillingham","L2"),("GRI","Grimsby Town","L2"),("HAR","Harrogate Town","L2"),
            ("MKD","Milton Keynes Dons","L2"),("MOR","Morecambe","L2"),("NEW2","Newport County","L2"),
            ("NOT","Notts County","L2"),("SAL","Salford City","L2"),("SWI","Swindon Town","L2"),
            ("TRA","Tranmere Rovers","L2"),("WAL","Walsall","L2"),("ACC2","Accrington Res","L2"),
            ("CRE2","Crewe Res","L2"),("FLE2","Fleetwood Res","L2"),("GRI2","Grimsby Res","L2"),

            ("ALD","Aldershot Town","NL"),("ALT","Altrincham","NL"),("BOS","Boston United","NL"),
            ("DAG","Dagenham & Redbridge","NL"),("DOV","Dover Athletic","NL"),("EAS","Eastleigh","NL"),
            ("EBB","Ebbsfleet United","NL"),("FYL","Fylde","NL"),("GAT","Gateshead","NL"),
            ("HAL","Halifax Town","NL"),("HAR2","Hartlepool United","NL"),("MAI","Maidenhead United","NL"),
            ("OLD","Oldham Athletic","NL"),("ROC","Rochdale","NL"),("SOL","Solihull Moors","NL"),
            ("SOU","Southend United","NL"),("TAM","Tamworth","NL"),("TOR","Torquay United","NL"),
            ("WEA","Wealdstone","NL"),("WOK","Woking","NL"),("YEO","Yeovil Town","NL"),
            ("YOR","York City","NL"),("SUT","Sutton United","NL"),("BRO2","Boreham Wood","NL")
        };

        var names = new[]
        {
            "Jack Wilson","Oliver Smith","Harry Taylor","George Brown","Charlie Davies",
            "Thomas Evans","James Thomas","William Roberts","Daniel Johnson","Archie Walker",
            "Freddie Wright","Oscar Thompson","Henry White","Alfie Hughes","Leo Edwards",
            "Theo Green","Arthur Hall","Finley Lewis","Lucas Harris","Noah Clarke"
        };

        foreach (var (id, name, league) in clubs)
        {
            var players = new List<Player>();
            for (int p = 0; p < 22; p++)
            {
                var overall = 58 + ((id.GetHashCode() & 0x7fffffff) + p * 7) % 30;
                players.Add(new Player
                {
                    Id = $"{id}-P{p + 1:00}",
                    Name = names[(p + id.Length) % names.Length],
                    Position = p switch { 0 => Position.GK, < 8 => Position.DEF, < 16 => Position.MID, _ => Position.FWD },
                    Age = 18 + ((p * 3 + id.Length) % 18),
                    Overall = overall,
                    Potential = Math.Min(95, overall + 3 + (p % 12)),
                    Pace = 50 + (p * 3) % 45,
                    Shooting = 45 + (p * 5) % 50,
                    Passing = 45 + (p * 7) % 50,
                    Defending = 45 + (p * 11) % 50,
                    Goalkeeping = p == 0 ? 60 + (p * 4) % 35 : 10,
                    WeeklyWage = 800 + overall * 90,
                    ContractClubId = id,
                    ContractYears = 1 + p % 4
                });
            }

            game.AddTeam(new Team
            {
                Id = id, Name = name, ShortName = id,
                LeagueId = game.State.Leagues[league].Id,
                Players = players,
                Reputation = league switch { "PL" => 70, "CH" => 60, "L1" => 50, "L2" => 42, _ => 35 }
            });
        }

        foreach (var league in game.State.Leagues.Values)
            league.TeamIds.AddRange(game.State.Teams.Values.Where(t => t.LeagueId == league.Id).Select(t => t.Id));

        game.AddCompetition(new Competition
        {
            Id = "PL-COMP", Name = "Premier League", Type = CompetitionType.League,
            LeagueId = "PL", TeamIds = game.State.Leagues["PL"].TeamIds
        });
        game.AddCompetition(new Competition
        {
            Id = "CH-COMP", Name = "Championship", Type = CompetitionType.League,
            LeagueId = "CH", TeamIds = game.State.Leagues["CH"].TeamIds
        });
        game.AddCompetition(new Competition
        {
            Id = "L1-COMP", Name = "League One", Type = CompetitionType.League,
            LeagueId = "L1", TeamIds = game.State.Leagues["L1"].TeamIds
        });
        game.AddCompetition(new Competition
        {
            Id = "L2-COMP", Name = "League Two", Type = CompetitionType.League,
            LeagueId = "L2", TeamIds = game.State.Leagues["L2"].TeamIds
        });
        game.AddCompetition(new Competition
        {
            Id = "NL-COMP", Name = "National League", Type = CompetitionType.League,
            LeagueId = "NL", TeamIds = game.State.Leagues["NL"].TeamIds
        });

        game.AddCompetition(new Competition
        {
            Id = "FA", Name = "FA Cup", Type = CompetitionType.FaCup,
            TeamIds = game.State.Teams.Keys.ToList()
        });
        game.AddCompetition(new Competition
        {
            Id = "CARABAO", Name = "Carabao Cup", Type = CompetitionType.LeagueCup,
            TeamIds = game.State.Leagues["PL"].TeamIds
                .Concat(game.State.Leagues["CH"].TeamIds).ToList()
        });
        game.AddCompetition(new Competition
        {
            Id = "EFLT", Name = "EFL Trophy", Type = CompetitionType.EflTrophy,
            TeamIds = game.State.Leagues["L1"].TeamIds
                .Concat(game.State.Leagues["L2"].TeamIds).ToList()
        });
        game.AddCompetition(new Competition
        {
            Id = "UCL", Name = "UEFA Champions League", Type = CompetitionType.EuropeanLeaguePhase
        });
        game.AddCompetition(new Competition
        {
            Id = "UEL", Name = "UEFA Europa League", Type = CompetitionType.EuropeanLeaguePhase
        });
        game.AddCompetition(new Competition
        {
            Id = "UECL", Name = "UEFA Conference League", Type = CompetitionType.EuropeanLeaguePhase
        });

        return game;
    }

    private static void AddLeague(
        FootballGameEngine game, string id, string name, int level,
        int ignoredTeamCount, int promotion, int relegation, int playoffs)
    {
        game.AddLeague(new League
        {
            Id = id, Name = name, Level = level,
            PromotionSpots = promotion, RelegationSpots = relegation,
            PlayoffSpots = playoffs
        });
    }
}
