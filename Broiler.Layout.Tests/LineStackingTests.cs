using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Linq;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A line starts where the line before it ends, however much taller than its line height vertical
/// alignment made that line.
/// </summary>
/// <remarks>
/// <para>
/// CSS 2.1 §9.4.2: line boxes are stacked with no space between them, and §10.8.1 makes a line box
/// reach from the top of its highest inline box to the bottom of its lowest. The flow starts each
/// line where it ended the one before, as tall as its line height and what it placed there, before
/// vertical alignment moves what is on it. A line that the alignment made taller, text in a larger
/// font or a box lowered from the baseline, reached into the line after it: in 16px/20px text, a
/// 32px "B" made its line 26px tall in browsers, and the next line started 20px down, where
/// browsers start it 26px down.
/// </para>
/// <para>
/// Each block here is 20px wide with 20px lines of a 16px font, whose glyphs stand 2px down their
/// line. Words are 8px wide a letter, and a space is 4px wide.
/// </para>
/// </remarks>
public sealed class LineStackingTests
{
    private static readonly Uri BaseUrl = new("file:///line-stacking.html");

    /// <summary>
    /// "B" in a 32px font, then " cc", which wraps. The 32px glyphs lower the first line's baseline
    /// to 19.6px down, and the strut reaches 5.2px below it, so the first line is 24.8px tall.
    /// "cc" starts the second line there, 26.8px down, and the block is 44.8px tall. "cc" was 22px
    /// down, and the block 40px tall.
    /// </summary>
    [Fact]
    public void The_Line_After_Text_In_A_Larger_Font_Starts_Below_It()
    {
        var block = Block();
        Text(Span(block, "32px"), "B");
        Text(block, " cc");
        Layout(block);

        Assert.Equal(26.8, Word(block, "cc").Top - block.Location.Y, 1);
        Assert.Equal(44.8, block.Size.Height, 1);
    }

    /// <summary>
    /// "a", an 8 × 10px image in a span in a 32px font, then " cc", which wraps: the span's strut
    /// makes the first line 24.8px tall, and "cc" stands 26.8px down. It stood 22px down.
    /// </summary>
    [Fact]
    public void The_Line_After_An_Image_In_A_Larger_Font_Starts_Below_It()
    {
        var block = Block();
        Text(block, "a");
        Image(Span(block, "32px"));
        Text(block, " cc");
        Layout(block);

        Assert.Equal(26.8, Word(block, "cc").Top - block.Location.Y, 1);
        Assert.Equal(44.8, block.Size.Height, 1);
    }

    /// <summary>
    /// "aa", then "B" in a 32px font on a second line, then "cc" on a third: the second line starts
    /// 20px down and is 24.8px tall, so "cc" stands 46.8px down, and the block is 64.8px tall.
    /// "cc" was 42px down, and the block 60px tall; "aa" stays 2px down.
    /// </summary>
    [Fact]
    public void A_Taller_Line_Between_Two_Others_Moves_Only_The_Lines_After_It()
    {
        var block = Block();
        Text(block, "aa ");
        Text(Span(block, "32px"), "B");
        Text(block, " cc");
        Layout(block);

        Assert.Equal(2, Word(block, "aa").Top - block.Location.Y, 1);
        Assert.Equal(46.8, Word(block, "cc").Top - block.Location.Y, 1);
        Assert.Equal(64.8, block.Size.Height, 1);
    }

    /// <summary>
    /// "a", then a 10px inline-block lowered 30px, then " cc", which wraps: the box reaches 44.8px
    /// down its line, so "cc" starts the second line there, 46.8px down, and the block is 64.8px
    /// tall. "cc" was 22px down, under the box, and the block 44.8px tall.
    /// </summary>
    [Fact]
    public void The_Line_After_A_Lowered_Box_Starts_Below_It()
    {
        var block = Block();
        Text(block, "a");
        new CssBox(block, new HtmlTag("span", false, null), BaseUrl)
        {
            Display = CssConstants.InlineBlock,
            Width = "8px",
            Height = "10px",
            VerticalAlign = "-30px",
        };
        Text(block, " cc");
        Layout(block);

        Assert.Equal(46.8, Word(block, "cc").Top - block.Location.Y, 1);
        Assert.Equal(64.8, block.Size.Height, 1);
    }

    /// <summary>
    /// Control, which passes before and after: "aa bb" wraps into two 20px lines, "bb" 22px down.
    /// </summary>
    [Fact]
    public void Control_Two_Lines_Of_Text()
    {
        var block = Block();
        Text(block, "aa bb");
        Layout(block);

        Assert.Equal(22, Word(block, "bb").Top - block.Location.Y, 1);
        Assert.Equal(40, block.Size.Height, 1);
    }

    /// <summary>
    /// Control, which passes before and after: "a", "B" in a span with <c>line-height: 40px</c>,
    /// then " cc": the flow already starts the second line below the 40px line, and "cc" stands
    /// 42px down.
    /// </summary>
    [Fact]
    public void Control_A_Line_Made_Taller_By_A_Taller_Line_Height()
    {
        var block = Block();
        Text(block, "a");
        var span = Span(block);
        span.LineHeight = "40px";
        Text(span, "B");
        Text(block, " cc");
        Layout(block);

        Assert.Equal(42, Word(block, "cc").Top - block.Location.Y, 1);
        Assert.Equal(60, block.Size.Height, 1);
    }

