using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FootballManagementEngine.MonoGameDemo;

/// <summary>
/// A 5x7 bitmap font built straight from source, so the demo needs no content pipeline, no MGCB
/// tool and no font file to ship. Glyphs are written as ASCII art below, which keeps them
/// readable and reviewable; the atlas is baked into one texture on load.
/// </summary>
public sealed class PixelFont : IDisposable
{
    public const int GlyphWidth = 5;
    public const int GlyphHeight = 7;
    private const int Advance = GlyphWidth + 1;

    private readonly Texture2D _atlas;
    private readonly Dictionary<char, int> _index = [];

    private PixelFont(Texture2D atlas, Dictionary<char, int> index)
    {
        _atlas = atlas;
        _index = index;
    }

    public static PixelFont Create(GraphicsDevice device)
    {
        var glyphs = Glyphs;
        var columns = glyphs.Count;
        var pixels = new Color[columns * GlyphWidth * GlyphHeight];
        var index = new Dictionary<char, int>(columns);

        var slot = 0;
        foreach (var (character, art) in glyphs)
        {
            index[character] = slot;
            var rows = art.Split('\n');

            for (var y = 0; y < GlyphHeight; y++)
            {
                var row = y < rows.Length ? rows[y] : "";
                for (var x = 0; x < GlyphWidth; x++)
                {
                    var lit = x < row.Length && row[x] == '#';
                    pixels[y * columns * GlyphWidth + slot * GlyphWidth + x] =
                        lit ? Color.White : Color.Transparent;
                }
            }
            slot++;
        }

        var atlas = new Texture2D(device, columns * GlyphWidth, GlyphHeight);
        atlas.SetData(pixels);
        return new PixelFont(atlas, index);
    }

    public int Measure(string text, int scale = 1) =>
        text.Length == 0 ? 0 : (text.Length * Advance - 1) * scale;

    public int LineHeight(int scale = 1) => GlyphHeight * scale;

    public void Draw(SpriteBatch batch, string text, Vector2 position, Color colour, int scale = 1)
    {
        var x = position.X;
        foreach (var raw in text)
        {
            var character = char.ToUpperInvariant(raw);
            if (!_index.TryGetValue(character, out var slot)) _index.TryGetValue('?', out slot);

            if (character != ' ')
            {
                batch.Draw(
                    _atlas,
                    new Rectangle((int)x, (int)position.Y, GlyphWidth * scale, GlyphHeight * scale),
                    new Rectangle(slot * GlyphWidth, 0, GlyphWidth, GlyphHeight),
                    colour);
            }
            x += Advance * scale;
        }
    }

    /// <summary>Draws text ending at <paramref name="right"/> rather than starting at it.</summary>
    public void DrawRight(SpriteBatch batch, string text, float right, float y, Color colour, int scale = 1) =>
        Draw(batch, text, new Vector2(right - Measure(text, scale), y), colour, scale);

    public void DrawCentred(SpriteBatch batch, string text, float centreX, float y, Color colour, int scale = 1) =>
        Draw(batch, text, new Vector2(centreX - Measure(text, scale) / 2f, y), colour, scale);

    /// <summary>Cuts a string down so it fits inside <paramref name="maxWidth"/> pixels.</summary>
    public string Fit(string text, int maxWidth, int scale = 1)
    {
        if (Measure(text, scale) <= maxWidth) return text;

        var characters = Math.Max(1, maxWidth / (Advance * scale) - 1);
        return characters >= text.Length ? text : string.Concat(text.AsSpan(0, characters), ".");
    }

    public void Dispose() => _atlas.Dispose();

