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
    private enum HubTab { Table, Squad, Market }

    private const int HeaderHeight = 62;
    private const int PickerHeaderHeight = 40;
    private const float MinimumSplashSeconds = 1.6f;
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

    // The market is rebuilt from every squad in the game, so it is cached rather than
    // recomputed on each of the sixty frames a second.
    private List<TransferListing> _market = [];
    private bool _marketStale = true;
    private string _marketMessage = "";
    private float _marketMessageSeconds;

    private float _splashSeconds;

    private readonly List<MatchInjury> _pendingInjuries = [];
    private MatchInjury? _awaitingSubstitution;

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
            _splashSeconds += elapsed;

            if (_loading is { IsCompletedSuccessfully: true })
            {
                _session = _loading.Result;
                _loading = null;
            }
            else if (_loading is { IsFaulted: true })
            {
                _loadingError = _loading.Exception?.GetBaseException().Message ?? "Unknown error";
                _loading = null;
            }

            // Hold the title card briefly even on a resumed save, which loads almost instantly.
            if (_session is not null && _splashSeconds >= MinimumSplashSeconds)
                _screen = _session.Club is null ? Screen.ClubSelect : Screen.Hub;
        }
        else if (_screen == Screen.Match && _awaitingSubstitution is null)
        {
            // 90 minutes in roughly seven seconds, then hold on the final score.
            _matchClock = Math.Min(96f, _matchClock + elapsed * 14f);

            // The clock stops the moment someone limps off, and only restarts once the
            // manager has named a replacement.
            if (_pendingInjuries.Count > 0 && _pendingInjuries[0].Minute <= (int)_matchClock)
            {
                _awaitingSubstitution = _pendingInjuries[0];
                _pendingInjuries.RemoveAt(0);
            }
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

        // The tap this frame handled is only discarded now that a frame has drawn.
        _ui.EndFrame();
        base.Draw(gameTime);
    }

    // ---------- screens ----------

    /// <summary>
    /// The title card. Deliberately a plain blocky wordmark drawn from the demo's own pixel font,
    /// in the spirit of an early-80s home-computer loading screen, rather than a copy of any
    /// existing cover art.
    /// </summary>
    private void DrawLoading()
    {
        const int centre = Ui.CanvasWidth / 2;

        _ui.TextCentred(_batch, "FOOTBALL", centre, 150, Palette.Text, 6);
        _ui.TextCentred(_batch, "MANAGER", centre, 212, Palette.Text, 6);

        // Underline sized to the wider of the two words, echoing the splash artwork.
        var rule = _font.Measure("MANAGER", 6);
        _ui.Fill(_batch, new Rectangle(centre - rule / 2, 274, rule, 3), Palette.Accent);
        _ui.TextCentred(_batch, "ENGINE DEMO", centre, 292, Palette.Accent, 2);

        if (_loadingError.Length > 0)
        {
            _ui.TextCentred(_batch, "COULD NOT START", centre, 400, Palette.Loss);
            _ui.TextCentred(_batch, _font.Fit(_loadingError.ToUpperInvariant(), 330), centre, 416, Palette.TextDim);
        }
        else
        {
            var dots = new string('.', 1 + (int)(_splashSeconds * 2) % 3);
            _ui.TextCentred(_batch, $"BUILDING THE ENGLISH PYRAMID{dots}", centre, 400, Palette.TextDim);
        }
    }

    private void DrawClubSelect(float elapsed)
    {
        if (_session is null) return;

        _ui.Fill(_batch, new Rectangle(0, 0, Ui.CanvasWidth, PickerHeaderHeight), Palette.Panel);
        _ui.Text(_batch, "CHOOSE A CLUB", 12, 14, Palette.Text, 2);

        var viewport = new Rectangle(0, PickerHeaderHeight, Ui.CanvasWidth, Ui.CanvasHeight - PickerHeaderHeight);
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
        _ui.Fill(_batch, new Rectangle(0, 0, Ui.CanvasWidth, PickerHeaderHeight), Palette.Panel);
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

        // Header - the same facts, in the same words, as the MAUI demo's club card.
        _ui.Fill(_batch, new Rectangle(0, 0, Ui.CanvasWidth, HeaderHeight), Palette.Panel);
        _ui.Text(_batch, _font.Fit(club.Name.ToUpperInvariant(), 336, 2), 12, 8, Palette.Text, 2);
        _ui.Text(_batch, _session.LeagueName(club.LeagueId).ToUpperInvariant(), 12, 26, Palette.TextDim);

        var table = _session.Table;
        var position = _session.TablePosition;
        var points = table.FirstOrDefault(r => r.TeamId == club.Id)?.Points ?? 0;
        _ui.Text(_batch, position > 0
                ? $"{Ordinal(position)} OF {table.Count} - {points} PTS"
                : "SEASON NOT STARTED",
            12, 38, Palette.Accent);
        _ui.Text(_batch, $"SEASON {_session.Season} - {_session.CurrentDateUtc:ddd d MMM yyyy}".ToUpperInvariant(),
            12, 50, Palette.TextDim);

        // Next fixture.
        var card = new Rectangle(8, 66, Ui.CanvasWidth - 16, 48);
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
        var play = new Rectangle(8, 122, 216, 30);
        var week = new Rectangle(232, 122, Ui.CanvasWidth - 240, 30);
        var canPlay = _session.NextFixture is not null;

        if (_ui.Button(_batch, play, "PLAY MATCH", canPlay)) StartMatch();
        if (_ui.Button(_batch, week, "WEEK +1"))
        {
            var transfers = _session.AdvanceWeek();
            _marketStale = true;

            // Tell the manager when one of their listed players has been sold.
            var mine = _session.Involving(transfers);
            if (mine.Count > 0)
            {
                var deal = mine[0];
                _marketMessage = deal.FromClubId == club.Id
                    ? $"{deal.ToClubName} SIGN {deal.PlayerName} FOR {Money(deal.Fee)}"
                    : $"SIGNED {deal.PlayerName} FROM {deal.FromClubName}";
                _marketMessageSeconds = 6f;
                _tab = HubTab.Market;
                _listScroll.Reset();
            }
        }

        // Formation, the same control the MAUI club page offers.
        var formationRow = new Rectangle(8, 160, Ui.CanvasWidth - 16, 26);
        _ui.Panel(_batch, formationRow, Palette.PanelAlt);
        var previous = new Rectangle(formationRow.X + 3, formationRow.Y + 3, 28, 20);
        var following = new Rectangle(formationRow.Right - 31, formationRow.Y + 3, 28, 20);
        if (_ui.Button(_batch, previous, "<")) CycleFormation(-1);
        if (_ui.Button(_batch, following, ">")) CycleFormation(1);
        _ui.TextCentred(_batch, $"FORMATION {FormationLabel(club.Formation)}",
            formationRow.Center.X, formationRow.Y + 10, Palette.Text);

        // Tabs.
        DrawTab(new Rectangle(8, 194, 108, 24), "TABLE", HubTab.Table);
        DrawTab(new Rectangle(120, 194, 108, 24), "SQUAD", HubTab.Squad);
        DrawTab(new Rectangle(232, 194, 120, 24), "MARKET", HubTab.Market);

        // List.
        var viewport = new Rectangle(0, 224, Ui.CanvasWidth, Ui.CanvasHeight - 224);
        switch (_tab)
        {
            case HubTab.Table: DrawTable(viewport, club.Id, elapsed); break;
            case HubTab.Squad: DrawSquad(viewport, elapsed); break;
            case HubTab.Market: DrawMarket(viewport, elapsed); break;
        }

        // No way back to the picker: once a club is chosen, the manager stays at that club.
    }

    private void DrawTab(Rectangle rectangle, string label, HubTab tab)
    {
        var active = _tab == tab;
        _ui.Fill(_batch, rectangle, active ? Palette.Accent : Palette.Panel);
        _ui.TextCentred(_batch, label, rectangle.Center.X, rectangle.Y + 9, active ? Palette.Text : Palette.TextDim);
        if (_ui.Tapped(rectangle)) { _tab = tab; _listScroll.Reset(); if (tab == HubTab.Market) _marketStale = true; }
    }

    private void DrawTable(Rectangle viewport, string clubId, float elapsed)
    {
        if (_session is null) return;

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

        // Drawn last so a scrolled row cannot paint over it.
        _ui.Fill(_batch, new Rectangle(0, viewport.Y, Ui.CanvasWidth, 16), Palette.PanelAlt);
        _ui.Text(_batch, "CLUB", 34, viewport.Y + 5, Palette.TextDim);
        _ui.TextRight(_batch, "P", 250, viewport.Y + 5, Palette.TextDim);
        _ui.TextRight(_batch, "GD", 300, viewport.Y + 5, Palette.TextDim);
        _ui.TextRight(_batch, "PTS", 350, viewport.Y + 5, Palette.TextDim);
    }

    /// <summary>
    /// The transfer market: who is available, what their club wants, and whether the budget
    /// covers it. Tapping a row bids the asking price at the wage the player expects.
    /// </summary>
    private void DrawMarket(Rectangle viewport, float elapsed)
    {
        if (_session?.Club is not { } club) return;

        if (_marketStale)
        {
            _market = _session.Market().Take(400).ToList();
            _marketStale = false;
        }

        var open = _session.IsTransferWindowOpen;

        _ui.Fill(_batch, new Rectangle(0, viewport.Y, Ui.CanvasWidth, 28), Palette.PanelAlt);
        _ui.Text(_batch, _session.TransferWindowLabel.ToUpperInvariant(), 8, viewport.Y + 4,
            open ? Palette.Win : Palette.Loss);
        _ui.TextRight(_batch, $"BUDGET {Money(_session.TransferBudget)}", Ui.CanvasWidth - 8, viewport.Y + 4, Palette.Text);

        if (_marketMessageSeconds > 0)
        {
            _marketMessageSeconds -= elapsed;
            _ui.Text(_batch, _font.Fit(_marketMessage.ToUpperInvariant(), 344), 8, viewport.Y + 16, Palette.Highlight);
        }
        else
        {
            _ui.Text(_batch, "TAP A PLAYER TO BID THE ASKING PRICE", 8, viewport.Y + 16, Palette.TextDim);
        }

        var list = new Rectangle(viewport.X, viewport.Y + 30, viewport.Width, viewport.Height - 30);
        _listScroll.Update(_ui, list.Height, _market.Count * 22, elapsed);

        var y = list.Y - (int)_listScroll.Offset;
        foreach (var listing in _market)
        {
            var row = new Rectangle(0, y, Ui.CanvasWidth, 22);
            if (y + 22 > list.Y && y < list.Bottom)
            {
                var affordable = listing.AskingPrice <= _session.TransferBudget;
                if (listing.ListedByClub) _ui.Fill(_batch, row, Palette.Panel);

                _ui.Text(_batch, listing.Position.ToString(), 8, y + 7, PositionColour(listing.Position));
                _ui.Text(_batch, _font.Fit(listing.PlayerName.ToUpperInvariant(), 118), 40, y + 7, Palette.Text);
                _ui.Text(_batch, _font.Fit(listing.ClubName.ToUpperInvariant(), 70), 166, y + 7, Palette.TextDim);
                _ui.TextRight(_batch, $"{listing.Overall}", 262, y + 7, Palette.TextDim);
                _ui.TextRight(_batch, Money(listing.AskingPrice), 352, y + 7,
                    affordable && open ? Palette.Win : Palette.TextDim);
            }

            if (_ui.Tapped(row, list))
            {
                var response = _session.SignPlayer(listing.PlayerId);
                _marketMessage = response.Message;
                _marketMessageSeconds = 4f;
                if (response.Accepted) _marketStale = true;
                return;
            }

            y += 22;
        }

        if (_market.Count == 0)
            _ui.TextCentred(_batch, "NOBODY AVAILABLE", Ui.CanvasWidth / 2, list.Y + 20, Palette.TextDim);
    }

    /// <summary>Fees read better as 12.5M than as a row of digits on a phone.</summary>
    private static string Money(decimal amount) =>
        amount >= 1_000_000m ? $"\u00A3{amount / 1_000_000m:0.#}M"
        : amount >= 1_000m ? $"\u00A3{amount / 1_000m:0}K"
        : $"\u00A3{amount:0}";

    private void DrawSquad(Rectangle viewport, float elapsed)
    {
        if (_session is null) return;

        const int stateColumn = 196;
        const int listColumn = 240;

        var squad = _session.Squad;

        _ui.Fill(_batch, new Rectangle(0, viewport.Y, Ui.CanvasWidth, 14), Palette.Background);
        _ui.Text(_batch, "TAP A PLAYER TO PUT THEM ON THE TRANSFER LIST", 8, viewport.Y + 4, Palette.TextDim);

        var header = viewport.Y + 14;
        var list = new Rectangle(viewport.X, header + 16, viewport.Width, viewport.Height - 30);
        _listScroll.Update(_ui, list.Height, squad.Count * 22, elapsed);

        var y = list.Y - (int)_listScroll.Offset;
        foreach (var player in squad)
        {
            var row = new Rectangle(0, y, Ui.CanvasWidth, 22);
            if (y + 22 > list.Y && y < list.Bottom)
            {
                if (player.ListedForTransfer) _ui.Fill(_batch, row, Palette.Panel);

                _ui.Text(_batch, player.Position.ToString(), 8, y + 7, PositionColour(player.Position));
                _ui.Text(_batch, _font.Fit(player.Name.ToUpperInvariant(), 140), 40, y + 7,
                    player.Injured ? Palette.Loss : Palette.Text);
                _ui.TextCentred(_batch, player.Badge, stateColumn, y + 7, StateColour(player.State));
                if (player.ListedForTransfer)
                    _ui.TextCentred(_batch, Money(player.Value), listColumn, y + 7, Palette.Highlight);
                _ui.TextRight(_batch, $"{player.Overall}", 292, y + 7, Palette.TextDim);
                _ui.TextRight(_batch, $"{player.Appearances}", 324, y + 7, Palette.TextDim);
                _ui.TextRight(_batch, $"{player.Goals}", 352, y + 7,
                    player.Goals > 0 ? Palette.Highlight : Palette.TextDim);
            }

            if (_ui.Tapped(row, list))
            {
                _session.ToggleTransferListed(player.PlayerId);
                _marketStale = true;
                return;
            }

            y += 22;
        }

        // Drawn last so a scrolled row cannot paint over it.
        _ui.Fill(_batch, new Rectangle(0, header, Ui.CanvasWidth, 16), Palette.PanelAlt);
        _ui.Text(_batch, "PLAYER", 40, header + 5, Palette.TextDim);
        _ui.TextCentred(_batch, "STATE", stateColumn, header + 5, Palette.TextDim);
        _ui.TextCentred(_batch, "LISTED", listColumn, header + 5, Palette.TextDim);
        _ui.TextRight(_batch, "OVR", 292, header + 5, Palette.TextDim);
        _ui.TextRight(_batch, "APP", 324, header + 5, Palette.TextDim);
        _ui.TextRight(_batch, "GLS", 352, header + 5, Palette.TextDim);
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

        if (_awaitingSubstitution is { } awaiting)
        {
            DrawSubstitutionOverlay(awaiting);
            return;
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

    /// <summary>
    /// Drawn over the paused match when a player limps off: the clock is stopped on the minute,
    /// the bench is offered, and play resumes to full time once a choice is made.
    /// </summary>
    private void DrawSubstitutionOverlay(MatchInjury injury)
    {
        if (_session is null) return;

        var panel = new Rectangle(0, 130, Ui.CanvasWidth, Ui.CanvasHeight - 130);
        _ui.Fill(_batch, panel, Palette.Background);
        _ui.Fill(_batch, new Rectangle(0, 130, Ui.CanvasWidth, 2), Palette.Loss);

        _ui.TextCentred(_batch, $"{injury.Minute}' INJURY", Ui.CanvasWidth / 2, 146, Palette.Loss, 2);
        _ui.TextCentred(_batch,
            $"{injury.Player.Position} {_font.Fit(injury.Player.Name.ToUpperInvariant(), 240)}",
            Ui.CanvasWidth / 2, 172, Palette.Text);
        _ui.TextCentred(_batch,
            $"OUT FOR {injury.Player.InjuryWeeks} WEEK{(injury.Player.InjuryWeeks == 1 ? "" : "S")}",
            Ui.CanvasWidth / 2, 186, Palette.TextDim);
        _ui.TextCentred(_batch, "BRING ON", Ui.CanvasWidth / 2, 210, Palette.Accent);

        var bench = _session.Bench;
        var y = 232;

        foreach (var player in bench)
        {
            var row = new Rectangle(8, y, Ui.CanvasWidth - 16, 34);
            _ui.Panel(_batch, row, Palette.PanelAlt);
            _ui.Text(_batch, player.Position.ToString(), row.X + 10, y + 13, PositionColour(player.Position));
            _ui.Text(_batch, _font.Fit(player.Name.ToUpperInvariant(), 190), row.X + 48, y + 13, Palette.Text);
            _ui.TextRight(_batch, $"{player.Overall}", row.Right - 10, y + 13, Palette.TextDim);

            if (_ui.Tapped(row))
            {
                _session.MakeSubstitution(injury.Player.PlayerId, player.PlayerId);
                _awaitingSubstitution = null;
                return;
            }

            y += 40;
        }

        if (bench.Count == 0)
            _ui.TextCentred(_batch, "NO SUBSTITUTES AVAILABLE", Ui.CanvasWidth / 2, 240, Palette.TextDim);

        var carryOn = new Rectangle(8, Ui.CanvasHeight - 44, Ui.CanvasWidth - 16, 32);
        if (_ui.Button(_batch, carryOn, "PLAY ON")) _awaitingSubstitution = null;
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

        _pendingInjuries.Clear();
        _pendingInjuries.AddRange(_session.InjuriesIn(_match));
        _awaitingSubstitution = null;

        _marketStale = true;
        if (_match is not null) _screen = Screen.Match;
    }

    private void CycleFormation(int direction)
    {
        if (_session?.Club is not { } club) return;

        var all = Enum.GetValues<Formation>();
        var index = (Array.IndexOf(all, club.Formation) + direction + all.Length) % all.Length;
        _session.SetFormation(all[index]);
    }

    /// <summary>Turns the enum name (F4231) into something readable (4-2-3-1), as MAUI does.</summary>
    private static string FormationLabel(Formation formation) =>
        string.Join('-', formation.ToString().TrimStart('F').ToCharArray());

    /// <summary>Matches the ordinal wording on the MAUI club card ("12th of 20").</summary>
    private static string Ordinal(int value) => value switch
    {
        11 or 12 or 13 => $"{value}TH",
        _ when value % 10 == 1 => $"{value}ST",
        _ when value % 10 == 2 => $"{value}ND",
        _ when value % 10 == 3 => $"{value}RD",
        _ => $"{value}TH"
    };

    private static (string Label, Color Colour) Verdict(int forGoals, int againstGoals) =>
        forGoals > againstGoals ? ("WIN", Palette.Win)
        : forGoals < againstGoals ? ("DEFEAT", Palette.Loss)
        : ("DRAW", Palette.Draw);

    private static Color StateColour(PlayerState state) => state switch
    {
        PlayerState.Selected => Palette.Win,
        PlayerState.Substitute => Palette.Accent,
        PlayerState.Injured => Palette.Loss,
        PlayerState.Suspended => Palette.Draw,
        _ => Palette.TextDim
    };

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