    /// <summary>
    /// Control, which passes before and after: in a column flex container, a 30px item with
    /// <c>margin-bottom: -25px</c> lets the 10px item after it overlap it, 5px down. The container's
    /// lines hold its items, not line boxes, and do not stack.
    /// </summary>
    [Fact]
    public void Control_A_Column_Flex_Item_With_A_Negative_Margin_Is_Overlapped()
    {
        var container = Block();
        container.Display = "flex";
        container.FlexDirection = "column";
        container.Width = "100px";

        new CssBox(container, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Height = "30px",
            MarginBottom = "-25px",
        };
        var second = new CssBox(container, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Height = "10px",
        };
        Layout(container);

        Assert.Equal(5, second.Location.Y - container.Location.Y, 1);
    }

    /// <summary>A 20px block with 20px lines of a 16px font, in a block in the root.</summary>
    private static CssBox Block()
    {
        var root = new CssBox(null, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };

        var body = new CssBox(root, new HtmlTag("div", false, null), BaseUrl) { Display = "block" };
        return new CssBox(body, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Width = "20px",
            FontSize = "16px",
            LineHeight = "20px",
        };
    }

    private static void Layout(CssBox block)
    {
        var root = block.ParentBox!.ParentBox!;
        root.PerformLayout(root.LayoutEnvironment);
    }

    /// <summary>A span in <paramref name="parent"/> inheriting its style, in the font size given.</summary>
    private static CssBox Span(CssBox parent, string? fontSize = null)
    {
        var span = new CssBox(parent, new HtmlTag("span", false, null), BaseUrl);
        span.InheritStyle();
        span.Display = CssConstants.Inline;

        if (fontSize != null)
            span.FontSize = fontSize;

        return span;
    }

    /// <summary>An 8 × 10px inline image.</summary>
    private static CssBoxImage Image(CssBox parent)
    {
        var image = new CssBoxImage(parent, new HtmlTag("img", true, new System.Collections.Generic.Dictionary<string, string> { ["src"] = "i.png" }), BaseUrl)
        {
            Display = CssConstants.Inline,
            Width = "8px",
            Height = "10px",
        };
        image.InheritStyle();
        return image;
    }

    /// <summary>
    /// An anonymous inline box in <paramref name="parent"/> holding the text, inheriting the
    /// parent's style as the box a text node makes does.
    /// </summary>
    private static void Text(CssBox parent, string text)
    {
        var box = new CssBox(parent, null, BaseUrl);
        box.InheritStyle();
        box.Display = CssConstants.Inline;
        box.Text = text.AsMemory();
        box.ParseToWords();
    }

    private static CssRect Word(CssBox block, string text) =>
        Descendants(block).SelectMany(b => b.Words).Single(w => w.Text == text);

    private static System.Collections.Generic.IEnumerable<CssBox> Descendants(CssBox box)
    {
        foreach (var child in box.Boxes)
        {
            yield return child;

            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }

    private sealed class FakeLayoutEnvironment : ILayoutEnvironment
    {
        private static readonly ConcurrentDictionary<double, FakeFont> Fonts = new();
        public Broiler.Graphics.Text.ILayoutFont GetFont(string family, double size, LayoutFontStyle style, string? fontFeatures = null) => Fonts.GetOrAdd(size, s => new FakeFont(s));
        public SizeF MeasureText(Broiler.Graphics.Text.ILayoutFont font, string text) => new(8 * text.Length, (float)font.Height);
        public void MeasureText(Broiler.Graphics.Text.ILayoutFont font, string text, double maxWidth, out int charFit, out double charFitWidth) { charFit = text.Length; charFitWidth = 8 * text.Length; }
        public double GetWhitespaceWidth(Broiler.Graphics.Text.ILayoutFont font) => 4;
        public ImageIntrinsics GetImageIntrinsics(object imageHandle) => new(300, 150, true);
        public Broiler.Graphics.Color.BColor ParseColor(string value) => default;
        public void RequestRefresh(bool relayout) { }
        public SizeF ViewportSize => new(1024, 768);
        public PointF RootLocation => PointF.Empty;
        public SizeF ActualSize { get; set; }
        public bool AvoidGeometryAntialias => false;
        public SizeF PageSize => new(1024, 768);
        public int MarginTop => 0;
        public void ReportLayoutError(string message, Exception? exception = null) { }
        public bool AvoidAsyncImagesLoading => true;
        public bool AvoidImagesLateLoading => true;
        public ILayoutImageLoader CreateImageLoader(Action<object?, RectangleF, bool> onComplete) => new ImageLoader(onComplete);
        public string FormatListMarker(int number, string style) => string.Empty;
    }

    private sealed class ImageLoader(Action<object?, RectangleF, bool> onComplete) : ILayoutImageLoader
    {
        private static readonly object TheImage = new();

        public object? Image { get; private set; }
        public RectangleF Rectangle => RectangleF.Empty;

        public void LoadImage(string src, System.Collections.Generic.IReadOnlyDictionary<string, string>? attributes, Uri baseUrl)
        {
            Image = TheImage;
            onComplete(TheImage, RectangleF.Empty, false);
        }

        public void Dispose() { }
    }

    /// <summary>A font of the given size in points, as tall in pixels as its size in pixels.</summary>
    private sealed class FakeFont(double size) : Broiler.Graphics.Text.ILayoutFont
    {
        public double Size => size;
        public double Height => size * 4 / 3;
        public double UnderlineOffset => 0;
        public double LeftPadding => 0;
        public string? FontFeatures => null;
    }
}