    private static Dictionary<char, string> Glyphs => new()
    {
        [' '] = """
                .....
                .....
                .....
                .....
                .....
                .....
                .....
                """,
        ['A'] = """
                .###.
                #...#
                #...#
                #####
                #...#
                #...#
                #...#
                """,
        ['B'] = """
                ####.
                #...#
                #...#
                ####.
                #...#
                #...#
                ####.
                """,
        ['C'] = """
                .###.
                #...#
                #....
                #....
                #....
                #...#
                .###.
                """,
        ['D'] = """
                ####.
                #...#
                #...#
                #...#
                #...#
                #...#
                ####.
                """,
        ['E'] = """
                #####
                #....
                #....
                ####.
                #....
                #....
                #####
                """,
        ['F'] = """
                #####
                #....
                #....
                ####.
                #....
                #....
                #....
                """,
        ['G'] = """
                .###.
                #...#
                #....
                #.###
                #...#
                #...#
                .###.
                """,
        ['H'] = """
                #...#
                #...#
                #...#
                #####
                #...#
                #...#
                #...#
                """,
        ['I'] = """
                #####
                ..#..
                ..#..
                ..#..
                ..#..
                ..#..
                #####
                """,
        ['J'] = """
                ..###
                ...#.
                ...#.
                ...#.
                ...#.
                #..#.
                .##..
                """,
        ['K'] = """
                #...#
                #..#.
                #.#..
                ##...
                #.#..
                #..#.
                #...#
                """,
        ['L'] = """
                #....
                #....
                #....
                #....
                #....
                #....
                #####
                """,
        ['M'] = """
                #...#
                ##.##
                #.#.#
                #...#
                #...#
                #...#
                #...#
                """,
        ['N'] = """
                #...#
                ##..#
                #.#.#
                #..##
                #...#
                #...#
                #...#
                """,
        ['O'] = """
                .###.
                #...#
                #...#
                #...#
                #...#
                #...#
                .###.
                """,
        ['P'] = """
                ####.
                #...#
                #...#
                ####.
                #....
                #....
                #....
                """,
        ['Q'] = """
                .###.
                #...#
                #...#
                #...#
                #.#.#
                #..#.
                .##.#
                """,
        ['R'] = """
                ####.
                #...#
                #...#
                ####.
                #.#..
                #..#.
                #...#
                """,
        ['S'] = """
                .####
                #....
                #....
                .###.
                ....#
                ....#
                ####.
                """,
        ['T'] = """
                #####
                ..#..
                ..#..
                ..#..
                ..#..
                ..#..
                ..#..
                """,
        ['U'] = """
                #...#
                #...#
                #...#
                #...#
                #...#
                #...#
                .###.
                """,
        ['V'] = """
                #...#
                #...#
                #...#
                #...#
                #...#
                .#.#.
                ..#..
                """,
        ['W'] = """
                #...#
                #...#
                #...#
                #...#
                #.#.#
                ##.##
                #...#
                """,
        ['X'] = """
                #...#
                #...#
                .#.#.
                ..#..
                .#.#.
                #...#
                #...#
                """,
        ['Y'] = """
                #...#
                #...#
                .#.#.
                ..#..
                ..#..
                ..#..
                ..#..
                """,
        ['Z'] = """
                #####
                ....#
                ...#.
                ..#..
                .#...
                #....
                #####
                """,
        ['0'] = """
                .###.
                #...#
                #..##
                #.#.#
                ##..#
                #...#
                .###.
                """,
        ['1'] = """
                ..#..
                .##..
                ..#..
                ..#..
                ..#..
                ..#..
                .###.
                """,
        ['2'] = """
                .###.
                #...#
                ....#
                ...#.
                ..#..
                .#...
                #####
                """,
        ['3'] = """
                #####
                ...#.
                ..#..
                ...#.
                ....#
                #...#
                .###.
                """,
        ['4'] = """
                ...#.
                ..##.
                .#.#.
                #..#.
                #####
                ...#.
                ...#.
                """,
        ['5'] = """
                #####
                #....
                ####.
                ....#
                ....#
                #...#
                .###.
                """,
        ['6'] = """
                ..##.
                .#...
                #....
                ####.
                #...#
                #...#
                .###.
                """,
        ['7'] = """
                #####
                ....#
                ...#.
                ..#..
                .#...
                .#...
                .#...
                """,
        ['8'] = """
                .###.
                #...#
                #...#
                .###.
                #...#
                #...#
                .###.
                """,
        ['9'] = """
                .###.
                #...#
                #...#
                .####
                ....#
                ...#.
                .##..
                """,
        ['.'] = """
                .....
                .....
                .....
                .....
                .....
                .##..
                .##..
                """,
        [','] = """
                .....
                .....
                .....
                .....
                .##..
                .##..
                .#...
                """,
        [':'] = """
                .....
                .##..
                .##..
                .....
                .##..
                .##..
                .....
                """,
        ['-'] = """
                .....
                .....
                .....
                #####
                .....
                .....
                .....
                """,
        ['\''] = """
                ..#..
                ..#..
                .....
                .....
                .....
                .....
                .....
                """,
        ['&'] = """
                .##..
                #..#.
                #..#.
                .##..
                #.#.#
                #..#.
                .##.#
                """,
        ['('] = """
                ..#..
                .#...
                #....
                #....
                #....
                .#...
                ..#..
                """,
        [')'] = """
                ..#..
                ...#.
                ....#
                ....#
                ....#
                ...#.
                ..#..
                """,
        ['/'] = """
                ....#
                ....#
                ...#.
                ..#..
                .#...
                #....
                #....
                """,
        ['!'] = """
                ..#..
                ..#..
                ..#..
                ..#..
                ..#..
                .....
                ..#..
                """,
        ['?'] = """
                .###.
                #...#
                ....#
                ...#.
                ..#..
                .....
                ..#..
                """,
        ['+'] = """
                .....
                ..#..
                ..#..
                #####
                ..#..
                ..#..
                .....
                """,
        ['%'] = """
                ##..#
                ##.#.
                ...#.
                ..#..
                .#...
                .#.##
                #..##
                """,
        ['*'] = """
                .....
                #.#.#
                .###.
                #####
                .###.
                #.#.#
                .....
                """,
        ['<'] = """
                ....#
                ...#.
                ..#..
                .#...
                ..#..
                ...#.
                ....#
                """,
        ['>'] = """
                #....
                .#...
                ..#..
                ...#.
                ..#..
                .#...
                #....
                """
    };
}
