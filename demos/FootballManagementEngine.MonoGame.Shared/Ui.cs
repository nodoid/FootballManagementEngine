using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input.Touch;

namespace FootballManagementEngine.MonoGameDemo;

/// <summary>The demo's palette, kept in one place so both screens and widgets agree.</summary>
public static class Palette
{
    public static readonly Color Background = new(11, 18, 32);
    public static readonly Color Panel = new(20, 32, 54);
    public static readonly Color PanelAlt = new(27, 42, 68);
    public static readonly Color Accent = new(58, 140, 232);
    public static readonly Color AccentDim = new(31, 74, 122);
    public static readonly Color Text = new(233, 240, 250);
    public static readonly Color TextDim = new(139, 163, 194);
    public static readonly Color Win = new(76, 175, 96);
    public static readonly Color Loss = new(214, 87, 76);
    public static readonly Color Draw = new(214, 168, 63);
    public static readonly Color Highlight = new(255, 214, 92);
}

/// <summary>
/// A tiny immediate-mode layer: one touch source, one white pixel, and enough helpers to lay out
/// panels, buttons and scrolling lists. Everything is expressed in a fixed virtual canvas so the
/// layout is identical on every phone and tablet; <see cref="ToVirtual"/> maps taps back.
/// </summary>
public sealed class Ui : IDisposable
{
    public const int CanvasWidth = 360;
    public const int CanvasHeight = 640;

    private readonly Texture2D _pixel;
    private Vector2 _pressOrigin;
    private bool _dragging;

    public Ui(GraphicsDevice device, PixelFont font)
    {
        Font = font;
        _pixel = new Texture2D(device, 1, 1);
        _pixel.SetData([Color.White]);
    }

    public PixelFont Font { get; }

    /// <summary>Where a finger went down and came back up this frame, in canvas coordinates.</summary>
    public Vector2? Tap { get; private set; }

    /// <summary>Vertical finger movement this frame, for flick scrolling.</summary>
    public float DragY { get; private set; }

    public bool IsTouching { get; private set; }

    public Matrix Transform { get; private set; } = Matrix.Identity;

    private float _scale = 1f;
    private Vector2 _offset;

    /// <summary>Fits the virtual canvas inside the real back buffer, letterboxing to keep it square.</summary>
    public void Resize(int backBufferWidth, int backBufferHeight)
    {
        _scale = Math.Min(backBufferWidth / (float)CanvasWidth, backBufferHeight / (float)CanvasHeight);
        _offset = new Vector2(
            (backBufferWidth - CanvasWidth * _scale) / 2f,
            (backBufferHeight - CanvasHeight * _scale) / 2f);
        Transform = Matrix.CreateScale(_scale) * Matrix.CreateTranslation(_offset.X, _offset.Y, 0);
    }

    public Vector2 ToVirtual(Vector2 screen) => (screen - _offset) / _scale;

    /// <summary>Collects this frame's touch into a tap and a drag.</summary>
    public void Update()
    {
        Tap = null;
        DragY = 0f;

        var touches = TouchPanel.GetState();
        var touch = touches.Count > 0 ? touches[0] : default;

        switch (touch.State)
        {
            case TouchLocationState.Pressed:
                IsTouching = true;
                _dragging = false;
                _pressOrigin = ToVirtual(touch.Position);
                break;

            case TouchLocationState.Moved:
                IsTouching = true;
                if (touch.TryGetPreviousLocation(out var previous))
                {
                    var delta = ToVirtual(touch.Position) - ToVirtual(previous.Position);
                    DragY = delta.Y;
                    // A few pixels of travel turns a press into a scroll, so lists do not
                    // fire the row underneath the finger when the user was only scrolling.
                    if (Math.Abs(ToVirtual(touch.Position).Y - _pressOrigin.Y) > 6f) _dragging = true;
                }
                break;

            case TouchLocationState.Released:
                IsTouching = false;
                if (!_dragging) Tap = ToVirtual(touch.Position);
                _dragging = false;
                break;
        }
    }

    // ---------- drawing ----------

    public void Fill(SpriteBatch batch, Rectangle rectangle, Color colour) =>
        batch.Draw(_pixel, rectangle, colour);

    public void Outline(SpriteBatch batch, Rectangle r, Color colour, int thickness = 1)
    {
        Fill(batch, new Rectangle(r.X, r.Y, r.Width, thickness), colour);
        Fill(batch, new Rectangle(r.X, r.Bottom - thickness, r.Width, thickness), colour);
        Fill(batch, new Rectangle(r.X, r.Y, thickness, r.Height), colour);
        Fill(batch, new Rectangle(r.Right - thickness, r.Y, thickness, r.Height), colour);
    }

    public void Panel(SpriteBatch batch, Rectangle rectangle, Color? colour = null) =>
        Fill(batch, rectangle, colour ?? Palette.Panel);

    public void Text(SpriteBatch batch, string text, int x, int y, Color colour, int scale = 1) =>
        Font.Draw(batch, text, new Vector2(x, y), colour, scale);

    public void TextRight(SpriteBatch batch, string text, int right, int y, Color colour, int scale = 1) =>
        Font.DrawRight(batch, text, right, y, colour, scale);

    public void TextCentred(SpriteBatch batch, string text, int centreX, int y, Color colour, int scale = 1) =>
        Font.DrawCentred(batch, text, centreX, y, colour, scale);

    /// <summary>Draws a button and reports whether it was tapped this frame.</summary>
    public bool Button(SpriteBatch batch, Rectangle rectangle, string label, bool enabled = true)
    {
        var background = enabled ? Palette.Accent : Palette.AccentDim;
        Fill(batch, rectangle, background);
        Fill(batch, new Rectangle(rectangle.X, rectangle.Bottom - 2, rectangle.Width, 2), Palette.AccentDim);
        TextCentred(batch, label, rectangle.Center.X, rectangle.Center.Y - Font.LineHeight() / 2, Palette.Text);

        return enabled && Tap is { } tap && rectangle.Contains((int)tap.X, (int)tap.Y);
    }

    public bool Tapped(Rectangle rectangle) =>
        Tap is { } tap && rectangle.Contains((int)tap.X, (int)tap.Y);

    /// <summary>
    /// A hit test for rows inside a scrolling list: the tap must land in the row *and* inside the
    /// list's viewport, so a row that has scrolled under a header cannot be tapped through it.
    /// </summary>
    public bool Tapped(Rectangle rectangle, Rectangle clip) =>
        Tap is { } tap
        && clip.Contains((int)tap.X, (int)tap.Y)
        && rectangle.Contains((int)tap.X, (int)tap.Y);

    public void Dispose() => _pixel.Dispose();
}

/// <summary>Keeps a scroll offset inside its bounds and applies drag and flick momentum.</summary>
public sealed class ScrollView
{
    private float _velocity;

    public float Offset { get; private set; }

    public void Update(Ui ui, float viewportHeight, float contentHeight, float elapsedSeconds)
    {
        var maximum = Math.Max(0f, contentHeight - viewportHeight);

        if (ui.IsTouching)
        {
            Offset -= ui.DragY;
            _velocity = -ui.DragY / Math.Max(elapsedSeconds, 0.0001f);
        }
        else if (Math.Abs(_velocity) > 1f)
        {
            Offset += _velocity * elapsedSeconds;
            _velocity *= 0.9f;
        }
        else
        {
            _velocity = 0f;
        }

        if (Offset < 0f) { Offset = 0f; _velocity = 0f; }
        if (Offset > maximum) { Offset = maximum; _velocity = 0f; }
    }

    public void Reset()
    {
        Offset = 0f;
        _velocity = 0f;
    }
}
