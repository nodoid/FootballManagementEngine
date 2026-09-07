using FootballManagementEngine.Demo;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input.Touch;

namespace FootballManagementEngine.MonoGameDemo;

/// <summary>
/// The whole MonoGame demo: a club picker, a hub with the live league table and squad, and a
/// match screen that replays the engine's highlights against a running clock. Shared by the
/// Android and iOS heads, which differ only in how they start it.
/// </summary>
public sealed class DemoGame : Game
{
    private enum Screen { Loading, ClubSelect, Hub, Match }
    private enum HubTab { Table, Squad }

    private const int HeaderHeight = 40;
    private const int RowHeight = 26;

    private readonly GraphicsDeviceManager _graphics;
    private readonly string _databasePath;

    private SpriteBatch _batch = null!;
    private PixelFont _font = null!;
    private Ui _ui = null!;

    private GameSession? _session;
    private Task<GameSession>? _loading;
    private string _loadingError = "";

    private Screen _screen = Screen.Loading;
    private HubTab _tab = HubTab.Table;

    private readonly ScrollView _clubScroll = new();
    private readonly ScrollView _listScroll = new();

    private MatchResult? _match;
    private FixtureCard? _matchFixture;
    private bool _matchWasHome;
    private float _matchClock;

    public DemoGame(string databasePath)
    {
        _databasePath = databasePath;
        _graphics = new GraphicsDeviceManager(this)
        {
            IsFullScreen = true,
            SupportedOrientations = DisplayOrientation.Portrait | DisplayOrientation.PortraitDown
        };
        Content.RootDirectory = "Content";
        IsMouseVisible = false;
    }

    protected override void Initialize()
    {
        TouchPanel.EnabledGestures = GestureType.None;
        base.Initialize();
    }

    protected override void LoadContent()
    {
        _batch = new SpriteBatch(GraphicsDevice);
        _font = PixelFont.Create(GraphicsDevice);
        _ui = new Ui(GraphicsDevice, _font);

        // Building the pyramid writes 116 clubs and a full calendar to SQLite, so it happens off
        // the render thread and the demo shows a loading screen until it lands.
        _loading = Task.Run(() => GameSession.Open(_databasePath));
    }

    protected override void Update(GameTime gameTime)
    {
        _ui.Resize(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
        _ui.Update();

        var elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;

        if (_screen == Screen.Loading)
        {
            if (_loading is { IsCompletedSuccessfully: true })
            {
                _session = _loading.Result;
                _loading = null;
                _screen = _session.Club is null ? Screen.ClubSelect : Screen.Hub;
            }
            else if (_loading is { IsFaulted: true })
            {
                _loadingError = _loading.Exception?.GetBaseException().Message ?? "Unknown error";
                _loading = null;
            }
        }
        else if (_screen == Screen.Match)
        {
            // 90 minutes in roughly seven seconds, then hold on the final score.
            _matchClock = Math.Min(96f, _matchClock + elapsed * 14f);
        }

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Palette.Background);

        _batch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: _ui.Transform);

        switch (_screen)
        {
            case Screen.Loading: DrawLoading(); break;
            case Screen.ClubSelect: DrawClubSelect((float)gameTime.ElapsedGameTime.TotalSeconds); break;
            case Screen.Hub: DrawHub((float)gameTime.ElapsedGameTime.TotalSeconds); break;
            case Screen.Match: DrawMatch(); break;
        }

        _batch.End();
        base.Draw(gameTime);
    }

    // ---------- screens ----------

    private void DrawLoading()
    {
        _ui.TextCentred(_batch, "FOOTBALL MANAGER", Ui.CanvasWidth / 2, 250, Palette.Accent, 2);
        _ui.TextCentred(_batch, "ENGINE DEMO", Ui.CanvasWidth / 2, 274, Palette.Text, 2);

        if (_loadingError.Length > 0)
        {
            _ui.TextCentred(_batch, "COULD NOT START", Ui.CanvasWidth / 2, 330, Palette.Loss);
            _ui.TextCentred(_batch, _font.Fit(_loadingError.ToUpperInvariant(), 330),
                Ui.CanvasWidth / 2, 346, Palette.TextDim);
        }
        else
        {
            _ui.TextCentred(_batch, "BUILDING THE ENGLISH PYRAMID", Ui.CanvasWidth / 2, 330, Palette.TextDim);
        }
    }

    private void DrawClubSelect(float elapsed)
    {
        if (_session is null) return;

        _ui.Fill(_batch, new Rectangle(0, 0, Ui.CanvasWidth, HeaderHeight), Palette.Panel);
        _ui.Text(_batch, "CHOOSE A CLUB", 12, 14, Palette.Text, 2);

        var viewport = new Rectangle(0, HeaderHeight, Ui.CanvasWidth, Ui.CanvasHeight - HeaderHeight);
        var divisions = _session.Divisions;
        var contentHeight = divisions.Sum(d => 22 + _session.Clubs.Count(c => c.LeagueId == d.Id) * RowHeight);

        _clubScroll.Update(_ui, viewport.Height, contentHeight, elapsed);

        var y = viewport.Y - (int)_clubScroll.Offset;
        string? chosen = null;

        foreach (var division in divisions)
        {
            if (y + 22 > viewport.Y && y < viewport.Bottom)
            {
                _ui.Fill(_batch, new Rectangle(0, y, Ui.CanvasWidth, 22), Palette.PanelAlt);
                _ui.Text(_batch, division.Name.ToUpperInvariant(), 12, y + 8, Palette.Accent);
            }
            y += 22;

            foreach (var club in _session.Clubs.Where(c => c.LeagueId == division.Id))
            {
                var row = new Rectangle(0, y, Ui.CanvasWidth, RowHeight);
                if (y + RowHeight > viewport.Y && y < viewport.Bottom)
                {
                    _ui.Text(_batch, club.ShortName, 12, y + 10, Palette.TextDim);
                    _ui.Text(_batch, _font.Fit(club.Name.ToUpperInvariant(), 230), 60, y + 10, Palette.Text);
                    _ui.Fill(_batch, new Rectangle(0, row.Bottom - 1, Ui.CanvasWidth, 1), Palette.Panel);
                }

                if (_ui.Tapped(row, viewport)) chosen = club.Id;
                y += RowHeight;
            }
        }

        // Redraw the header over any row that scrolled beneath it.
        _ui.Fill(_batch, new Rectangle(0, 0, Ui.CanvasWidth, HeaderHeight), Palette.Panel);
        _ui.Text(_batch, "CHOOSE A CLUB", 12, 14, Palette.Text, 2);

        if (chosen is not null)
        {
            _session.SelectClub(chosen);
            _listScroll.Reset();
            _screen = Screen.Hub;
        }
    }

    private void DrawHub(float elapsed)
    {
        if (_session?.Club is not { } club) { _screen = Screen.ClubSelect; return; }

        // Header.
        _ui.Fill(_batch, new Rectangle(0, 0, Ui.CanvasWidth, HeaderHeight), Palette.Panel);
        _ui.Text(_batch, _font.Fit(club.Name.ToUpperInvariant(), 250, 2), 12, 8, Palette.Text, 2);
        _ui.Text(_batch, $"{_session.LeagueName(club.LeagueId).ToUpperInvariant()}  {_session.CurrentDateUtc:d MMM yyyy}".ToUpperInvariant(),
            12, 26, Palette.TextDim);

        var position = _session.TablePosition;
        if (position > 0) _ui.TextRight(_batch, $"{position}", Ui.CanvasWidth - 12, 12, Palette.Accent, 2);

        // Next fixture.
        var card = new Rectangle(8, 46, Ui.CanvasWidth - 16, 48);
        _ui.Panel(_batch, card);
        if (_session.NextFixture is { } next)
        {
            _ui.Text(_batch, $"{next.CompetitionName} RD {next.Round}".ToUpperInvariant(), card.X + 10, card.Y + 8, Palette.TextDim);
            var line = next.IsHome(club.Id)
                ? $"V {next.Opponent(club.Id)}"
                : $"AT {next.Opponent(club.Id)}";
            _ui.Text(_batch, _font.Fit(line.ToUpperInvariant(), card.Width - 20), card.X + 10, card.Y + 24, Palette.Text);
        }
        else
        {
            _ui.Text(_batch, "SEASON COMPLETE", card.X + 10, card.Y + 18, Palette.TextDim);
        }

        // Actions.
        var play = new Rectangle(8, 102, 216, 30);
        var week = new Rectangle(232, 102, Ui.CanvasWidth - 240, 30);
        var canPlay = _session.NextFixture is not null;

        if (_ui.Button(_batch, play, "PLAY MATCH", canPlay)) StartMatch();
        if (_ui.Button(_batch, week, "WEEK +1")) _session.AdvanceWeek();

        // Tabs.
        var tableTab = new Rectangle(8, 140, 100, 24);
        var squadTab = new Rectangle(112, 140, 100, 24);
        DrawTab(tableTab, "TABLE", HubTab.Table);
        DrawTab(squadTab, "SQUAD", HubTab.Squad);

        // List.
        var viewport = new Rectangle(0, 170, Ui.CanvasWidth, 428);
        if (_tab == HubTab.Table) DrawTable(viewport, club.Id, elapsed);
        else DrawSquad(viewport, elapsed);

        var change = new Rectangle(8, Ui.CanvasHeight - 38, Ui.CanvasWidth - 16, 30);
        if (_ui.Button(_batch, change, "CHANGE CLUB"))
        {
            _session.Game.State.PlayerTeamId = null;
            _session.Game.SaveIfConfigured();
            _clubScroll.Reset();
            _screen = Screen.ClubSelect;
        }
    }

    private void DrawTab(Rectangle rectangle, string label, HubTab tab)
    {
        var active = _tab == tab;
        _ui.Fill(_batch, rectangle, active ? Palette.Accent : Palette.Panel);
        _ui.TextCentred(_batch, label, rectangle.Center.X, rectangle.Y + 9, active ? Palette.Text : Palette.TextDim);
        if (_ui.Tapped(rectangle)) { _tab = tab; _listScroll.Reset(); }
    }

    private void DrawTable(Rectangle viewport, string clubId, float elapsed)
    {
        if (_session is null) return;

        _ui.Fill(_batch, new Rectangle(0, viewport.Y, Ui.CanvasWidth, 16), Palette.PanelAlt);
        _ui.Text(_batch, "CLUB", 34, viewport.Y + 5, Palette.TextDim);
        _ui.TextRight(_batch, "P", 250, viewport.Y + 5, Palette.TextDim);
        _ui.TextRight(_batch, "GD", 300, viewport.Y + 5, Palette.TextDim);
        _ui.TextRight(_batch, "PTS", 350, viewport.Y + 5, Palette.TextDim);

        var rows = _session.Table;
        var list = new Rectangle(viewport.X, viewport.Y + 16, viewport.Width, viewport.Height - 16);
        _listScroll.Update(_ui, list.Height, rows.Count * 22, elapsed);

        var y = list.Y - (int)_listScroll.Offset;
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (y + 22 > list.Y && y < list.Bottom)
            {
                var mine = row.TeamId == clubId;
                if (mine) _ui.Fill(_batch, new Rectangle(0, y, Ui.CanvasWidth, 22), Palette.AccentDim);

                var colour = mine ? Palette.Text : Palette.TextDim;
                _ui.TextRight(_batch, $"{i + 1}", 28, y + 7, colour);
                _ui.Text(_batch, _font.Fit(row.TeamName.ToUpperInvariant(), 180), 34, y + 7, mine ? Palette.Text : Palette.Text);
                _ui.TextRight(_batch, $"{row.Played}", 250, y + 7, colour);
                _ui.TextRight(_batch, row.GoalDifference > 0 ? $"+{row.GoalDifference}" : $"{row.GoalDifference}", 300, y + 7, colour);
                _ui.TextRight(_batch, $"{row.Points}", 350, y + 7, mine ? Palette.Highlight : Palette.Text);
            }
            y += 22;
        }
    }

    private void DrawSquad(Rectangle viewport, float elapsed)
    {
        if (_session is null) return;

        _ui.Fill(_batch, new Rectangle(0, viewport.Y, Ui.CanvasWidth, 16), Palette.PanelAlt);
        _ui.Text(_batch, "PLAYER", 46, viewport.Y + 5, Palette.TextDim);
        _ui.TextRight(_batch, "OVR", 268, viewport.Y + 5, Palette.TextDim);
        _ui.TextRight(_batch, "APP", 310, viewport.Y + 5, Palette.TextDim);
        _ui.TextRight(_batch, "GLS", 350, viewport.Y + 5, Palette.TextDim);

        var squad = _session.Squad;
        var list = new Rectangle(viewport.X, viewport.Y + 16, viewport.Width, viewport.Height - 16);
        _listScroll.Update(_ui, list.Height, squad.Count * 22, elapsed);

        var y = list.Y - (int)_listScroll.Offset;
        foreach (var player in squad)
        {
            if (y + 22 > list.Y && y < list.Bottom)
            {
                _ui.Text(_batch, player.Position.ToString(), 10, y + 7, PositionColour(player.Position));
                _ui.Text(_batch, _font.Fit(player.Name.ToUpperInvariant(), 200), 46, y + 7,
                    player.Injured ? Palette.Loss : Palette.Text);
                _ui.TextRight(_batch, $"{player.Overall}", 268, y + 7, Palette.TextDim);
                _ui.TextRight(_batch, $"{player.Appearances}", 310, y + 7, Palette.TextDim);
                _ui.TextRight(_batch, $"{player.Goals}", 350, y + 7,
                    player.Goals > 0 ? Palette.Highlight : Palette.TextDim);
            }
            y += 22;
        }
    }

    private void DrawMatch()
    {
        if (_session?.Club is not { } club || _match is null || _matchFixture is null)
        {
            _screen = Screen.Hub;
            return;
        }

        var minute = (int)_matchClock;
        var shown = _match.Highlights.Where(h => h.Minute <= minute).OrderBy(h => h.Minute).ToList();
        var homeGoals = _match.Highlights.Count(h => h.Type == MatchEventType.Goal && h.Minute <= minute && h.TeamId == _matchFixture.HomeTeamId);
        var awayGoals = _match.Highlights.Count(h => h.Type == MatchEventType.Goal && h.Minute <= minute && h.TeamId == _matchFixture.AwayTeamId);

        var finished = _matchClock >= 96f;
        if (finished) { homeGoals = _match.HomeGoals; awayGoals = _match.AwayGoals; }

        _ui.Fill(_batch, new Rectangle(0, 0, Ui.CanvasWidth, 120), Palette.Panel);
        _ui.TextCentred(_batch, _font.Fit(_matchFixture.CompetitionName.ToUpperInvariant(), 340), Ui.CanvasWidth / 2, 14, Palette.TextDim);

        var homeName = _matchWasHome ? club.ShortName : _matchFixture.HomeTeamId;
        var awayName = _matchWasHome ? _matchFixture.AwayTeamId : club.ShortName;
        _ui.Text(_batch, homeName, 16, 46, Palette.Text, 2);
        _ui.TextRight(_batch, awayName, Ui.CanvasWidth - 16, 46, Palette.Text, 2);
        _ui.TextCentred(_batch, $"{homeGoals}-{awayGoals}", Ui.CanvasWidth / 2, 42, Palette.Highlight, 3);

        _ui.TextCentred(_batch, finished ? "FULL TIME" : $"{Math.Min(90, minute)}'",
            Ui.CanvasWidth / 2, 88, finished ? Palette.Accent : Palette.TextDim, finished ? 1 : 2);

        // Commentary, newest at the top so the latest event is always visible.
        var y = 136;
        foreach (var highlight in shown.AsEnumerable().Reverse().Take(18))
        {
            var colour = highlight.Type switch
            {
                MatchEventType.Goal => Palette.Highlight,
                MatchEventType.YellowCard => Palette.Draw,
                MatchEventType.Save => Palette.Accent,
                _ => Palette.TextDim
            };
            _ui.TextRight(_batch, $"{highlight.Minute}'", 34, y, Palette.TextDim);
            _ui.Text(_batch, _font.Fit(highlight.Description.ToUpperInvariant(), 300), 44, y, colour);
            y += 22;
        }

        if (finished)
        {
            var verdict = _matchWasHome
                ? Verdict(_match.HomeGoals, _match.AwayGoals)
                : Verdict(_match.AwayGoals, _match.HomeGoals);

            _ui.TextCentred(_batch, verdict.Label, Ui.CanvasWidth / 2, Ui.CanvasHeight - 80, verdict.Colour, 2);

            var continueButton = new Rectangle(8, Ui.CanvasHeight - 44, Ui.CanvasWidth - 16, 32);
            if (_ui.Button(_batch, continueButton, "CONTINUE"))
            {
                _match = null;
                _matchFixture = null;
                _listScroll.Reset();
                _screen = Screen.Hub;
            }
        }
        else
        {
            var skip = new Rectangle(Ui.CanvasWidth - 96, Ui.CanvasHeight - 44, 88, 32);
            if (_ui.Button(_batch, skip, "SKIP")) _matchClock = 96f;
        }
    }

    // ---------- helpers ----------

    private void StartMatch()
    {
        if (_session?.Club is not { } club || _session.NextFixture is not { } next) return;

        _matchFixture = next;
        _matchWasHome = next.IsHome(club.Id);
        _matchClock = 0f;
        _match = _session.PlayNextMatch(new MatchSimulationOptions
        {
            DurationSeconds = 0,
            IncludeHighlights = true,
            HighlightCount = 12
        });

        if (_match is not null) _screen = Screen.Match;
    }

    private static (string Label, Color Colour) Verdict(int forGoals, int againstGoals) =>
        forGoals > againstGoals ? ("WIN", Palette.Win)
        : forGoals < againstGoals ? ("DEFEAT", Palette.Loss)
        : ("DRAW", Palette.Draw);

    private static Color PositionColour(Position position) => position switch
    {
        Position.GK => Palette.Draw,
        Position.DEF => Palette.Accent,
        Position.MID => Palette.Win,
        _ => Palette.Loss
    };

    protected override void UnloadContent()
    {
        _ui.Dispose();
        _font.Dispose();
        _batch.Dispose();
        base.UnloadContent();
    }
}
